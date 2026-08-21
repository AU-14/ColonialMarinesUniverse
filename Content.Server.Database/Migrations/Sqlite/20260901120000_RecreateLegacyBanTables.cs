// CMU14 file: recreates the legacy wizden ban tables (dropped by BanRefactor) so upstream
// master, whose model still lives in them, can share a database with this branch. Intentionally
// empty and without FKs/indexes; Rebase reads/writes only the new ban* tables.
using Content.Server.Database;
using Microsoft.EntityFrameworkCore.Infrastructure; // CMU14
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    [DbContext(typeof(SqliteServerDbContext))]
    [Migration("20260901120000_RecreateLegacyBanTables")]
    public sealed class RecreateLegacyBanTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS server_ban (
                    server_ban_id INTEGER PRIMARY KEY,
                    address TEXT,
                    auto_delete INTEGER NOT NULL,
                    ban_time TEXT NOT NULL,
                    banning_admin TEXT,
                    exempt_flags INTEGER NOT NULL,
                    expiration_time TEXT,
                    hidden INTEGER NOT NULL,
                    last_edited_at TEXT,
                    last_edited_by_id TEXT,
                    player_user_id TEXT,
                    playtime_at_note TEXT NOT NULL,
                    reason TEXT NOT NULL,
                    round_id INTEGER,
                    severity INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS server_unban (
                    unban_id INTEGER PRIMARY KEY,
                    ban_id INTEGER NOT NULL,
                    unban_time TEXT NOT NULL,
                    unbanning_admin TEXT
                );

                CREATE TABLE IF NOT EXISTS server_role_ban (
                    server_role_ban_id INTEGER PRIMARY KEY,
                    address TEXT,
                    ban_time TEXT NOT NULL,
                    banning_admin TEXT,
                    expiration_time TEXT,
                    hidden INTEGER NOT NULL,
                    last_edited_at TEXT,
                    last_edited_by_id TEXT,
                    player_user_id TEXT,
                    playtime_at_note TEXT NOT NULL,
                    reason TEXT NOT NULL,
                    role_id TEXT NOT NULL,
                    round_id INTEGER,
                    severity INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS server_role_unban (
                    role_unban_id INTEGER PRIMARY KEY,
                    ban_id INTEGER NOT NULL,
                    unban_time TEXT NOT NULL,
                    unbanning_admin TEXT
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
