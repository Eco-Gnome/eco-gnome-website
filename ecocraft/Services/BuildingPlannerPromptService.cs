using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Anthropic.Models.Models;
using ecocraft.BuildingPlanner.Generator;
using ecocraft.Models;
using Microsoft.AspNetCore.DataProtection;

namespace ecocraft.Services;

// Génération d'un programme architectural par un LLM (Claude ou Mistral, AiProviders) : le modèle décrit le bâtiment, le
// générateur C# le construit. Singleton sans état — la clé appartient au serveur (une par fournisseur, posée par un admin,
// chiffrée en base via Data Protection, déchiffrée ici à chaque appel) et le catalogue de matériaux est passé à chaque appel
// (il varie par serveur). Le schéma passe par un outil non strict, pas par la sortie structurée : celle-ci compile le schéma en
// grammaire et l'API refuse « compiled grammar is too large » dès six tableaux d'éléments (mesuré le 2026-09-20,
// enums de matériaux retirées comprises). Le générateur re-clampe et ignore les champs inconnus, donc la contrainte
// souple de l'outil suffit ; le system prompt reste constant et donc cacheable. Opus 5.5 refuse le tool_choice forcé
// (400) : l'outil est proposé en « auto » et le prompt exige son appel ; une réponse sans appel est relancée une fois.
// Modification d'un plan existant : le plan décrit en texte (PlanDescriber) va dans le message, Claude renvoie une PlanEdit
// par l'outil submit_plan_edit (seul outil de la requête), appliquée ensuite par PlanEditor.
// Mistral : API chat/completions (format OpenAI) par HttpClient, même outil et même prompt système ; tool_choice « any »
// y force l'appel de l'outil ; pas de cache de prompt, le prompt système est renvoyé en entier à chaque appel.
public sealed class BuildingPlannerPromptService(IDataProtectionProvider dataProtection, ILogger<BuildingPlannerPromptService> log)
{
    private const int MaxPromptChars = 2000;              // ≈ 500 tokens, peu face aux ~11k du prompt système
    private const int MaxTokens = 16000;                  // réflexion comprise : ~5k de réflexion + ~2k de programme mesurés sur Opus 5.5
    private const string ProgramTool = "submit_building_program";
    private const string EditTool = "submit_plan_edit";

    // Appels longs (jusqu'à 180 s) : c'est l'annulation de la page qui coupe, pas le timeout du client.
    private static readonly HttpClient MistralHttp = new() { BaseAddress = new Uri("https://api.mistral.ai/"), Timeout = Timeout.InfiniteTimeSpan };

    private readonly IDataProtector _protector = dataProtection.CreateProtector("Server.AiApiKey");

    public string Protect(string apiKey) => _protector.Protect(apiKey);

    // Null si aucune clé ou si les clés Data Protection ont changé (la clé stockée est alors illisible : à ressaisir).
    public string? Unprotect(string? protectedKey)
    {
        if (string.IsNullOrEmpty(protectedKey)) return null;
        try { return _protector.Unprotect(protectedKey); }
        catch (Exception e) { log.LogWarning(e, "bp-ai: stored key unreadable"); return null; }
    }

    // Clé déchiffrée du fournisseur actif du serveur et son modèle (celui par défaut s'il n'est plus proposé) ; null sans clé lisible.
    public AiCredentials? CredentialsFor(Server server)
    {
        if (ServerAiKey.Active(server) is not { } key || !AiProviders.Models.TryGetValue(key.Provider, out var models)) return null;
        var apiKey = Unprotect(key.KeyProtected);
        return apiKey is null ? null : new AiCredentials(key.Provider, apiKey, models.Contains(key.Model) ? key.Model : models[0]);
    }

    // Vérifie la clé sans consommer de tokens (liste des modèles). Null si OK, sinon suffixe de ServerManagement.AiKey.Error.*.
    public async Task<string?> ValidateAsync(string provider, string apiKey, CancellationToken ct)
    {
        if (provider == AiProviders.Mistral)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "v1/models");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                using var response = await MistralHttp.SendAsync(request, ct);
                if (response.IsSuccessStatusCode) return null;
                log.LogWarning("bp-ai: mistral validate {Status}", (int)response.StatusCode);
                return (int)response.StatusCode is 401 or 403 ? "InvalidKey" : "Unavailable";
            }
            catch (HttpRequestException e) { log.LogWarning(e, "bp-ai: mistral validate, network error"); return "Unavailable"; }
        }
        try
        {
            await new AnthropicClient { ApiKey = apiKey }.Models.List(new ModelListParams { Limit = 1 }, ct);
            return null;
        }
        catch (Anthropic5xxException e) { log.LogWarning(e, "bp-ai: validate, service unavailable"); return "Unavailable"; }
        catch (AnthropicApiException e) { log.LogWarning("bp-ai: key rejected: {Message}", e.Message); return "InvalidKey"; }
        catch (AnthropicIOException e) { log.LogWarning(e, "bp-ai: validate, network error"); return "Unavailable"; }
    }

    // ErrorKey = suffixe de BuildingPlanner.Ai.Error.* ; Program non null sinon.
    public async Task<PromptResult> GenerateAsync(AiCredentials? credentials, string prompt, IReadOnlyCollection<string> materials, IReadOnlyCollection<FurnitureInfo> doors, IReadOnlyCollection<FurnitureInfo> furniture, CancellationToken ct)
    {
        if (credentials is null || materials.Count == 0) return PromptResult.Fail("Disabled");
        if (prompt.Length > MaxPromptChars) prompt = prompt[..MaxPromptChars];
        var (program, error) = await CallAsync(credentials, prompt, ProgramTool, "Submit the architectural program of the requested building.",
            BuildingProgramSchema.Build(materials, doors, furniture), BuildingProgramJson.Parse, ct);
        return new PromptResult(program, error);
    }

    // Modification du plan décrit par description (PlanDescriber) ; ErrorKey comme GenerateAsync, Edit non null sinon.
    public async Task<EditResult> EditAsync(AiCredentials? credentials, string prompt, string description, IReadOnlyCollection<string> materials, IReadOnlyCollection<FurnitureInfo> doors, IReadOnlyCollection<FurnitureInfo> furniture, CancellationToken ct)
    {
        if (credentials is null || materials.Count == 0) return new EditResult(null, "Disabled");
        if (prompt.Length > MaxPromptChars) prompt = prompt[..MaxPromptChars];
        var content = $"""
            Existing plan:
            {description}
            Change request:
            {prompt}
            """;
        var (edit, error) = await CallAsync(credentials, content, EditTool, "Submit the changes to make to the existing plan.",
            BuildingProgramSchema.BuildEdit(materials, doors, furniture), PlanEditJson.Parse, ct);
        return new EditResult(edit, error);
    }

    // Un appel, l'outil toolName seul proposé ; parse lit son entrée. (null, clé d'erreur) en cas d'échec.
    private Task<(T? Value, string? ErrorKey)> CallAsync<T>(AiCredentials credentials, string content, string toolName, string toolDescription, JsonObject inputSchema, Func<string, T> parse, CancellationToken ct) where T : class
        => credentials.Provider == AiProviders.Mistral
            ? CallMistralAsync(credentials, content, toolName, toolDescription, inputSchema, parse, ct)
            : CallAnthropicAsync(credentials, content, toolName, toolDescription, inputSchema, parse, ct);

    private async Task<(T? Value, string? ErrorKey)> CallAnthropicAsync<T>(AiCredentials credentials, string content, string toolName, string toolDescription, JsonObject inputSchema, Func<string, T> parse, CancellationToken ct) where T : class
    {
        var schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(inputSchema.ToJsonString())!;
        var tool = new Tool
        {
            Name = toolName,
            Description = toolDescription,
            InputSchema = new InputSchema(schema),
        };
        var client = new AnthropicClient { ApiKey = credentials.ApiKey };
        var sw = Stopwatch.StartNew();

        // Une seule relance : la contrainte souple de l'outil laisse passer de rares JSON malformés (objet rendu comme
        // chaîne, entier avec un zéro initial — vu une fois sur quatre appels), et le second appel relit le cache.
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            Message response;
            try
            {
                // Pas de streaming : un JSON partiel ne sert à rien. L'annulation (timeout de la page) coupe la requête.
                response = await client.Messages.Create(new MessageCreateParams
                {
                    Model = credentials.Model,
                    MaxTokens = MaxTokens,
                    System = new List<TextBlockParam> { new() { Text = SystemPrompt, CacheControl = new CacheControlEphemeral() } },
                    OutputConfig = new OutputConfig { Effort = Effort.Medium },   // low essayé le 2026-09-27 : 26 c au lieu de 34, rendu nettement moins bon
                    Tools = [tool],
                    Messages = [new() { Role = Role.User, Content = content }],
                }, ct);
            }
            catch (AnthropicRateLimitException) { return (null, "Busy"); }
            catch (Anthropic5xxException e) { log.LogWarning(e, "bp-ai: service unavailable"); return (null, "Unavailable"); }
            // L'API renvoie un 400 générique quand le compte n'a plus de crédit : seul le texte le distingue.
            catch (AnthropicBadRequestException e) when (e.Message.Contains("credit balance", StringComparison.OrdinalIgnoreCase))
            { log.LogWarning("bp-ai: no credit left: {Message}", e.Message); return (null, "NoCredit"); }
            catch (Exception e) when (e is AnthropicUnauthorizedException or AnthropicForbiddenException)
            { log.LogWarning("bp-ai: key rejected: {Message}", e.Message); return (null, "KeyRejected"); }
            catch (AnthropicApiException e) { log.LogWarning(e, "bp-ai: api error"); return (null, "Failed"); }
            catch (AnthropicIOException e) { log.LogWarning(e, "bp-ai: network error"); return (null, "Unavailable"); }

            var input = response.Content.Select(bl => bl.Value).OfType<ToolUseBlock>().FirstOrDefault(bl => bl.Name == toolName)?.Input;
            // programChars / 4 ≈ tokens du programme ; le reste de « out » est de la réflexion.
            log.LogInformation("bp-ai: tool={Tool} try={Try} in={In} out={Out} cacheRead={CacheRead} cacheWrite={CacheWrite} programChars={ProgramChars} ms={Ms} stop={Stop}",
                toolName, attempt, response.Usage.InputTokens, response.Usage.OutputTokens, response.Usage.CacheReadInputTokens, response.Usage.CacheCreationInputTokens,
                input is null ? 0 : JsonSerializer.Serialize(input).Length, sw.ElapsedMilliseconds, response.StopReason);
            if (response.StopReason == "max_tokens") return (null, "TooComplex");
            if (response.StopReason == "refusal") return (null, "Refused");

            if (input is null) { log.LogWarning("bp-ai: no tool call, try {Try}", attempt); continue; }
            try { return (parse(JsonSerializer.Serialize(input)), null); }
            catch (JsonException e) { log.LogWarning(e, "bp-ai: unreadable json, try {Try}", attempt); }
        }
        return (null, "Invalid");
    }

    // Même contrat que l'appel Anthropic. 429 = limite de débit, ou modèle sans quota pour cette offre (clé gratuite).
    private async Task<(T? Value, string? ErrorKey)> CallMistralAsync<T>(AiCredentials credentials, string content, string toolName, string toolDescription, JsonObject inputSchema, Func<string, T> parse, CancellationToken ct) where T : class
    {
        var body = new JsonObject
        {
            ["model"] = credentials.Model,
            ["max_tokens"] = MaxTokens,
            ["temperature"] = 0.2,
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "system", ["content"] = SystemPrompt }, new JsonObject { ["role"] = "user", ["content"] = content }),
            ["tools"] = new JsonArray(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = toolName, ["description"] = toolDescription, ["parameters"] = inputSchema.DeepClone() },
            }),
            ["tool_choice"] = "any",
        }.ToJsonString();
        var sw = Stopwatch.StartNew();

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            string text;
            int status;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.ApiKey);
                using var response = await MistralHttp.SendAsync(request, ct);
                status = (int)response.StatusCode;
                text = await response.Content.ReadAsStringAsync(ct);
            }
            catch (HttpRequestException e) { log.LogWarning(e, "bp-ai: mistral network error"); return (null, "Unavailable"); }
            if (status is < 200 or >= 300)
            {
                log.LogWarning("bp-ai: mistral {Status}: {Body}", status, text.Length > 500 ? text[..500] : text);
                // 403 « tier_not_allowed » : clé valide, modèle hors de l'offre (Large et Medium avec la clé gratuite).
                if (status == 403 && text.Contains("tier_not_allowed", StringComparison.Ordinal)) return (null, "ModelNotInPlan");
                return (null, status switch { 401 or 403 => "KeyRejected", 429 => "RateLimited", >= 500 => "Unavailable", _ => "Failed" });
            }

            string? arguments = null, finish = null;
            int? promptTokens = null, completionTokens = null;
            try
            {
                using var json = JsonDocument.Parse(text);
                var root = json.RootElement;
                var choice = root.GetProperty("choices")[0];
                finish = choice.TryGetProperty("finish_reason", out var f) ? f.GetString() : null;
                if (choice.GetProperty("message").TryGetProperty("tool_calls", out var calls) && calls.ValueKind == JsonValueKind.Array)
                    foreach (var call in calls.EnumerateArray())
                    {
                        var function = call.GetProperty("function");
                        if (function.GetProperty("name").GetString() != toolName) continue;
                        var args = function.GetProperty("arguments");
                        arguments = args.ValueKind == JsonValueKind.String ? args.GetString() : args.GetRawText();
                        break;
                    }
                if (root.TryGetProperty("usage", out var usage))
                {
                    promptTokens = usage.TryGetProperty("prompt_tokens", out var p) ? p.GetInt32() : null;
                    completionTokens = usage.TryGetProperty("completion_tokens", out var c) ? c.GetInt32() : null;
                }
            }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
            { log.LogWarning(e, "bp-ai: mistral unreadable response, try {Try}", attempt); continue; }

            log.LogInformation("bp-ai: provider=mistral model={Model} tool={Tool} try={Try} in={In} out={Out} programChars={ProgramChars} ms={Ms} stop={Stop}",
                credentials.Model, toolName, attempt, promptTokens, completionTokens, arguments?.Length ?? 0, sw.ElapsedMilliseconds, finish);
            if (finish is "length" or "model_length") return (null, "TooComplex");
            if (arguments is null) { log.LogWarning("bp-ai: mistral no tool call, try {Try}", attempt); continue; }
            try { return (parse(arguments), null); }
            catch (JsonException e) { log.LogWarning(e, "bp-ai: mistral unreadable json, try {Try}", attempt); }
        }
        return (null, "Invalid");
    }

    // Constant (jamais la liste des matériaux, qui varie par serveur et casserait le cache) ; la langue de la réponse
    // suit celle du prompt pour « name » seulement, le reste est du JSON contraint par le schéma.
    private const string SystemPrompt = """
        You design buildings for the game Eco as an "architectural program": a JSON document validated by a strict schema.
        Always answer by calling the tool you are given (submit_building_program for a new building, submit_plan_edit to change
        an existing plan), never with text alone. You never draw voxels. A deterministic generator turns your program into blocks; it clamps everything, so
        prefer clear, well-proportioned compositions over clever tricks.

        Coordinate frame (planner grid, one unit = one game block):
        - x grows east (to the right), y grows south (down on the plan), z grows up. Grid origin (0,0) is the north-west corner.
        - Volumes: x,y is the minimum corner of the footprint; width along x, depth along y; baseZ is the z of the volume's slab;
          height is the number of air layers between slab and ceiling. Walls occupy the outer ring of the footprint.
          Every volume gets its slab, the ground storey too (at baseZ 0 the slab replaces the terrain).
        - The ceiling of a volume is at z = baseZ + height + 1. A roof covering that volume has baseZ = baseZ + height + 1 and
          the same footprint (plus overhang). A storey stacked on a volume has baseZ = baseZ + height + 1 and floorMaterial "".
        - Under a gable or hip roof with courses 0, the top volume has no flat ceiling: the room is open up to the roof, and
          the generator raises the walls and full-height partitions to the roof and fills the steps under the slopes itself.
          Set flatCeiling only when the request explicitly asks for a flat ceiling or an attic; by default leave it false.
        - Openings target a volume or partition by id. side "north" = the y-min edge, "south" = y-max, "west" = x-min, "east" = x-max,
          "auto" = let the generator choose. offset counts from the minimum corner of that edge. sill is the height of the opening's
          bottom above the slab: 0 for a door, 1 or more for a window. width/height in blocks.
        - Stairs: x,y is the first step, they climb toward direction and reach toZ; fromZ is the slab they start on.
        - Columns: count copies stepping by (stepX, stepY) from x,y; size 1 or 2; height in blocks. baseZ is the slab or
          ground they stand on (0 on the ground), like fences: a column occupies baseZ+1 to baseZ+height, so under a roof or
          a slab at z its height is z - baseZ - 1.
        - Partitions are straight inner walls from (x0,y0) to (x1,y1) (axis-aligned) on a given baseZ.
        - wallForm (volumes and partitions): "" = the material's own wall. Every material uses Wall except
          FarEastLumberItem, whose walls are Wall_01 to Wall_19, one block thick: Wall_01 plain plaster, Wall_02 framed with
          panel, Wall_03 framed top and bottom, Wall_04 plain with panel, Wall_05 and Wall_06 framed vertical edge (single,
          double), Wall_07 framed vertical mid, Wall_08 framed horizontal double with mid, Wall_09 framed horizontal mid,
          Wall_10 framed vertical pattern, Wall_11 decorative block, Wall_12 framed X, Wall_13 and Wall_14 framed corner top
          and bottom, Wall_15 and Wall_16 framed T vertical and horizontal, Wall_17 framed top with panel, Wall_18 framed end
          with panel, Wall_19 framed vertical edge with panel. The generator turns each side, adds the corners and the T or X
          where partitions meet in the matching style, and uses the FarEast stairs and windows by itself. Half-timbered,
          Japanese or Far East style = FarEastLumberItem walls with a framed wallForm (Wall_03, Wall_05, Wall_12…), Wall_01
          for plain rooms; one wallForm per volume, so vary it between storeys or wings. AdobeItem walls are always
          WallMid (rounded adobe wall, one block thick); the generator turns them and adds WallCorner, WallT and WallX,
          and uses the adobe stairs (StairsMid) and window (Window) by itself. Adobe has no sloped roof pieces: give an
          adobe building a flat roof (overhang 0), or a sloped roof in another material. An adobe flat roof is built as in
          the game: parapet blocks on the walls, with the wooden beam ends (vigas) showing outside, and a RoofFill deck;
          parapet true adds a low rounded adobe wall on top.

        Editing an existing plan (message "Existing plan" + "Change request", tool submit_plan_edit): the plan may be drawn by
        hand or generated. It is listed as ops painted in order (a later op overwrites an earlier one, "cut" carves air),
        objects, rooms, the planner's problems and a top view of each level. Change only what the request asks for (and what
        it forces, such as a roof following a taller storey); everything you do not mention stays exactly as it is.
        - changeOps repaints an op (material), changes its form or rot, or moves its corners a and b; removeOps deletes ops.
          To move or reshape a part, change its corners or remove it and add it again. A "cells" op is listed by its bounds
          only: remove or repaint it whole, or carve into it with cuts.
        - cuts carve air boxes after the existing ops: a passage, a stairwell, a door hole in a wall drawn cell by cell.
        - add takes the same elements and rules as a new building, in the plan's coordinates; they are built after the
          existing ops, over them. A new volume uses the base of an existing level, or stands above the top level (it
          then adds a level). An opening may target an existing wall op by its id: a hollow box is a room (side as usual),
          a thin solid box is a partition; the door hole is carved and the door object placed.
        - Furniture goes in add.furniture (inside a room, off the walls, never on another object); to move a piece, put
          its id in removeObjects and add it again. Leave the arrays you do not need empty.

        Materials: use only names from the schema enum (technical item names such as "MortaredSandstoneItem",
        "HewnLogItem"). Map the player's wording to the closest enum entry. Prefer one main material, one accent material
        for trim/roof/columns, and at most three in total unless asked otherwise.
        Doors: every "door" opening names its door object in "door", from the schema enum. Match it to the building: by
        default the door of the wall's family (HewnDoorItem for HewnLogItem walls, MortaredStoneDoorItem for
        MortaredStoneItem), a different one only when the requested style calls for it. Windows leave "door" empty.
        A door opening takes the size of its door (listed in the schema: ShojiDoorItem is 4 wide, the large doors 5 wide
        and 4 high), so leave room for it in the wall and keep furniture off the cells just inside and outside it.
        Furniture: the "furniture" array puts catalogue objects on the floor of rooms when the request asks for a
        furnished building or implies one (a home, an inn, a tavern, a workshop). Match the family to the walls: Hewn*
        objects with HewnLogItem, Mortared<Stone>* with the mortared stones, Lumber* with lumber, AdobeDoorItem with
        adobe. x, y is the north-west cell of the piece's footprint (footprints are listed in the schema; rot 1 and 3
        swap width and depth), baseZ is the slab of its room. Keep every piece strictly inside the room, off the walls,
        never in front of a door, with one free cell of walkway; three to eight pieces per room, chairs beside tables,
        beds, dressers and nightstands along walls, a fireplace against a wall. Leave the array empty when no
        furniture is wanted.

        Plausibility rules:
        - Storey height 3 to 4 (height field). Footprints usually 6 to 20 blocks a side; keep the grid compact (footprint + 4).
        - Exactly one door per building at ground level (sill 0, height 2) unless asked otherwise; windows have sill >= 1,
          height 1 or 2, spaced every 2 to 4 blocks, never in corners.
        - Every volume needs a roof or a storey above it. Gable and hip roofs use overhang 1; flat roofs use
          parapet 1 for a modern look. "Modern architect" style = flat roofs, large windows (width 2 to 4), pilotis or columns,
          two materials, clean offsets between stacked volumes. "Medieval" or "cottage" = gable roof, small windows, one storey.
        - Stairs only when there is an upper storey; place them inside the footprint, away from the door.
        - Use the empty arrays for element types you do not need. Keep ids short (letters, digits, dashes).
        - name: a short title in the language of the request.

        Roofs: slopes are fixed at 45 degrees, one course per layer, each course one cell further in. The generator
        places the slopes (RoofSide), the hip corners (RoofCorner) and the ridge (RoofPeak) with the right rotations; for
        FarEastLumberItem it makes the lowest course an upturned eave (RoofEdgeSide, RoofEdgeCorner) by itself.
        - courses 0 = an ordinary roof that rises to its ridge. It would cut into any storey standing on it, so a roof
          with courses 0 only covers the top of a building.
        - courses N > 0 = an eave of N courses from the outer edge, with no ridge and no gable: a skirt roof around the
          storey standing on the covered volume. N = overhang + setback of the storey above, so the eave stops against
          its walls.
        - Tiered roofs (pagoda, Chinese or Japanese tower, temple, castle keep): stack storeys, each set back one cell on
          every side (x+1, y+1, width-2, depth-2, baseZ = ceiling of the storey below, floorMaterial ""). Over every
          storey but the top one, a hip roof with that storey's footprint, baseZ = its ceiling, overhang 1, courses 2;
          storey height 3 or 4 so the walls show between the eaves. The top storey gets a hip roof with courses 0.
          FarEastLumberItem walls (framed wallForm, varied per storey) and roofs give the Far East look.
        - Japanese temple or Japanese house: never a plain box on a box. Build, from the ground up: a stone platform (a slab
          at z 0, thickness 1, of a mortared stone or ashlar, two to three cells wider than the building on every side; the
          ground storey keeps baseZ 0, since a slab one layer below a storey breaks the levels); on it a veranda (engawa) one or two cells wide all around, marked by a row of
          FarEastLumberItem columns every two or three cells along its outer edge and a FarEastLumberItem fence on that edge,
          open in front of each entrance (the columns stand on the fence path and become its posts, the rail runs between
          them); the walls set back inside the veranda, framed wallForm on the ground storey and
          another one upstairs; windows grouped in bands of two to four, not one every few cells; deep eaves (overhang 1
          or 2, courses so the eave covers the veranda); an upper storey smaller than the ground one with its own hip roof.
          The garden: GardenGravelItem slabs, a StoneRoadItem path from the gate to the stairs, a fence around the plot
          with a gate on the path, and a gateway (two columns and a beam) at the gate.

        Fences: every fence, barrier, railing, balustrade or garden enclosure goes in "fences", never as a low wall, a
        partition or blocks. One entry is a path of cells (points joined by straight runs, closed for an enclosure) and
        the generator lays the runs, corners, T, crossings and ends with the right pieces and rotations; gates are cells of
        the path left open, where a path or a door comes through. Fence pieces exist for FarEastLumberItem and AdobeItem
        (gardens, verandas, courtyards), the hewn logs (HewnLogItem: dock and pier railings), CorrugatedSteelItem,
        ReinforcedConcreteItem and FlatSteelItem (industrial), the Composite*Item (modern) and the Ashlar*Item
        (balustrades); pick the building's material when it has one. A railing on a veranda, a balcony or a deck stands on
        its slab (baseZ = that slab) along its outer edge. An enclosure always has at least one gate.

        Blocks: the element types above describe ordinary buildings. Everything else — a pier, a pontoon, a jetty, a
        ladder, a pile of logs — goes in "blocks". One entry fills a whole box (x, y, width, depth, z, height) with one
        game form, so a deck or a railing is a single entry, not one entry per cell.
        - Docks, over water or along a shore: DocksPlatform (the deck; DocksPlatformFill for a plain inner surface),
          DocksPillar (post standing against one edge of its cell), the DocksPillarBeam family (below), DocksColumn
          (free-standing mooring post), DocksFenceMid
          (railing run, with DocksFenceCorner, DocksFenceT, DocksFenceX, DocksFenceEndCap, DocksFenceEndCapDouble and
          DocksFenceSolo at ends and crossings), DocksRamps with DocksRampsCorner and DocksRampsCornerInverted (ramp
          from the deck down to the shore), DocksRampA to DocksRampD (gangway pieces), DocksBarrelPlatform (barrels).
        - Post and beam frames (gateway, pergola, crane frame): a beam block fills only the top of its cell, so a beam
          laid on top of a post floats. Stack DocksPillar in a column and use DocksPillarBeamJunction at each layer where
          a beam crosses it (post top + beam + knee braces), DocksPillarBeam between junctions, DocksPillarBeamCorner,
          DocksPillarBeamT and DocksPillarBeamX where beams turn or cross, DocksPillarBeamEnd as a short stub past the
          last post (DocksPillarBeamEndAlt: the same stub mirrored). DocksPillar and
          DocksPillarBeamJunction stand against the west, south, east, north edge of their cell for rot 0..3 (junction
          beam east-west for 0 and 2, north-south for 1 and 3); DocksPillarBeam runs east-west for rot 0, north-south for
          1; DocksPillarBeamEnd continues its beam toward east, north, west, south. Gateway as built in the game: two
          columns in adjacent cells (rot 0 at x, rot 2 at x+1), junctions two layers apart, one beam cell then a stub on
          each side of the lower beam, a stub on each side of the upper one.
        - Other pieces: Ladder (vertical, one cell wide), Stacked1, Stacked2, Stacked3 (piles of logs, decoration).
        - Glass (GlassItem): Cube is a glass block in a wooden frame; Window is a thin pane that joins the neighbouring
          panes and walls by itself (greenhouse, glass partition, shop front); ThinWallStraight and ThinWallCorner are
          panes the player turns, EdgeWall a pane against one edge of its cell, EdgeWallTurn a pane along two edges;
          FlatRoof (horizontal pane mid-cell), ThinFloorTop and ThinFloorBottom (pane at the top or bottom of the cell)
          make glass roofs and floors; Stacked1 to Stacked4 are crates of glass panes (decoration).
        - Brick (BrickItem) extra forms: Brace, BraceCorner, BraceTurn, SideBrace, SmallCornerBrace (corbels and
          buttresses under a beam or a floor edge), UnderBrace, UnderBraceCorner, UnderBraceTurn (the same, hanging
          under a slab), BasicSlopeSide and BasicSlopePoint (plain slope and its point), UnderSlopeSide and
          UnderSlopePeak (upside-down slope under an overhang), ThinFloorTop and ThinFloorBottom (thin slab at the top or
          bottom of the cell), ThinWallEdge (thin wall against one edge), WindowEdge and WindowGrillesEdge (window
          against one edge), Aqueduct (channel that joins the neighbouring aqueduct blocks), RampA to RampD (road ramp,
          one per cell in a row, lowest A; the sides join the ramps beside them).
        - Corrugated steel (CorrugatedSteelItem) and reinforced concrete (ReinforcedConcreteItem), industrial and modern:
          their window is Window (the generator uses it), DoubleWindow a wider framed window, Fence a railing, FlatRoof a
          flat roof slab whose free edges get an overhanging rim, RoadBarrier a barrier (steel guardrail, concrete jersey
          barrier); all of them join their neighbours by themselves. BasicSlopeSide, BasicSlopePoint, UnderSlopeSide and
          UnderSlopePeak as in Brick. Steel only: FloatStairs, FloatStairsTurn, FloatStairsCorner (open steel stairs).
          Concrete only: ThinColumn (slim pillar), UnderStairs (upside-down flight under a stair), BasicSlopeCorner and
          BasicSlopeTurn (outer and inner corner of a slope), UnderSlopeCorner and UnderSlopeTurn (the same upside down),
          HalfSlopeA and HalfSlopeB (gentle slope over two cells, B the upper half then A the lower half toward the low
          side), PeakSet and UnderPeakSet (ridge of the concrete slopes, upright or upside down, joining by itself).
        - Tier 5, ashlar stone (AshlarBasaltItem, AshlarGneissItem, AshlarGraniteItem, AshlarLimestoneItem,
          AshlarSandstoneItem, AshlarShaleItem: classical, monumental), composite lumber (CompositeLumberItem and one per
          wood: CompositeBirchLumberItem, Cedar, Ceiba, Fir, Joshua, Oak, Palm, Redwood, Saguaro, Spruce: modern wooden
          houses), flat steel (FlatSteelItem: industrial, high-tech) and framed glass (FramedGlassItem: glass walls in a steel
          frame, winter gardens, shop fronts). Ashlar and flat steel windows are Window (the generator uses it); composite
          and framed glass keep WindowGrilles. Flat steel has no Stairs: its stairs are FloatStairs. Shared with the concrete:
          FlatRoof, Fence, DoubleWindow, ThinColumn (composite, flat steel), UnderStairs, FloatStairs, FloatStairsTurn,
          FloatStairsCorner, PeakSet, UnderPeakSet, the BasicSlope and UnderSlope families, HalfSlopeA and HalfSlopeB
          (ashlar); ashlar and composite also have the Brick braces and RampA to RampD (a pediment or a ramp).
          FullWall (ashlar, composite) is a thick plain wall; composite also has WallTrim (wall with a trim course),
          CladWall (clapboard wall), WindowWall (full-height glazed wall) and SideFence (railing on the side of a
          cell). Flat steel WindowCorners is the steel post where panes meet in a corner, T or cross; put it between Window
          runs. Framed glass Wall and WindowGrilles are glazed; its Cube is a glass block.
        - Smart roof (Roof, RoofPeakSet, RoofCube; HewnLogItem, the mortared stones, BrickItem, LumberItem,
          CorrugatedSteelItem, ReinforcedConcreteItem, FlatSteelItem, FramedGlassItem; RoofPeakSet and RoofCube only for
          ashlar and composite): the game's
          own roof blocks, which pick slope, corner and ridge pieces from their neighbours. Build a stepped pyramid of Roof
          boxes one layer high, each layer one cell smaller on every side (7×5, then 5×3, then 3×1 for the ridge), with a
          ring of RoofCube under the first course to carry the eaves. RoofPeakSet makes a ridge line from a row of blocks
          (ends, T and crossings join by themselves), for L or T ridges. Prefer the roofs element for ordinary roofs.
        - Roof pieces, for roofs the roofs element cannot shape (an L or T roof, a tower cap), placed one cell wide, one
          layer per course: RoofSide (slope), RoofCorner (outer hip corner), RoofTurn (inner corner, where the two roofs
          of an L or T meet), RoofPeak (ridge). FarEastLumberItem adds RoofEdgeSide, RoofEdgeCorner, RoofEdgeTurn (the
          same with an upturned eave, for the lowest course), RoofPeakCorner, RoofPeakT, RoofPeakX (ridge turning,
          branching, crossing), RoofUnderslopeSide, RoofUnderslopeCorner, RoofUnderslopeTurn (sloped underside of an
          overhang, flat on top), UnderInnerPeak (the same under a ridge) and RoofCube (block carrying the eave).
        - Other FarEast pieces: Column_01, Column_02, Column_03 (base, shaft and top of a column; Column picks them by
          itself), FenceMid, FenceCorner, FenceT, FenceX (crossing), FenceEnd and FenceSolo (a lone short railing),
          StairsEndLeft and StairsEndRight (the two edges of a flight two or more wide, StairsMid between them),
          StairsTurn and StairsCorner, WallX_01 to _04 (walls crossing; the generator adds them), Ceiling.
        - Adobe (AdobeItem, pueblo and desert style): RoofMid, RoofCorner, RoofT, RoofX, RoofEnd, RoofSolo are the
          parapet blocks of a flat roof (the roofs element places them), with viga beam ends on the side that has no solid
          neighbour (RoofFill: a roof slab in the upper half of its cell); WallSolo, WallEnd,
          WallCorner, WallT, WallX are the pieces of a free-standing wall; UnderSlopeSide, Ladder, StairsSolo and the
          FarEast-like FenceMid, FenceCorner, FenceT, FenceX, FenceEnd, FenceSolo, StairsEndLeft, StairsEndRight,
          StairsTurn, StairsCorner complete the set. Adobe walls and railings grow a base and a cap by themselves when
          stacked.
        - Roads: StoneRoadItem and AsphaltConcreteItem are laid as Cube (its edges join the road beside it) with RampA to
          RampD for a slope. Asphalt adds painted lines: WhiteLine and WhiteDashLine (centre line, solid or dashed, that
          follows the line beside it), WhiteEdge (edge line that follows the road's border), WhiteEdgeRotate (line along
          one edge), TwoWhiteEdgeRotate (lines along two edges, a corner), WhiteCube (fully painted), and on ramps
          WhiteRampLineA-D, WhiteRampDashLineA-D, WhiteRampEdgeA-D.
        - Carpets (CottonCarpetItem, NylonCarpetItem, WoolCarpetItem): Floor (carpet with a wooden border that joins the
          carpet beside it), SimpleFloor (borderless), FullWall and Cube (carpet block with wooden trim), CanopyWindow
          (fabric awning over a window, stacks by itself), Stacked1 to Stacked4 (rolls).
        - Chimney (mortared stones, Brick): a column of Chimney blocks, base, shaft and top join by themselves.
          CopperPipe, IronPipe and SteelPipe: pipes that join the neighbouring pipes and may cross a wall or a slab.
        - rot matters only for forms the player turns; measured in the game's meshes:
          · Ladder: against the south side of its cell for rot 0, then east, north, west.
          · Stairs, StairsMid, DocksRamps, RoofSide, RoofEdgeSide, BasicSlopeSide, FloatStairs, HalfSlopeA, HalfSlopeB: the
            low side faces east, north, west, south for rot 0..3. UnderSlopeSide: thick side toward east, north, west,
            south. UnderStairs: thin side toward east, north, west, south.
          · RoofCorner, RoofTurn, RoofEdgeCorner, RoofEdgeTurn, low corner for rot 0..3: south-east, north-east,
            north-west, south-west for FarEastLumberItem, reinforced concrete and tier 5; south-west, south-east,
            north-east, north-west for HewnLogItem, the mortared stones, BrickItem, LumberItem and corrugated steel.
            RoofPeak: ridge east-west for rot 0 and 2, north-south for 1 and 3.
          · FarEast RoofPeakCorner: ridge arms north+east (0), north+west (1), south+west (2), south+east (3).
            RoofPeakT: branch toward north, west, south, east. RoofUnderslopeSide: thick side toward east, north, west,
            south. RoofUnderslopeCorner: thick corner south-east, north-east, north-west, south-west. RoofUnderslopeTurn:
            thin corner north-west, south-west, south-east, north-east. UnderInnerPeak: groove north-south for rot 0
            and 2, east-west for 1 and 3.
          · FarEast StairsEndLeft and StairsEndRight: rot of the flight's StairsMid; seen from the bottom of the flight,
            StairsEndLeft is the left edge (south edge for rot 0), StairsEndRight the right edge. StairsTurn: low corner
            south-east (0), north-east (1), north-west (2), south-west (3). StairsCorner: high corner north-west (0),
            south-west (1), south-east (2), north-east (3). FenceSolo: runs east-west for rot 0, north-south for 1.
          · Steel and concrete StairsTurn, FloatStairsTurn, BasicSlopeTurn: low corner south-west (0), south-east (1),
            north-east (2), north-west (3). StairsCorner, FloatStairsCorner, BasicSlopeCorner: high corner north-east (0),
            north-west (1), south-west (2), south-east (3). UnderSlopeCorner: thick corner north-west (0), south-west (1),
            south-east (2), north-east (3). UnderSlopeTurn: thin corner south-east (0), north-east (1), north-west (2),
            south-west (3).
          · Tier 5 (ashlar, composite, flat steel): StairsTurn and FloatStairsTurn (ashlar, composite) as the concrete;
            StairsCorner and FloatStairsCorner (ashlar, composite) too. BasicSlopeTurn and flat steel FloatStairsTurn: low
            corner south-east (0), north-east (1), north-west (2), south-west (3). BasicSlopeCorner and flat steel
            FloatStairsCorner: high corner north-west (0), south-west (1), south-east (2), north-east (3). UnderSlopeCorner:
            thick corner north-east (0), north-west (1), south-west (2), south-east (3). UnderSlopeTurn: thin corner
            south-west (0), south-east (1), north-east (2), north-west (3). BasicSlopePoint and UnderSlopePeak: ridge
            east-west for rot 0 and 2, north-south for 1 and 3. Braces as in Brick, except ashlar UnderBraceCorner and
            UnderBraceTurn: corner south-east (0), north-east (1), north-west (2), south-west (3); composite SideBrace:
            south-west, south-east, north-east, north-west; ashlar SideBrace: against the west, south, east, north side.
          · DocksRampA to DocksRampD and RampA to RampD of Brick, ashlar, composite and the roads, and the asphalt
            WhiteRamp pieces (lowest A, highest D, one per cell in a row): rise toward west, south, east, north.
          · Asphalt WhiteEdgeRotate: line along the north, west, south, east edge for rot 0..3. TwoWhiteEdgeRotate: lines
            along the west+north (0), west+south (1), east+south (2), east+north (3) edges.
          · Carpet CanopyWindow: against the west, south, east, north side of its cell for rot 0..3.
          · Adobe WallMid, WallSolo, WallEnd, RoofMid, RoofSolo, RoofEnd and Window: rot 0 runs east-west, rot 1
            north-south; WallEnd and RoofEnd have their rounded end toward west, south, east, north for rot 0..3.
            WallCorner, RoofCorner, FenceCorner as the FarEast WallCorner; WallT, RoofT as the FarEast WallT. The rest of
            the adobe railings and stairs turn as the FarEast ones.
          · Brick Brace, UnderBrace, ThinWallEdge: against the south, east, north, west side for rot 0..3; WindowEdge and
            WindowGrillesEdge: against the east, north, west, south side. BraceCorner, BraceTurn, UnderBraceCorner,
            UnderBraceTurn: corner south-west (0), south-east (1), north-east (2), north-west (3). SmallCornerBrace:
            south-east, north-east, north-west, south-west. SideBrace: south-east, north-east, south-west, north-west.
          · DocksFenceMid: rot 0 runs north-south, rot 1 east-west. DocksFenceCorner: arms east+south (0), east+north (1),
            west+north (2), west+south (3). DocksFenceT and FenceT: branch toward north, west, south, east. DocksFenceEndCap:
            cap at the north, west, south, east end.
          · FarEast FenceMid, Wall_01 to Wall_19 and Window: rot 0 runs east-west, rot 1 north-south. FenceCorner and
            WallCorner_01 to _04: arms west+north (0), west+south (1), east+south (2), east+north (3). WallT_01 to _04: branch
            toward north, west, south, east. FenceEnd: the rail continues toward east, north, west, south.
          · Glass ThinWallStraight: rot 0 runs east-west, rot 1 north-south. ThinWallCorner: arms east+north (0),
            west+north (1), west+south (2), east+south (3). EdgeWall: against the north, west, south, east edge for rot
            0..3. EdgeWallTurn: along the west+north (0), west+south (1), east+south (2), east+north (3) edges.
          · Forms shaped by their neighbours ignore rot: Wall, Floor, Column, Cube, Roof, RoofPeakSet, RoofCube, WallX_*,
            Ceiling, DocksColumn, Stacked1-4, the glass, steel and concrete Window, FlatRoof, ThinFloorTop, ThinFloorBottom,
            Aqueduct, BasicSlopePoint and UnderSlopePeak (Brick, steel, concrete), DoubleWindow, Fence, RoadBarrier,
            ThinColumn, PeakSet, UnderPeakSet, FullWall, WallTrim, CladWall, WindowWall, SideFence, WindowCorners,
            RoofPeakX, FenceX, Column_01 to _03, Chimney, CopperPipe, IronPipe, SteelPipe, the adobe WallX and RoofX, the
            asphalt WhiteLine, WhiteDashLine, WhiteEdge and WhiteCube, the carpet SimpleFloor.
            Railings go in "fences", which turns them; place fence pieces as blocks only for a lone piece.
        - A box is filled solid, so keep pilings, beams, railings and ladders one cell wide: pilings go in rows under
          the deck edges every three or four cells, never as one box the size of the deck. A deck is one layer thick.
        - Keep the dedicated elements for walls, floors, roofs, columns and stairs; blocks are for what they cannot
          express. A pier is about four entries plus its railings in fences: deck, pilings, ramp. Stay under eighty.
        """;
}

public sealed record PromptResult(BuildingProgram? Program, string? ErrorKey)
{
    public static PromptResult Fail(string key) => new(null, key);
}

public sealed record EditResult(PlanEdit? Edit, string? ErrorKey);

// Fournisseur (AiProviders), clé en clair et modèle d'un appel.
public sealed record AiCredentials(string Provider, string ApiKey, string Model);
