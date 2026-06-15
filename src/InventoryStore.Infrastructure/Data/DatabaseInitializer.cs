using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventoryStore.Infrastructure.Data;

public class DatabaseInitializer
{
    private readonly AppDbContext _context;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(AppDbContext context, ILogger<DatabaseInitializer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _context.Database.EnsureCreatedAsync();
            await ApplySchemaUpgradesAsync();
            _logger.LogInformation("Database initialized.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database.");
            throw;
        }
    }

    private async Task ApplySchemaUpgradesAsync()
    {
        var conn = (SqliteConnection)_context.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        // Add new columns first so they exist before the table-rebuild step
        await AddColumnIfMissingAsync(conn, "Users", "FirstName", "TEXT NULL");
        await AddColumnIfMissingAsync(conn, "Users", "LastName",  "TEXT NULL");

        // SQLite doesn't support ALTER COLUMN — rebuild Users to make Email nullable
        await MakeUsersEmailNullableAsync(conn);

        await AddColumnIfMissingAsync(conn, "InventoryItems", "ItemType",        "INTEGER NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync(conn, "InventoryItems", "CheckedOutCount", "INTEGER NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync(conn, "InventoryItems", "LostCount",       "INTEGER NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync(conn, "InventoryItems", "ScanWarning",     "TEXT NULL");

        await EnsureCheckoutRecordsTableAsync(conn);
        await EnsureCategoriesTableAsync(conn);
        await EnsureTagsTablesAsync(conn);
        await AddColumnIfMissingAsync(conn, "InventoryItems", "CategoryId",   "INTEGER NULL");
        await AddColumnIfMissingAsync(conn, "InventoryItems", "ExpiryDate",   "TEXT NULL");
        await AddColumnIfMissingAsync(conn, "InventoryItems", "IsPublic",     "INTEGER NOT NULL DEFAULT 0");
        await EnsureClientsTableAsync(conn);
        await AddColumnIfMissingAsync(conn, "CheckoutRecords", "ClientId",           "INTEGER NULL");
        await AddColumnIfMissingAsync(conn, "Clients",         "Email",              "TEXT NULL");
        await AddColumnIfMissingAsync(conn, "InventoryItems",  "IsMetadataMatched",  "INTEGER NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync(conn, "InventoryItems",  "MetadataSource",     "TEXT NULL");
        await AddColumnIfMissingAsync(conn, "InventoryItems",  "SelectedMetadataId", "INTEGER NULL");
        await EnsureProductMetadataTableAsync(conn);
        await AddColumnIfMissingAsync(conn, "ProductMetadata", "Size",   "TEXT NULL");
        await AddColumnIfMissingAsync(conn, "ProductMetadata", "Weight", "TEXT NULL");
        await EnsureSafetyDataSheetsTableAsync(conn);
        await EnsureItemCostsTableAsync(conn);
        await EnsureStockMovementsTableAsync(conn);
        await EnsureWebhookEndpointsTableAsync(conn);

        // Kit ("bundle") support: a per-kit AllowPartial flag, the member + checkout-group tables,
        // and a link column so a checkout record can belong to a kit checkout.
        await AddColumnIfMissingAsync(conn, "InventoryItems",  "AllowPartial",  "INTEGER NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync(conn, "CheckoutRecords", "KitCheckoutId", "INTEGER NULL");
        await EnsureKitComponentsTableAsync(conn);
        await EnsureKitCheckoutsTableAsync(conn);

        // Maintenance module: vendors, the per-item schedule, the out-for-maintenance visits, and an
        // availability column that maintenance draws down the same way checkouts do.
        await AddColumnIfMissingAsync(conn, "InventoryItems", "OutForMaintenanceCount", "INTEGER NOT NULL DEFAULT 0");
        await EnsureVendorsTableAsync(conn);
        await EnsureMaintenanceSchedulesTableAsync(conn);
        await EnsureMaintenanceVisitsTableAsync(conn);

        await BackfillActivityLogUsernamesAsync(conn);
        await CleanupOrphanedCheckoutRecordsAsync(conn);
    }

    // One-time cleanup: older builds deleted an inventory item without removing its checkout records,
    // leaving rows that point at an item that no longer exists. Those show as "Unknown" in the
    // Checked Out / Lost reports and cannot be acted on, so remove them. Guarded by a flag.
    private static async Task CleanupOrphanedCheckoutRecordsAsync(SqliteConnection conn)
    {
        const string flagKey = "migration.checkoutrecords.orphans";

        await using (var check = conn.CreateCommand())
        {
            check.CommandText = "SELECT Value FROM AppSettings WHERE Key = $k";
            check.Parameters.AddWithValue("$k", flagKey);
            if (await check.ExecuteScalarAsync() is string v && v == "done") return;
        }

        await using (var delete = conn.CreateCommand())
        {
            delete.CommandText = @"
                DELETE FROM CheckoutRecords
                WHERE InventoryItemId NOT IN (SELECT Id FROM InventoryItems);";
            await delete.ExecuteNonQueryAsync();
        }

        await using (var flag = conn.CreateCommand())
        {
            flag.CommandText = "INSERT INTO AppSettings (Key, Value, UpdatedAt) VALUES ($k, 'done', $t)";
            flag.Parameters.AddWithValue("$k", flagKey);
            flag.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
            await flag.ExecuteNonQueryAsync();
        }
    }

    // One-time correction: older activity-log rows stored a user's display name (first/last) instead
    // of their login name. Each row carries a UserId, so set Username to the matching user's login
    // name. Guarded by a flag so it runs once and never rewrites history again.
    private static async Task BackfillActivityLogUsernamesAsync(SqliteConnection conn)
    {
        const string flagKey = "migration.activitylog.usernames";

        await using (var check = conn.CreateCommand())
        {
            check.CommandText = "SELECT Value FROM AppSettings WHERE Key = $k";
            check.Parameters.AddWithValue("$k", flagKey);
            if (await check.ExecuteScalarAsync() is string v && v == "done") return;
        }

        await using (var update = conn.CreateCommand())
        {
            update.CommandText = @"
                UPDATE ActivityLogs
                SET Username = (SELECT u.Username FROM Users u WHERE u.Id = ActivityLogs.UserId)
                WHERE UserId IS NOT NULL
                  AND EXISTS (SELECT 1 FROM Users u WHERE u.Id = ActivityLogs.UserId);";
            await update.ExecuteNonQueryAsync();
        }

        await using (var flag = conn.CreateCommand())
        {
            flag.CommandText = "INSERT INTO AppSettings (Key, Value, UpdatedAt) VALUES ($k, 'done', $t)";
            flag.Parameters.AddWithValue("$k", flagKey);
            flag.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
            await flag.ExecuteNonQueryAsync();
        }
    }

    private static async Task MakeUsersEmailNullableAsync(SqliteConnection conn)
    {
        // Check whether the Email column is currently declared NOT NULL
        var isNotNull = false;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(Users)";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (reader.GetString(1).Equals("Email", StringComparison.OrdinalIgnoreCase)
                    && reader.GetInt32(3) == 1) // notnull = 1
                {
                    isNotNull = true;
                    break;
                }
            }
        }

        if (!isNotNull) return; // Already nullable — nothing to do

        // Rebuild the table with Email NULL.
        // At this point FirstName / LastName columns already exist (added above).
        foreach (var sql in new[]
        {
            "PRAGMA foreign_keys = OFF",

            @"CREATE TABLE Users_v2 (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                Username     TEXT    NOT NULL,
                FirstName    TEXT    NULL,
                LastName     TEXT    NULL,
                Email        TEXT    NULL,
                PasswordHash TEXT    NOT NULL,
                Role         INTEGER NOT NULL,
                IsActive     INTEGER NOT NULL DEFAULT 1,
                CreatedAt    TEXT    NOT NULL,
                LastLoginAt  TEXT    NULL
            )",

            @"INSERT INTO Users_v2
                (Id, Username, FirstName, LastName, Email, PasswordHash, Role, IsActive, CreatedAt, LastLoginAt)
              SELECT
                Id, Username, FirstName, LastName, Email, PasswordHash, Role, IsActive, CreatedAt, LastLoginAt
              FROM Users",

            "DROP TABLE Users",
            "ALTER TABLE Users_v2 RENAME TO Users",
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_Username ON Users (Username)",
            "PRAGMA foreign_keys = ON"
        })
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static readonly HashSet<string> _allowedTables = new(StringComparer.OrdinalIgnoreCase)
        { "Users", "InventoryItems", "CheckoutRecords", "Clients", "ProductMetadata", "SafetyDataSheets",
          "ItemCosts", "StockMovements", "WebhookEndpoints", "KitComponents", "KitCheckouts",
          "Vendors", "MaintenanceSchedules", "MaintenanceVisits" };

    private static readonly HashSet<string> _allowedColumns = new(StringComparer.OrdinalIgnoreCase)
        { "FirstName", "LastName", "ItemType", "CheckedOutCount", "LostCount", "ScanWarning", "CategoryId", "ExpiryDate", "IsPublic", "ClientId", "Email", "IsMetadataMatched", "MetadataSource", "SelectedMetadataId", "Size", "Weight", "AllowPartial", "KitCheckoutId", "OutForMaintenanceCount" };

    private static async Task AddColumnIfMissingAsync(
        SqliteConnection conn, string table, string column, string definition)
    {
        if (!_allowedTables.Contains(table))
            throw new ArgumentException($"Table '{table}' is not permitted for schema upgrades.", nameof(table));
        if (!_allowedColumns.Contains(column))
            throw new ArgumentException($"Column '{column}' is not permitted for schema upgrades.", nameof(column));

        var exists = false;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"PRAGMA table_info({table})";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (!exists)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task EnsureCheckoutRecordsTableAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS CheckoutRecords (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                InventoryItemId INTEGER NOT NULL,
                CheckedOutBy    TEXT    NOT NULL,
                Quantity        INTEGER NOT NULL DEFAULT 1,
                CheckedOutAt    TEXT    NOT NULL,
                CheckedInAt     TEXT    NULL,
                IsLost          INTEGER NOT NULL DEFAULT 0,
                Notes           TEXT    NULL
            );
            CREATE INDEX IF NOT EXISTS IX_CheckoutRecords_InventoryItemId
                ON CheckoutRecords (InventoryItemId);";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task EnsureClientsTableAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Clients (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                FirstName   TEXT    NOT NULL,
                LastName    TEXT    NULL,
                Phone       TEXT    NULL,
                DateOfBirth TEXT    NULL,
                Address     TEXT    NULL,
                Notes       TEXT    NULL,
                CreatedAt   TEXT    NOT NULL,
                UpdatedAt   TEXT    NOT NULL
            );";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task EnsureProductMetadataTableAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS ProductMetadata (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Barcode     TEXT    NOT NULL,
                Source      TEXT    NOT NULL,
                Name        TEXT    NOT NULL,
                Description TEXT    NULL,
                ImageUrl    TEXT    NULL,
                Brand       TEXT    NULL,
                Category    TEXT    NULL,
                Size        TEXT    NULL,
                Weight      TEXT    NULL,
                FetchedAt   TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_ProductMetadata_Barcode ON ProductMetadata (Barcode);";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task EnsureSafetyDataSheetsTableAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS SafetyDataSheets (
                Id                      INTEGER PRIMARY KEY AUTOINCREMENT,
                InventoryItemId         INTEGER NOT NULL,
                Source                  TEXT    NOT NULL,
                ChemicalName            TEXT    NOT NULL,
                Cid                     TEXT    NULL,
                CasNumber               TEXT    NULL,
                SignalWord              TEXT    NULL,
                Pictograms              TEXT    NULL,
                HazardStatements        TEXT    NULL,
                PrecautionaryStatements TEXT    NULL,
                SdsUrl                  TEXT    NULL,
                FetchedAt               TEXT    NOT NULL,
                FOREIGN KEY (InventoryItemId) REFERENCES InventoryItems (Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_SafetyDataSheets_InventoryItemId
                ON SafetyDataSheets (InventoryItemId);";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task EnsureItemCostsTableAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS ItemCosts (
                Id               INTEGER PRIMARY KEY AUTOINCREMENT,
                InventoryItemId  INTEGER NOT NULL,
                UnitCost         TEXT    NOT NULL,
                PurchaseDate     TEXT    NULL,
                UsefulLifeMonths INTEGER NULL,
                UpdatedAt        TEXT    NOT NULL,
                FOREIGN KEY (InventoryItemId) REFERENCES InventoryItems (Id) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_ItemCosts_InventoryItemId
                ON ItemCosts (InventoryItemId);";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task EnsureStockMovementsTableAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS StockMovements (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                InventoryItemId INTEGER NOT NULL,
                ChangeType      TEXT    NOT NULL,
                Quantity        INTEGER NOT NULL,
                Timestamp       TEXT    NOT NULL,
                UserId          INTEGER NULL,
                Username        TEXT    NULL,
                Notes           TEXT    NULL,
                FOREIGN KEY (InventoryItemId) REFERENCES InventoryItems (Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_StockMovements_InventoryItemId ON StockMovements (InventoryItemId);
            CREATE INDEX IF NOT EXISTS IX_StockMovements_Timestamp ON StockMovements (Timestamp);";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task EnsureWebhookEndpointsTableAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS WebhookEndpoints (
                Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                Url        TEXT    NOT NULL,
                Events     TEXT    NOT NULL,
                Secret     TEXT    NULL,
                Enabled    INTEGER NOT NULL DEFAULT 1,
                CreatedAt  TEXT    NOT NULL,
                LastStatus TEXT    NULL,
                LastSentAt TEXT    NULL
            );";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task EnsureKitComponentsTableAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS KitComponents (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                KitItemId       INTEGER NOT NULL,
                ComponentItemId INTEGER NOT NULL,
                Quantity        INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (KitItemId)       REFERENCES InventoryItems (Id) ON DELETE CASCADE,
                FOREIGN KEY (ComponentItemId) REFERENCES InventoryItems (Id) ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS IX_KitComponents_KitItemId       ON KitComponents (KitItemId);
            CREATE INDEX IF NOT EXISTS IX_KitComponents_ComponentItemId ON KitComponents (ComponentItemId);";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task EnsureKitCheckoutsTableAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS KitCheckouts (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                KitItemId    INTEGER NOT NULL,
                CheckedOutBy TEXT    NOT NULL,
                Quantity     INTEGER NOT NULL DEFAULT 1,
                CheckedOutAt TEXT    NOT NULL,
                CheckedInAt  TEXT    NULL,
                IsLost       INTEGER NOT NULL DEFAULT 0,
                Notes        TEXT    NULL,
                ClientId     INTEGER NULL
            );
            CREATE INDEX IF NOT EXISTS IX_KitCheckouts_KitItemId ON KitCheckouts (KitItemId);";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task EnsureVendorsTableAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Vendors (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                Name      TEXT    NOT NULL,
                Phone     TEXT    NULL,
                Email     TEXT    NULL,
                Address   TEXT    NULL,
                Notes     TEXT    NULL,
                CreatedAt TEXT    NOT NULL,
                UpdatedAt TEXT    NOT NULL
            );";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task EnsureMaintenanceSchedulesTableAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS MaintenanceSchedules (
                Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
                InventoryItemId    INTEGER NOT NULL,
                LastMaintainedDate TEXT    NULL,
                IntervalValue      INTEGER NOT NULL DEFAULT 0,
                IntervalUnit       INTEGER NOT NULL DEFAULT 1,
                Notes              TEXT    NULL,
                UpdatedAt          TEXT    NOT NULL,
                FOREIGN KEY (InventoryItemId) REFERENCES InventoryItems (Id) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_MaintenanceSchedules_InventoryItemId
                ON MaintenanceSchedules (InventoryItemId);";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task EnsureMaintenanceVisitsTableAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS MaintenanceVisits (
                Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                InventoryItemId     INTEGER NOT NULL,
                Quantity            INTEGER NOT NULL DEFAULT 1,
                VendorId            INTEGER NULL,
                OutForMaintenanceAt TEXT    NOT NULL,
                ReturnedAt          TEXT    NULL,
                Notes               TEXT    NULL,
                FOREIGN KEY (InventoryItemId) REFERENCES InventoryItems (Id) ON DELETE CASCADE,
                FOREIGN KEY (VendorId)        REFERENCES Vendors (Id)        ON DELETE SET NULL
            );
            CREATE INDEX IF NOT EXISTS IX_MaintenanceVisits_InventoryItemId
                ON MaintenanceVisits (InventoryItemId);";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task EnsureTagsTablesAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Tags (
                Id   INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT    NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Tags_Name ON Tags (Name);

            CREATE TABLE IF NOT EXISTS InventoryItemTags (
                InventoryItemId INTEGER NOT NULL,
                TagId           INTEGER NOT NULL,
                PRIMARY KEY (InventoryItemId, TagId),
                FOREIGN KEY (InventoryItemId) REFERENCES InventoryItems (Id) ON DELETE CASCADE,
                FOREIGN KEY (TagId)           REFERENCES Tags (Id)           ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_InventoryItemTags_TagId ON InventoryItemTags (TagId);";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task EnsureCategoriesTableAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Categories (
                Id    INTEGER PRIMARY KEY AUTOINCREMENT,
                Name  TEXT    NOT NULL,
                Color TEXT    NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Categories_Name ON Categories (Name);";
        await cmd.ExecuteNonQueryAsync();
    }
}
