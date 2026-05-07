using ecocraft.Models;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using System.Data;

class DataMigrator
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== EcoCraft data migration SQLite -> PostgreSQL ===");

        var sqlitePath = Environment.GetEnvironmentVariable("SQLITE_PATH") ?? "/data/ecocraft.db";
        var sqliteConnectionString = $"Data Source={sqlitePath};Foreign Keys=True;";
        var postgresConnectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION")
            ?? "Host=db;Port=5432;Database=ecocraft;Username=ecocraft;Password=ecocraft";

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

        // Ajouter les colonnes manquantes au SQLite si la DB est ancienne
        Console.WriteLine("Mise à jour du schéma SQLite (colonnes manquantes)...");
        await PatchSqliteSchema(sqlite);

        Console.WriteLine("Suppression de la base PostgreSQL existante...");
        await pg.Database.EnsureDeletedAsync();

        Console.WriteLine("Applique les migrations sur PostgreSQL...");
        await pg.Database.MigrateAsync();

        // OPTIONNEL : désactiver la détection auto pour un peu de perf
        pg.ChangeTracker.AutoDetectChangesEnabled = false;
        pg.Database.SetCommandTimeout(0);

        // Tuning session PG pour bulk-load (one-shot, on assume une migration en maintenance)
        await pg.Database.ExecuteSqlRawAsync(@"
            SET synchronous_commit = OFF;
            SET work_mem = '256MB';
            SET maintenance_work_mem = '1GB';
            SET temp_buffers = '256MB';
        ");

        // Désactive les triggers FK pour la session => pas de vérif par ligne pendant les COPY.
        // Safe ici car on copie en ordre topologique (parents avant enfants).
        Console.WriteLine("Désactivation des triggers FK pour la session...");
        await pg.Database.ExecuteSqlRawAsync("SET session_replication_role = 'replica';");

        // Capture + drop des index secondaires (on garde les PK et les index qui backent une contrainte)
        Console.WriteLine("Drop des index secondaires (recréés en bulk après les COPYs)...");
        var droppedIndexes = await DropSecondaryIndexes(pg);

        // Ordre important pour respecter les FK
        await CopyTable<Server>(sqlite, pg);             // Parent de quasi tout
        await CopyTable<LocalizedField>(sqlite, pg);     // FK vers Server

        await CopyTable<Skill>(sqlite, pg);              // FK Server
        await CopyTable<Talent>(sqlite, pg);             // FK Skill, LocalizedField
        await CopyTable<PluginModule>(sqlite, pg);       // FK Server, Skill, LocalizedField
        await CopyTable<CraftingTable>(sqlite, pg);      // FK Server, LocalizedField
        await CopyJoinTable("CraftingTablePluginModule", sqlite, pg); // M2M CraftingTable <-> PluginModule

        await CopyTable<DynamicValue>(sqlite, pg);       // FK Server

        await CopyTable<ItemOrTag>(sqlite, pg);          // FK Server, LocalizedField
        await CopyJoinTable("ItemTagAssoc", sqlite, pg);    // M2M ItemOrTag <-> ItemOrTag

        await CopyTable<Recipe>(sqlite, pg);             // FK Skill, CraftingTable, Server, LocalizedField, DynamicValue
        await CopyTable<Element>(sqlite, pg);            // FK Recipe, ItemOrTag, DynamicValue (ItemOrTag copié plus tard)
        await CopyTable<Modifier>(sqlite, pg);           // FK DynamicValue, Skill, Talent

        await CopyTable<User>(sqlite, pg);
        await CopyTable<UserServer>(sqlite, pg);         // FK User, Server

        await CopyTable<DataContext>(sqlite, pg);        // FK UserServer

        await CopyTable<UserSetting>(sqlite, pg);        // FK DataContext
        await CopyTable<UserMargin>(sqlite, pg);         // FK DataContext
        await CopyTable<UserCraftingTable>(sqlite, pg);  // FK DataContext, CraftingTable, PluginModule
        await CopyJoinTable("UserCraftingTablePluginModule", sqlite, pg); // M2M UserCraftingTable <-> PluginModule
        await CopyTable<UserSkill>(sqlite, pg);          // FK DataContext, Skill
        await CopyTable<UserTalent>(sqlite, pg);         // FK DataContext, Talent
        await CopyUserRecipe(sqlite, pg);         // FK DataContext, Recipe, self FK ParentUserRecipe

        await CopyTable<UserElement>(sqlite, pg);        // FK Element, DataContext, UserRecipe

        // Attention ici : FK vers UserMargin, ItemOrTag, DataContext, UserElement, UserPrice (self)
        await CopyUserPrice(sqlite, pg);

        await CopyTable<ModUploadHistory>(sqlite, pg);   // FK User, Server

        // Nettoyage des doublons qui violeraient les contraintes uniques modernes
        // (la prod SQLite peut contenir des données qui n'auraient pas dû passer après les nouvelles migrations).
        Console.WriteLine("Nettoyage des doublons sur les contraintes uniques...");
        await DeduplicateData(pg);

        // Recrée les index en bulk (sort + build B-tree) - bien plus rapide que les updates par ligne
        Console.WriteLine("Recréation des index secondaires en bulk...");
        await RecreateIndexes(pg, droppedIndexes);

        // Réactive les triggers FK
        Console.WriteLine("Réactivation des triggers FK...");
        await pg.Database.ExecuteSqlRawAsync("SET session_replication_role = 'origin';");

        // Met à jour les stats du planner sur la nouvelle data
        Console.WriteLine("ANALYZE de la base...");
        await pg.Database.ExecuteSqlRawAsync("ANALYZE;");

        Console.WriteLine("Migration terminée. Tu peux respirer.");
    }

    private static async Task DeduplicateData(EcoCraftDbContext pg)
    {
        // UserServer : la migration AddUniqueIndexUserServer (avril 2026) a ajouté un unique
        // sur (UserId, ServerId) côté PG, mais les anciennes données SQLite peuvent encore
        // contenir des doublons. On garde le UserServer avec l'Id minimum et on rebranche
        // les DataContext qui pointaient vers les doublons.
        var rerouted = await pg.Database.ExecuteSqlRawAsync(@"
            WITH dups AS (
                SELECT
                    ""Id"",
                    FIRST_VALUE(""Id"") OVER (PARTITION BY ""UserId"", ""ServerId"" ORDER BY ""Id"") AS keep_id,
                    ROW_NUMBER() OVER (PARTITION BY ""UserId"", ""ServerId"" ORDER BY ""Id"") AS rn
                FROM ""UserServer""
            )
            UPDATE ""DataContext"" dc
            SET ""UserServerId"" = d.keep_id
            FROM dups d
            WHERE dc.""UserServerId"" = d.""Id"" AND d.rn > 1;
        ");
        if (rerouted > 0)
            Console.WriteLine($"  {rerouted} DataContext re-routés vers le UserServer canonique.");

        var deleted = await pg.Database.ExecuteSqlRawAsync(@"
            DELETE FROM ""UserServer""
            WHERE ""Id"" IN (
                SELECT ""Id"" FROM (
                    SELECT ""Id"", ROW_NUMBER() OVER (PARTITION BY ""UserId"", ""ServerId"" ORDER BY ""Id"") AS rn
                    FROM ""UserServer""
                ) t WHERE rn > 1
            );
        ");
        if (deleted > 0)
            Console.WriteLine($"  {deleted} UserServer doublons supprimés.");
        else
            Console.WriteLine("  Pas de doublon UserServer.");
    }

    private static async Task<List<string>> DropSecondaryIndexes(EcoCraftDbContext pg)
    {
        // Capture le DDL de tous les index non-PK et qui ne backent pas une contrainte (unique, exclusion).
        // Ceux-ci seront supprimés pendant le bulk-load et recréés à la fin.
        var ddls = await pg.Database.SqlQueryRaw<string>(@"
            SELECT pg_get_indexdef(i.indexrelid) AS ""Value""
            FROM pg_index i
            JOIN pg_class c ON c.oid = i.indexrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public'
              AND NOT i.indisprimary
              AND NOT EXISTS (
                  SELECT 1 FROM pg_constraint con WHERE con.conindid = i.indexrelid
              )
        ").ToListAsync();

        foreach (var ddl in ddls)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                ddl,
                @"INDEX\s+""?([^""\s]+)""?\s+ON",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success) continue;

            var indexName = match.Groups[1].Value;
            await pg.Database.ExecuteSqlRawAsync($"DROP INDEX IF EXISTS \"{indexName}\"");
            Console.WriteLine($"  - drop index {indexName}");
        }

        Console.WriteLine($"  {ddls.Count} index secondaires capturés et droppés.");
        return ddls;
    }

    private static async Task RecreateIndexes(EcoCraftDbContext pg, List<string> ddls)
    {
        foreach (var ddl in ddls)
        {
            await pg.Database.ExecuteSqlRawAsync(ddl);
            Console.WriteLine($"  + {ddl.Substring(0, Math.Min(80, ddl.Length))}...");
        }
        Console.WriteLine($"  {ddls.Count} index recréés.");
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

        Console.WriteLine($"[COPY] {typeof(T).Name} : {total} lignes via Npgsql binary COPY...");

        var idProp = typeof(T).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        IQueryable<T> query = srcSet;
        if (idProp != null)
        {
            query = query.OrderBy(e => EF.Property<object>(e, "Id"));
        }

        var copied = await BulkCopyAsync(pg, query.AsAsyncEnumerable(), total);

        Console.WriteLine($"[DONE] {typeof(T).Name} : {copied} lignes copiées.");
    }

    private static async Task<long> BulkCopyAsync<T>(EcoCraftDbContext pg, IAsyncEnumerable<T> source, long total)
        where T : class
    {
        var entityType = pg.Model.FindEntityType(typeof(T))
            ?? throw new InvalidOperationException($"Entité EF introuvable pour {typeof(T).Name}");

        var tableName = entityType.GetTableName()!;
        var schema = entityType.GetSchema();
        var qualifiedTable = string.IsNullOrEmpty(schema)
            ? $"\"{tableName}\""
            : $"\"{schema}\".\"{tableName}\"";

        // On ne prend que les propriétés mappées en colonne réelle (pas les owned/shadow particuliers)
        var properties = entityType.GetProperties()
            .Where(p => !string.IsNullOrEmpty(p.GetColumnName()))
            .ToList();

        var columnList = string.Join(",", properties.Select(p => $"\"{p.GetColumnName()}\""));
        var copyCommand = $"COPY {qualifiedTable} ({columnList}) FROM STDIN (FORMAT BINARY)";

        // Pré-construit un writer par colonne pour éviter le switch dans la boucle
        var writers = properties.Select(BuildColumnWriter).ToArray();

        var pgConn = (NpgsqlConnection)pg.Database.GetDbConnection();
        if (pgConn.State != ConnectionState.Open)
            await pgConn.OpenAsync();

        long copied = 0;
        await using (var writer = await pgConn.BeginBinaryImportAsync(copyCommand))
        {
            // Le Complete final peut prendre plusieurs minutes sur 1M+ lignes
            writer.Timeout = TimeSpan.FromHours(2);
            await foreach (var entity in source)
            {
                FixDateTimes(entity);
                await writer.StartRowAsync();
                for (var i = 0; i < properties.Count; i++)
                {
                    var value = properties[i].PropertyInfo?.GetValue(entity)
                                ?? properties[i].FieldInfo?.GetValue(entity);
                    await writers[i](writer, value);
                }
                copied++;
                if (copied % 50000 == 0)
                    Console.WriteLine($"    -> {copied}/{total}");
            }
            if (total >= 100000)
                Console.WriteLine($"    -> {copied}/{total} (flush + commit PG en cours, ça peut prendre quelques minutes sur les grosses tables)...");
            await writer.CompleteAsync();
        }

        return copied;
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
        }
        await Task.CompletedTask;
    }

    private static Func<NpgsqlBinaryImporter, object?, ValueTask> BuildColumnWriter(IProperty prop)
    {
        var clrType = Nullable.GetUnderlyingType(prop.ClrType) ?? prop.ClrType;

        if (clrType.IsEnum)
        {
            return async (w, v) =>
            {
                if (v is null) await w.WriteNullAsync();
                else await w.WriteAsync(Convert.ToInt32(v));
            };
        }

        if (clrType == typeof(Guid)) return async (w, v) => { if (v is null) await w.WriteNullAsync(); else await w.WriteAsync((Guid)v); };
        if (clrType == typeof(string)) return async (w, v) => { if (v is null) await w.WriteNullAsync(); else await w.WriteAsync((string)v); };
        if (clrType == typeof(bool)) return async (w, v) => { if (v is null) await w.WriteNullAsync(); else await w.WriteAsync((bool)v); };
        if (clrType == typeof(int)) return async (w, v) => { if (v is null) await w.WriteNullAsync(); else await w.WriteAsync((int)v); };
        if (clrType == typeof(long)) return async (w, v) => { if (v is null) await w.WriteNullAsync(); else await w.WriteAsync((long)v); };
        if (clrType == typeof(short)) return async (w, v) => { if (v is null) await w.WriteNullAsync(); else await w.WriteAsync((short)v); };
        if (clrType == typeof(byte)) return async (w, v) => { if (v is null) await w.WriteNullAsync(); else await w.WriteAsync((byte)v); };
        if (clrType == typeof(decimal)) return async (w, v) => { if (v is null) await w.WriteNullAsync(); else await w.WriteAsync((decimal)v); };
        if (clrType == typeof(double)) return async (w, v) => { if (v is null) await w.WriteNullAsync(); else await w.WriteAsync((double)v); };
        if (clrType == typeof(float)) return async (w, v) => { if (v is null) await w.WriteNullAsync(); else await w.WriteAsync((float)v); };
        if (clrType == typeof(DateTime)) return async (w, v) => { if (v is null) await w.WriteNullAsync(); else await w.WriteAsync((DateTime)v); };
        if (clrType == typeof(DateTimeOffset)) return async (w, v) => { if (v is null) await w.WriteNullAsync(); else await w.WriteAsync((DateTimeOffset)v); };
        if (clrType == typeof(string[])) return async (w, v) => { if (v is null) await w.WriteNullAsync(); else await w.WriteAsync((string[])v); };
        if (clrType == typeof(decimal[])) return async (w, v) => { if (v is null) await w.WriteNullAsync(); else await w.WriteAsync((decimal[])v); };
        if (clrType == typeof(int[])) return async (w, v) => { if (v is null) await w.WriteNullAsync(); else await w.WriteAsync((int[])v); };
        if (clrType == typeof(byte[])) return async (w, v) => { if (v is null) await w.WriteNullAsync(); else await w.WriteAsync((byte[])v); };

        throw new NotSupportedException(
            $"Type CLR non géré pour COPY binaire : {clrType.FullName} (colonne {prop.DeclaringType.ClrType.Name}.{prop.Name})");
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
            .AsNoTracking()
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

            await BulkCopyAsync(pg, ToAsyncEnumerable(batch), batch.Count);

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
            .AsNoTracking()
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

            await BulkCopyAsync(pg, ToAsyncEnumerable(batch), batch.Count);

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

    private static async Task CopyJoinTable(string tableName, EcoCraftDbContext sqlite, EcoCraftDbContext pg)
    {
        var countResult = await sqlite.Database.SqlQueryRaw<int>($"SELECT COUNT(*) AS \"Value\" FROM \"{tableName}\"").FirstAsync();
        if (countResult == 0)
        {
            Console.WriteLine($"[EMPTY] {tableName}");
            return;
        }

        // Lire les colonnes de la table SQLite
        var columns = new List<string>();
        await sqlite.Database.OpenConnectionAsync();
        using (var cmd = sqlite.Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = $"PRAGMA table_info(\"{tableName}\")";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }
        }

        var columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));
        var copyCommand = $"COPY \"{tableName}\" ({columnList}) FROM STDIN (FORMAT BINARY)";

        Console.WriteLine($"[COPY] {tableName} : {countResult} lignes via Npgsql binary COPY...");

        var pgConn = (NpgsqlConnection)pg.Database.GetDbConnection();
        if (pgConn.State != ConnectionState.Open)
            await pgConn.OpenAsync();

        long copied = 0;
        await using (var writer = await pgConn.BeginBinaryImportAsync(copyCommand))
        {
            writer.Timeout = TimeSpan.FromHours(2);

            using var cmd = sqlite.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = $"SELECT {columnList} FROM \"{tableName}\"";
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                await writer.StartRowAsync();
                for (var i = 0; i < columns.Count; i++)
                {
                    if (await reader.IsDBNullAsync(i))
                    {
                        await writer.WriteNullAsync();
                        continue;
                    }
                    var v = reader.GetValue(i);
                    // SQLite stocke les Guid comme TEXT - on parse vers Guid pour uuid PG
                    if (v is string s && Guid.TryParse(s, out var g))
                        await writer.WriteAsync(g);
                    else if (v is long l)
                        await writer.WriteAsync(l);
                    else if (v is int ii)
                        await writer.WriteAsync(ii);
                    else if (v is bool b)
                        await writer.WriteAsync(b);
                    else if (v is double d)
                        await writer.WriteAsync(d);
                    else if (v is decimal dec)
                        await writer.WriteAsync(dec);
                    else if (v is string str)
                        await writer.WriteAsync(str);
                    else
                        throw new NotSupportedException(
                            $"Type SQLite non géré pour {tableName}.{columns[i]}: {v.GetType().FullName}");
                }
                copied++;
                if (copied % 50000 == 0)
                    Console.WriteLine($"    -> {copied}/{countResult}");
            }

            await writer.CompleteAsync();
        }

        Console.WriteLine($"[DONE] {tableName} : {copied} lignes copiées.");
    }

    private static async Task PatchSqliteSchema(EcoCraftDbContext sqlite)
    {
        await sqlite.Database.OpenConnectionAsync();

        foreach (var entityType in sqlite.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (string.IsNullOrEmpty(tableName))
                continue;

            // Vérifier que la table existe en SQLite (sinon on saute - elle sera créée vide côté PG)
            if (!await TableExists(sqlite, tableName))
            {
                Console.WriteLine($"  ! table {tableName} absente de SQLite (normal si nouvelle table)");
                continue;
            }

            var existingColumns = await GetSqliteColumns(sqlite, tableName);

            foreach (var prop in entityType.GetProperties())
            {
                var columnName = prop.GetColumnName();
                if (string.IsNullOrEmpty(columnName) || existingColumns.Contains(columnName))
                    continue;

                var sqliteType = MapClrTypeToSqlite(prop.ClrType);
                var (defaultClause, notNull) = BuildColumnDefault(prop);

                try
                {
                    await sqlite.Database.ExecuteSqlRawAsync(
                        $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {sqliteType}{notNull}{defaultClause}");
                    Console.WriteLine($"  + {tableName}.{columnName} ({sqliteType}) ajouté");
                }
                catch (Microsoft.Data.Sqlite.SqliteException ex)
                {
                    Console.WriteLine($"  ! {tableName}.{columnName} : {ex.Message}");
                }
            }
        }
    }

    private static async Task<bool> TableExists(EcoCraftDbContext sqlite, string tableName)
    {
        using var cmd = sqlite.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$name";
        var p = cmd.CreateParameter();
        p.ParameterName = "$name";
        p.Value = tableName;
        cmd.Parameters.Add(p);
        var result = await cmd.ExecuteScalarAsync();
        return result != null;
    }

    private static async Task<HashSet<string>> GetSqliteColumns(EcoCraftDbContext sqlite, string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = sqlite.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{tableName}\")";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }
        return columns;
    }

    private static string MapClrTypeToSqlite(Type clrType)
    {
        var t = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (t.IsEnum) return "INTEGER";
        if (t == typeof(bool) || t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)) return "INTEGER";
        if (t == typeof(float) || t == typeof(double) || t == typeof(decimal)) return "REAL";
        if (t == typeof(byte[])) return "BLOB";
        // string, Guid, DateTime, DateTimeOffset, TimeSpan, arrays JSON, etc. -> TEXT
        return "TEXT";
    }

    private static (string defaultClause, string notNull) BuildColumnDefault(IProperty prop)
    {
        // SQLite ALTER ADD COLUMN exige une DEFAULT pour les colonnes NOT NULL.
        // Le modèle EF actuel impose la nullabilité ; on respecte ça mais on tolère
        // l'ajout même quand la colonne est déclarée NOT NULL côté EF, en fournissant
        // un DEFAULT compatible avec le type.
        if (prop.IsNullable)
            return ("", "");

        var t = Nullable.GetUnderlyingType(prop.ClrType) ?? prop.ClrType;
        string defaultLiteral;
        if (t.IsEnum) defaultLiteral = "0";
        else if (t == typeof(bool)) defaultLiteral = "0";
        else if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)) defaultLiteral = "0";
        else if (t == typeof(float) || t == typeof(double) || t == typeof(decimal)) defaultLiteral = "0";
        else if (t == typeof(Guid)) defaultLiteral = "'00000000-0000-0000-0000-000000000000'";
        else if (t == typeof(DateTime) || t == typeof(DateTimeOffset)) defaultLiteral = "'1970-01-01T00:00:00Z'";
        else defaultLiteral = "''";

        return ($" DEFAULT {defaultLiteral}", " NOT NULL");
    }

}
