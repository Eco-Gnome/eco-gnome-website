using System.Text.RegularExpressions;
using ecocraft.Components.Diagram;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace ecocraft.Services;

public class GameObject(string id)
{
    public string Id = id;
    public string? Name;
    public GameObject? Parent;
    public List<GameObject> Children = [];
    public List<Component> Components = [];
    public readonly List<string> ComponentIDs = [];

    public override string ToString()
    {
        return $"!u!1 &{Id} {Name} ({Components.Count})";
    }

    public List<GameObject> GetDescendants()
    {
        var children = new List<GameObject>();
        children.AddRange(Children);

        foreach (var child in Children)
        {
            children.AddRange(child.GetDescendants());
        }

        return children;
    }
}

public class Component(string typeId, string id)
{
    public string TypeId = typeId;
    public string Id = id;
    public string? SpriteGuid;
    public readonly List<string> ChildrenIDs = [];
    public string? ParentId;
    public GameObject? GameObject;

    public override string ToString()
    {
        return $"!u!{TypeId} &{Id} {SpriteGuid}";
    }
}

public class Sprite(string name, string id, int posX, int posY, int width, int height, string textureId)
{
    public string Name = name;
    public string Id = id;
    public int PosX = posX;
    public int PosY = posY;
    public int Width = width;
    public int Height = height;
    public string TextureId = textureId;
}

public static class UnityStructureParser
{
    private static readonly Regex HeaderRegex = new Regex(@"^--- !u!(\d+) &(\d+)");
    private static readonly Regex NameRegexp = new Regex(@"^  m_Name: (.+)$");
    private static readonly Regex GoComponentRegexp = new Regex(@"^  - component: \{fileID: (\d+)\}$");
    private static readonly Regex ChildrenRegexp = new Regex(@"^  m_Children:$");
    private static readonly Regex ChildRegexp = new Regex(@"^  - \{fileID: (\d+)\}$");
    private static readonly Regex SpriteRegexp = new Regex(@"^  m_Sprite: \{fileID: \d+, guid: ([0-9a-z]+), type: 2\}$");

    private static readonly Regex GuidRegexp = new Regex(@"guid: ([0-9a-z]+)\n");

    private static readonly Regex SpriteDataOffsetRegexp = new Regex(@"  m_Rect:\n    serializedVersion: \d+\n    x: (\d+)\n    y: (\d+)\n    width: (\d+)\n    height: (\d+)\n");
    private static readonly Regex SpriteDataTextureRegexp = new Regex(@"  m_RD:\n    serializedVersion: \d+\n    texture: \{fileID: \d+, guid: ([0-9a-z]+), type: \d+\}\n");

    public static List<GameObject> ParseFile(string filePath)
    {
        var gos = new Dictionary<string, GameObject>();
        var components = new Dictionary<string, Component>();
        var isReadingChildren = false;

        using (StreamReader reader = new StreamReader(filePath))
        {
            string? line;
            GameObject? currentGO = null;
            Component? currentComponent = null;

            while ((line = reader.ReadLine()) != null)
            {
                var match = HeaderRegex.Match(line);
                if (match.Success)
                {
                    switch (match.Groups[1].Value)
                    {
                        case "1":
                        {
                            currentComponent = null;
                            currentGO = new GameObject(match.Groups[2].Value);
                            gos.Add(currentGO.Id, currentGO);
                            break;
                        }
                        default:
                        {
                            currentGO = null;
                            currentComponent = new Component(match.Groups[1].Value, match.Groups[2].Value);
                            components.Add(currentComponent.Id, currentComponent);
                            break;
                        }
                    }
                }
                else if (currentGO is not null)
                {
                    var nameMatch = NameRegexp.Match(line);

                    if (nameMatch.Success)
                    {
                        currentGO.Name = nameMatch.Groups[1].Value;
                        continue;
                    }

                    var goComponentMatch = GoComponentRegexp.Match(line);

                    if (goComponentMatch.Success)
                    {
                        currentGO.ComponentIDs.Add(goComponentMatch.Groups[1].Value);
                    }
                }
                else if (currentComponent is not null)
                {
                    if (isReadingChildren)
                    {
                        var childMatch = ChildRegexp.Match(line);

                        if (childMatch.Success)
                        {
                            currentComponent.ChildrenIDs.Add(childMatch.Groups[1].Value);
                        }
                        else
                        {
                            isReadingChildren = false;
                        }
                    }

                    var childrenMatch = ChildrenRegexp.Match(line);

                    if (childrenMatch.Success)
                    {
                        isReadingChildren = true;
                        continue;
                    }

                    var spriteMatch = SpriteRegexp.Match(line);

                    if (spriteMatch.Success)
                    {
                        currentComponent.SpriteGuid = spriteMatch.Groups[1].Value;
                    }
                }
            }
        }

        foreach (var go in gos.Values)
        {
            go.Components = go.ComponentIDs.Select(c => components[c]).ToList();

            foreach (var comp in go.Components)
            {
                comp.GameObject = go;
            }
        }

        foreach (var go in gos.Values)
        {
            foreach (var comp in go.Components)
            {
                if (comp.ChildrenIDs.Count > 0)
                {
                    go.Children = comp.ChildrenIDs.Select(c => components[c].GameObject!).ToList();

                    foreach (var child in go.Children)
                    {
                        child.Parent = go;
                    }
                }
            }
        }

        return gos.Values.Where(g => g.Parent is null).ToList();
    }

    private static List<GameObject> RetrieveEcoItems(List<GameObject> gameObjects)
    {
        var items = new List<GameObject>();

        foreach (var go in gameObjects)
        {
            if (go.Name!.EndsWith("Item"))
            {
                items.Add(go);
            }
            else
            {
                if (go.Name!.EndsWith("Object")) continue;

                if (go.Children.Count > 0)
                {
                    items.AddRange(RetrieveEcoItems(go.Children));
                }
            }

        }

        return items;
    }

    public static Dictionary<string, string> FindItemNameSpriteGuidAssociation(List<GameObject> parents)
    {
        var assoc = new Dictionary<string, string>();
        var allItemGos = RetrieveEcoItems(parents);

        foreach (var item in allItemGos)
        {
            var descendants = item.GetDescendants();
            var fullImageGo = descendants.FirstOrDefault(c => c.Name!.Equals("fullimage", StringComparison.CurrentCultureIgnoreCase));
            var foregroundGo = descendants.FirstOrDefault(c => c.Name!.Equals("foreground", StringComparison.CurrentCultureIgnoreCase));

            var fullImageGuid = fullImageGo?.Components.FirstOrDefault(c => c.SpriteGuid is not null)?.SpriteGuid;
            var foregroundGuid = foregroundGo?.Components.FirstOrDefault(c => c.SpriteGuid is not null)?.SpriteGuid;

            if (fullImageGuid is null && foregroundGuid is null)
            {
                Console.WriteLine($"No icon for {item.Name}");
                continue;
            }

            assoc.Add(item.Name!, fullImageGuid ?? foregroundGuid ?? "null");
        }

        return assoc;
    }

    public static Dictionary<string, string> FindMetaPathGuidAssociation(string path)
    {
        var assoc = new Dictionary<string, string>();

        var files = Directory.GetFiles(path);
        var metaFiles = files.Where(f => f.EndsWith(".meta")).ToList();

        foreach (var metaFile in metaFiles)
        {
            var content = File.ReadAllText(metaFile);
            var match = GuidRegexp.Match(content);
            if (match.Success)
            {
                assoc.Add(match.Groups[1].Value, metaFile.Replace(".meta", ""));
            }
        }

        return assoc;
    }

    public static List<Sprite> RetrieveSprites(Dictionary<string, string> itemNameSpriteGuidAssociation, Dictionary<string, string> spritePathAssociation)
    {
        var list = new List<Sprite>();

        foreach (var assoc in itemNameSpriteGuidAssociation)
        {
            var spriteFile = spritePathAssociation[assoc.Value];

            var content = File.ReadAllText(spriteFile);
            var match1 = SpriteDataOffsetRegexp.Match(content);
            var match2 = SpriteDataTextureRegexp.Match(content);

            if (match1.Success && match2.Success)
            {
                list.Add(new Sprite(
                    assoc.Key,
                    assoc.Value,
                    int.Parse(match1.Groups[1].Value),
                    int.Parse(match1.Groups[2].Value),
                    int.Parse(match1.Groups[3].Value),
                    int.Parse(match1.Groups[4].Value),
                    match2.Groups[1].Value
                ));
            }
            else
            {
                Console.WriteLine($"No texture found for sprite {assoc.Key}");
            }
        }

        return list;
    }

    public static void ResizeImageTo64(string inputPath, string outputPath, Sprite sprite)
    {
        using var image = Image.Load(inputPath);
        int cropY = image.Height - sprite.PosY - sprite.Height;
        var cropRectangle = new Rectangle(sprite.PosX, cropY, sprite.Width, sprite.Height);
        cropRectangle.Intersect(new Rectangle(0, 0, image.Width, image.Height));

        image.Mutate(x =>
        {
            x.Crop(cropRectangle);
            x.Resize(64, 64);
        });

        image.Save(outputPath, new PngEncoder());
    }
}
