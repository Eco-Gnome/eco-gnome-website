using ecocraft.Models;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

class DataMigrator
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== EcoCraft data migration SQLite -> PostgreSQL ===");

        // À ADAPTER selon ton contexte
        var sqlitePath = @"C:\Users\thiba\Documents\Repositories\eco-calculator-website\DataMigrator\ecocraft.db";
        var sqliteConnectionString = $"Data Source={sqlitePath};Foreign Keys=True;"; // ton fichier SQLite
        var postgresConnectionString =
            "Host=localhost;Port=5432;Database=ecocraft;Username=ecocraft;Password=ecocraft";

        // Contexte SQLite
        var sqliteOptions = new DbContextOptionsBuilder<EcoCraftDbContext>()
            .UseSqlite(sqliteConnectionString)
            .Options;

        // Contexte PostgreSQL
        var postgresOptions = new DbContextOptionsBuilder<EcoCraftDbContext>()
            .UseNpgsql(postgresConnectionString)
            .Options;

        using var sqlite = new EcoCraftDbContext(sqliteOptions);
        using var pg = new EcoCraftDbContext(postgresOptions);

        Console.WriteLine("Applique les migrations sur PostgreSQL...");
        await pg.Database.MigrateAsync();

        // OPTIONNEL : désactiver la détection auto pour un peu de perf
        pg.ChangeTracker.AutoDetectChangesEnabled = false;
        pg.Database.SetCommandTimeout(0);

        // Ordre important pour respecter les FK
        await CopyTable<Server>(sqlite, pg);             // Parent de quasi tout
        await CopyTable<LocalizedField>(sqlite, pg);     // FK vers Server

        await CopyTable<Skill>(sqlite, pg);              // FK Server
        await CopyTable<Talent>(sqlite, pg);             // FK Skill, LocalizedField
        await CopyTable<PluginModule>(sqlite, pg);       // FK Server, Skill, LocalizedField
        await CopyTable<CraftingTable>(sqlite, pg);      // FK Server, LocalizedField

        await CopyTable<DynamicValue>(sqlite, pg);       // FK Server

        await CopyTable<ItemOrTag>(sqlite, pg);          // FK Server, LocalizedField (assoc M2M en table join auto)

        await CopyTable<Recipe>(sqlite, pg);             // FK Skill, CraftingTable, Server, LocalizedField, DynamicValue
        await CopyTable<Element>(sqlite, pg);            // FK Recipe, ItemOrTag, DynamicValue (ItemOrTag copié plus tard)
        await CopyTable<Modifier>(sqlite, pg);           // FK DynamicValue, Skill, Talent

        await CopyTable<User>(sqlite, pg);
        await CopyTable<UserServer>(sqlite, pg);         // FK User, Server

        await CopyTable<DataContext>(sqlite, pg);        // FK UserServer

        await CopyTable<UserSetting>(sqlite, pg);        // FK DataContext
        await CopyTable<UserMargin>(sqlite, pg);         // FK DataContext
        await CopyTable<UserCraftingTable>(sqlite, pg);  // FK DataContext, CraftingTable, PluginModule
        await CopyTable<UserSkill>(sqlite, pg);          // FK DataContext, Skill
        await CopyTable<UserTalent>(sqlite, pg);         // FK DataContext, Talent
        await CopyUserRecipe(sqlite, pg);         // FK DataContext, Recipe, self FK ParentUserRecipe

        await CopyTable<UserElement>(sqlite, pg);        // FK Element, DataContext, UserRecipe

        // Attention ici : FK vers UserMargin, ItemOrTag, DataContext, UserElement, UserPrice (self)
        await CopyUserPrice(sqlite, pg);

        await CopyTable<ModUploadHistory>(sqlite, pg);   // FK User, Server

        Console.WriteLine("Migration terminée. Tu peux respirer.");
    }

    private static async Task CopyTable<T>(EcoCraftDbContext sqlite, EcoCraftDbContext pg)
    where T : class
    {
        var srcSet = sqlite.Set<T>().AsNoTracking();
        var dstSet = pg.Set<T>();

        if (await dstSet.AnyAsync())
        {
            Console.WriteLine($"[SKIP] {typeof(T).Name} (déjà des données en PostgreSQL)");
            return;
        }

        var total = await srcSet.CountAsync();
        if (total == 0)
        {
            Console.WriteLine($"[EMPTY] {typeof(T).Name}");
            return;
        }

        Console.WriteLine($"[COPY] {typeof(T).Name} : {total} lignes...");

        const int batchSize = 5000;
        var batch = new List<T>(batchSize);
        var copied = 0;

        // Optionnel : si la classe a une propriété Id, on ordonne dessus
        var idProp = typeof(T).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        IQueryable<T> query = srcSet;
        if (idProp != null)
        {
            query = query.OrderBy(e => EF.Property<object>(e, "Id"));
        }

        await foreach (var entity in query.AsAsyncEnumerable())
        {
            FixDateTimes(entity);
            batch.Add(entity);

            if (batch.Count >= batchSize)
            {
                await dstSet.AddRangeAsync(batch);
                await pg.SaveChangesAsync();
                pg.ChangeTracker.Clear();

                copied += batch.Count;
                Console.WriteLine($"    -> {copied}/{total}");
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await dstSet.AddRangeAsync(batch);
            await pg.SaveChangesAsync();
            pg.ChangeTracker.Clear();

            copied += batch.Count;
            Console.WriteLine($"    -> {copied}/{total}");
        }

        Console.WriteLine($"[DONE] {typeof(T).Name} : {copied} lignes copiées.");
    }

    private static void FixDateTimes(object entity)
    {
        var type = entity.GetType();
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in props)
        {
            if (prop.PropertyType == typeof(DateTime))
            {
                var value = (DateTime)prop.GetValue(entity)!;
                if (value.Kind == DateTimeKind.Unspecified)
                {
                    prop.SetValue(entity, DateTime.SpecifyKind(value, DateTimeKind.Utc));
                }
            }
            else if (prop.PropertyType == typeof(DateTime?))
            {
                var value = (DateTime?)prop.GetValue(entity);
                if (value.HasValue && value.Value.Kind == DateTimeKind.Unspecified)
                {
                    prop.SetValue(entity, (DateTime?)DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
                }
            }
            else if (prop.PropertyType == typeof(DateTimeOffset))
            {
                var value = (DateTimeOffset)prop.GetValue(entity)!;
                if (value.Offset != TimeSpan.Zero)
                {
                    prop.SetValue(entity, value.ToUniversalTime());
                }
            }
            else if (prop.PropertyType == typeof(DateTimeOffset?))
            {
                var value = (DateTimeOffset?)prop.GetValue(entity);
                if (value.HasValue && value.Value.Offset != TimeSpan.Zero)
                {
                    prop.SetValue(entity, (DateTimeOffset?)value.Value.ToUniversalTime());
                }
            }
        }
    }

    private static async Task CopyUserRecipe(EcoCraftDbContext sqlite, EcoCraftDbContext pg)
    {
        var dstSet = pg.Set<UserRecipe>();

        if (await dstSet.AnyAsync())
        {
            Console.WriteLine("[SKIP] UserRecipe (déjà des données en PostgreSQL)");
            return;
        }

        Console.WriteLine("Chargement des UserRecipe depuis SQLite...");
        var all = await sqlite.UserRecipes
            .ToListAsync();

        if (all.Count == 0)
        {
            Console.WriteLine("[EMPTY] UserRecipe");
            return;
        }

        Console.WriteLine($"[COPY] UserRecipe : {all.Count} lignes...");

        // Dictionnaire des recettes restantes à insérer
        var remaining = all.ToDictionary(ur => ur.Id, ur => ur);
        var insertedIds = new HashSet<Guid>();

        const int batchSize = 5000;
        var total = remaining.Count;
        var insertedTotal = 0;

        while (remaining.Count > 0)
        {
            // On prend un batch où le parent est déjà inséré ou null
            var batch = remaining.Values
                .Where(ur => ur.ParentUserRecipeId == null
                             || insertedIds.Contains(ur.ParentUserRecipeId.Value))
                .Take(batchSize)
                .ToList();

            if (batch.Count == 0)
            {
                Console.WriteLine("Aucun UserRecipe insérable trouvé. Boucle ou données cassées ?");
                Console.WriteLine($"Il reste {remaining.Count} UserRecipe non insérés.");
                throw new InvalidOperationException("Impossible de résoudre les dépendances ParentUserRecipeId.");
            }

            foreach (var entity in batch)
            {
                FixDateTimes(entity);
            }

            await dstSet.AddRangeAsync(batch);
            await pg.SaveChangesAsync();
            pg.ChangeTracker.Clear();

            foreach (var ur in batch)
            {
                insertedIds.Add(ur.Id);
                remaining.Remove(ur.Id);
            }

            insertedTotal += batch.Count;
            Console.WriteLine($"    -> {insertedTotal}/{total}");
        }

        Console.WriteLine($"[DONE] UserRecipe : {insertedTotal} lignes copiées.");
    }

    private static async Task CopyUserPrice(EcoCraftDbContext sqlite, EcoCraftDbContext pg)
    {
        var dstSet = pg.Set<UserPrice>();

        if (await dstSet.AnyAsync())
        {
            Console.WriteLine("[SKIP] UserPrice (déjà des données en PostgreSQL)");
            return;
        }

        Console.WriteLine("Chargement des UserPrice depuis SQLite...");
        var all = await sqlite.UserPrices
            .ToListAsync();

        if (all.Count == 0)
        {
            Console.WriteLine("[EMPTY] UserPrice");
            return;
        }

        Console.WriteLine($"[COPY] UserPrice : {all.Count} lignes...");

        // Dictionnaire des prix restants à insérer
        var remaining = all.ToDictionary(up => up.Id, up => up);
        var insertedIds = new HashSet<Guid>();

        const int batchSize = 5000;
        var total = remaining.Count;
        var insertedTotal = 0;

        while (remaining.Count > 0)
        {
            // On prend un batch où la self-FK est résolue
            var batch = remaining.Values
                .Where(up => up.PrimaryUserPriceId == null
                             || insertedIds.Contains(up.PrimaryUserPriceId.Value))
                .Take(batchSize)
                .ToList();

            if (batch.Count == 0)
            {
                Console.WriteLine("Aucun UserPrice insérable trouvé. Boucle ou données cassées ?");
                Console.WriteLine($"Il reste {remaining.Count} UserPrice non insérés.");
                throw new InvalidOperationException("Impossible de résoudre les dépendances PrimaryUserPriceId.");
            }

            foreach (var entity in batch)
            {
                FixDateTimes(entity);
            }

            await dstSet.AddRangeAsync(batch);
            await pg.SaveChangesAsync();
            pg.ChangeTracker.Clear();

            foreach (var up in batch)
            {
                insertedIds.Add(up.Id);
                remaining.Remove(up.Id);
            }

            insertedTotal += batch.Count;
            Console.WriteLine($"    -> {insertedTotal}/{total}");
        }

        Console.WriteLine($"[DONE] UserPrice : {insertedTotal} lignes copiées.");
    }

}
