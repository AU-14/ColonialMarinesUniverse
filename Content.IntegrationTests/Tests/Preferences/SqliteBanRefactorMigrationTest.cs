using System.Collections.Generic;
using System.Linq;
using Content.Server.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Content.IntegrationTests.Tests.Preferences;

[TestFixture]
public sealed class SqliteBanRefactorMigrationTest
{
    private const string LegacyMigration = "20251003024036_RMCChatBans";
    private const string BanRefactorMigration = "20260120200455_BanRefactor";

    [Test]
    public async Task MigratesLegacyGameAndRoleBansWithoutLosingRelationships()
    {
        await using var connection = await OpenConnection();
        await using var context = CreateContext(connection);
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(LegacyMigration);
        await ExecuteNonQuery(connection, SeedMixedLegacyBans);
        await migrator.MigrateAsync();

        var mergedRoleBanId = await ExecuteScalarInt(connection, """
            SELECT ban_id
            FROM ban_role
            WHERE role_type = 'Job' AND role_id = 'Captain';
            """);
        var secondMergedRoleBanId = await ExecuteScalarInt(connection, """
            SELECT ban_id
            FROM ban_role
            WHERE role_type = 'Antag' AND role_id = 'Revolutionary';
            """);
        var separateRoleBanId = await ExecuteScalarInt(connection, """
            SELECT ban_id
            FROM ban_role
            WHERE role_type = 'Job' AND role_id = 'ChiefEngineer';
            """);
        var roleSelectors = await QueryStrings(connection, """
            SELECT role_type || ':' || role_id
            FROM ban_role
            WHERE ban_id = 30
            ORDER BY role_type, role_id;
            """);
        var oldTables = await QueryStrings(connection, """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table'
              AND name IN ('server_ban', 'server_unban', 'server_role_ban', 'server_role_unban')
            ORDER BY name;
            """);
        var migrationHistory = await QueryStrings(connection, """
            SELECT MigrationId
            FROM __EFMigrationsHistory
            ORDER BY MigrationId;
            """);
        var pendingMigrations = (await context.Database.GetPendingMigrationsAsync()).ToArray();
        var expectedMigrations = context.Database.GetMigrations().ToArray();
        var foreignKeyViolations = await CountRows(connection, "PRAGMA foreign_key_check;");
        var banCount = await ExecuteScalarInt(connection, "SELECT COUNT(*) FROM ban;");
        var copiedGameBanCount = await ExecuteScalarInt(connection, """
            SELECT COUNT(*)
            FROM ban
            WHERE ban_id = 10
              AND type = 0
              AND playtime_at_note = '01:02:03'
              AND expiration_time = '2026-02-01 01:00:00'
              AND reason = 'legacy-game-ban'
              AND severity = 2
              AND exempt_flags = 3
              AND auto_delete = TRUE
              AND hidden = TRUE;
            """);
        var roleBanCount = await ExecuteScalarInt(connection, "SELECT COUNT(*) FROM ban WHERE type = 1;");
        var playerSelectorCount = await ExecuteScalarInt(connection, "SELECT COUNT(*) FROM ban_player;");
        var addressSelectorCount = await ExecuteScalarInt(connection, "SELECT COUNT(*) FROM ban_address;");
        var hwidSelectorCount = await ExecuteScalarInt(connection, "SELECT COUNT(*) FROM ban_hwid;");
        var roundSelectorCount = await ExecuteScalarInt(connection, "SELECT COUNT(*) FROM ban_round;");
        var roleSelectorCount = await ExecuteScalarInt(connection, "SELECT COUNT(*) FROM ban_role;");
        var unbanCount = await ExecuteScalarInt(connection, "SELECT COUNT(*) FROM unban;");
        var gameUnbanCount = await ExecuteScalarInt(connection, "SELECT COUNT(*) FROM unban WHERE ban_id = 10;");
        var mergedRoleUnbanCount = await ExecuteScalarInt(
            connection,
            $"SELECT COUNT(*) FROM unban WHERE ban_id = {mergedRoleBanId};");
        var separateRoleUnbanCount = await ExecuteScalarInt(
            connection,
            $"SELECT COUNT(*) FROM unban WHERE ban_id = {separateRoleBanId};");
        var gameAddressCount = await ExecuteScalarInt(connection, """
            SELECT COUNT(*)
            FROM ban_address
            WHERE address = '198.51.100.10/32' AND ban_id = 10;
            """);
        var mergedRoleAddressCount = await ExecuteScalarInt(
            connection,
            $"SELECT COUNT(*) FROM ban_address WHERE address = '203.0.113.20/32' AND ban_id = {mergedRoleBanId};");
        var gameHwidCount = await ExecuteScalarInt(connection, """
            SELECT COUNT(*)
            FROM ban_hwid
            WHERE hex(hwid) = '01020304' AND hwid_type = 1 AND ban_id = 10;
            """);
        var separateRoleHwidCount = await ExecuteScalarInt(
            connection,
            $"SELECT COUNT(*) FROM ban_hwid WHERE hex(hwid) = 'AABBCCDD' AND hwid_type = 2 AND ban_id = {separateRoleBanId};");
        var serverBanHitForeignKeyCount = await ExecuteScalarInt(connection, """
            SELECT COUNT(*)
            FROM pragma_foreign_key_list('server_ban_hit')
            WHERE "table" = 'ban' AND "from" = 'ban_id' AND "to" = 'ban_id';
            """);

        Assert.Multiple(() =>
        {
            Assert.That(banCount, Is.EqualTo(3));
            Assert.That(copiedGameBanCount, Is.EqualTo(1));
            Assert.That(roleBanCount, Is.EqualTo(2));
            Assert.That(mergedRoleBanId, Is.EqualTo(30));
            Assert.That(secondMergedRoleBanId, Is.EqualTo(mergedRoleBanId));
            Assert.That(separateRoleBanId, Is.EqualTo(32));
            Assert.That(separateRoleBanId, Is.Not.EqualTo(mergedRoleBanId));
            Assert.That(roleSelectors, Is.EqualTo(new[] { "Antag:Revolutionary", "Job:Captain" }));

            Assert.That(playerSelectorCount, Is.EqualTo(3));
            Assert.That(addressSelectorCount, Is.EqualTo(3));
            Assert.That(hwidSelectorCount, Is.EqualTo(3));
            Assert.That(roundSelectorCount, Is.EqualTo(3));
            Assert.That(roleSelectorCount, Is.EqualTo(3));
            Assert.That(unbanCount, Is.EqualTo(2));
            Assert.That(gameUnbanCount, Is.EqualTo(1));
            Assert.That(mergedRoleUnbanCount, Is.EqualTo(1));
            Assert.That(separateRoleUnbanCount, Is.Zero);

            Assert.That(gameAddressCount, Is.EqualTo(1));
            Assert.That(mergedRoleAddressCount, Is.EqualTo(1));
            Assert.That(gameHwidCount, Is.EqualTo(1));
            Assert.That(separateRoleHwidCount, Is.EqualTo(1));

            Assert.That(foreignKeyViolations, Is.Zero);
            Assert.That(serverBanHitForeignKeyCount, Is.EqualTo(1));
            Assert.That(oldTables, Is.Empty);
            Assert.That(migrationHistory, Does.Contain(LegacyMigration));
            Assert.That(migrationHistory, Does.Contain(BanRefactorMigration));
            Assert.That(migrationHistory, Has.Count.EqualTo(expectedMigrations.Length));
            Assert.That(pendingMigrations, Is.Empty);
        });
    }

    [Test]
    public async Task RoleOnlyLegacyDatabaseProducesPositiveAndAdvancingBanIds()
    {
        await using var connection = await OpenConnection();
        await using var context = CreateContext(connection);
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(LegacyMigration);
        await ExecuteNonQuery(connection, SeedRoleOnlyLegacyBans);
        await migrator.MigrateAsync();

        var migratedBanId = await ExecuteScalarInt(connection, "SELECT ban_id FROM ban WHERE type = 1;");
        var migratedRoles = await QueryStrings(connection, """
            SELECT role_type || ':' || role_id
            FROM ban_role
            ORDER BY role_type, role_id;
            """);

        await ExecuteNonQuery(connection, """
            INSERT INTO ban
                (type, playtime_at_note, ban_time, reason, severity, exempt_flags, auto_delete, hidden)
            VALUES
                (0, '00:00:00', '2026-01-03 00:00:00', 'post-migration-ban', 0, 0, FALSE, FALSE);
            """);
        var nextBanId = await ExecuteScalarInt(connection, "SELECT last_insert_rowid();");
        var banCount = await ExecuteScalarInt(connection, "SELECT COUNT(*) FROM ban;");
        var migratedRoleCount = await ExecuteScalarInt(
            connection,
            $"SELECT COUNT(*) FROM ban_role WHERE ban_id = {migratedBanId};");
        var foreignKeyViolations = await CountRows(connection, "PRAGMA foreign_key_check;");

        Assert.Multiple(() =>
        {
            Assert.That(migratedBanId, Is.EqualTo(5));
            Assert.That(migratedBanId, Is.Positive);
            Assert.That(migratedRoles, Is.EqualTo(new[] { "Antag:Traitor", "Job:Assistant" }));
            Assert.That(nextBanId, Is.GreaterThan(migratedBanId));
            Assert.That(banCount, Is.EqualTo(2));
            Assert.That(migratedRoleCount, Is.EqualTo(2));
            Assert.That(foreignKeyViolations, Is.Zero);
        });
    }

    private static SqliteServerDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<SqliteServerDbContext>()
            .UseSqlite(connection)
            .Options;
        return new SqliteServerDbContext(options);
    }

    private static async Task<SqliteConnection> OpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ExecuteScalarInt(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> CountRows(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var count = 0;
        while (await reader.ReadAsync())
        {
            count++;
        }

        return count;
    }

    private static async Task<List<string>> QueryStrings(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private const string SeedMixedLegacyBans = """
        INSERT INTO player
            (user_id, first_seen_time, last_seen_time, last_seen_user_name, last_seen_address)
        VALUES
            ('10000000-0000-0000-0000-000000000001', '2026-01-01 00:00:00', '2026-01-01 00:00:00', 'GameTarget', '198.51.100.10'),
            ('20000000-0000-0000-0000-000000000002', '2026-01-01 00:00:00', '2026-01-01 00:00:00', 'RoleTarget', '203.0.113.20');

        INSERT INTO server (server_id, name) VALUES (1, 'BanRefactorMigrationTest');
        INSERT INTO round (round_id, server_id, start_date) VALUES (7, 1, '2026-01-01 00:00:00');

        INSERT INTO server_ban
            (server_ban_id, player_user_id, address, hwid, hwid_type, ban_time, expiration_time,
             reason, round_id, severity, playtime_at_note, exempt_flags, auto_delete, hidden)
        VALUES
            (10, '10000000-0000-0000-0000-000000000001', '198.51.100.10/32', X'01020304', 1,
             '2026-01-01 01:00:00', '2026-02-01 01:00:00', 'legacy-game-ban', 7, 2, '01:02:03', 3, TRUE, TRUE);

        INSERT INTO server_unban (unban_id, ban_id, unban_time)
        VALUES (1, 10, '2026-01-02 01:00:00');

        INSERT INTO server_role_ban
            (server_role_ban_id, player_user_id, address, hwid, hwid_type, ban_time, expiration_time,
             reason, round_id, severity, playtime_at_note, hidden, role_id)
        VALUES
            (20, '20000000-0000-0000-0000-000000000002', '203.0.113.20/32', X'AABBCCDD', 2,
             '2026-01-01 02:00:00', NULL, 'legacy-role-ban', 7, 1, '00:10:00', FALSE, 'Job:Captain'),
            (21, '20000000-0000-0000-0000-000000000002', '203.0.113.20/32', X'AABBCCDD', 2,
             '2026-01-01 02:00:00', NULL, 'legacy-role-ban', 7, 1, '00:10:00', FALSE, 'Antag:Revolutionary'),
            (22, '20000000-0000-0000-0000-000000000002', '203.0.113.20/32', X'AABBCCDD', 2,
             '2026-01-01 02:00:00', NULL, 'legacy-role-ban', 7, 1, '00:10:00', FALSE, 'Job:ChiefEngineer');

        INSERT INTO server_role_unban (role_unban_id, ban_id, unban_time)
        VALUES
            (1, 20, '2026-01-02 02:00:00'),
            (2, 21, '2026-01-02 02:00:00');
        """;

    private const string SeedRoleOnlyLegacyBans = """
        INSERT INTO server_role_ban
            (server_role_ban_id, address, ban_time, expiration_time, reason, severity, playtime_at_note, hidden, role_id)
        VALUES
            (5, '203.0.113.50/32', '2026-01-01 03:00:00', NULL, 'role-only-ban', 1, '00:00:00', FALSE, 'Job:Assistant'),
            (6, '203.0.113.50/32', '2026-01-01 03:00:00', NULL, 'role-only-ban', 1, '00:00:00', FALSE, 'Antag:Traitor');
        """;
}
