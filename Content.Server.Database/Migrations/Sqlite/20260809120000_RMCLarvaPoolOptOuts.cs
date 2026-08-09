using System;
using Content.Server.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite;

[DbContext(typeof(SqliteServerDbContext))]
[Migration("20260809120000_RMCLarvaPoolOptOuts")]
public sealed class RMCLarvaPoolOptOuts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "rmc_larva_pool_opt_out",
            columns: table => new
            {
                player_id = table.Column<Guid>(type: "TEXT", nullable: false),
                hive_id = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_rmc_larva_pool_opt_out", x => new { x.player_id, x.hive_id });
                table.ForeignKey(
                    name: "FK_rmc_larva_pool_opt_out_player_player_id",
                    column: x => x.player_id,
                    principalTable: "player",
                    principalColumn: "user_id",
                    onDelete: ReferentialAction.Cascade);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "rmc_larva_pool_opt_out");
    }
}
