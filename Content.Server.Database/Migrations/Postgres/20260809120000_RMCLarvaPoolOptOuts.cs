using Content.Server.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres;

[DbContext(typeof(PostgresServerDbContext))]
[Migration("20260809120000_RMCLarvaPoolOptOuts")]
public sealed class RMCLarvaPoolOptOuts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // This table was previously applied under the 20260718212523_RMCLarvaPoolOptOuts migration ID.
        // Keep the replacement migration compatible with both those databases and fresh installs.
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS rmc_larva_pool_opt_out (
                player_id uuid NOT NULL,
                hive_id text NOT NULL,
                CONSTRAINT "PK_rmc_larva_pool_opt_out" PRIMARY KEY (player_id, hive_id),
                CONSTRAINT "FK_rmc_larva_pool_opt_out_player_player_id" FOREIGN KEY (player_id) REFERENCES player (user_id) ON DELETE CASCADE
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "rmc_larva_pool_opt_out");
    }
}
