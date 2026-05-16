using Content.Server.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    [DbContext(typeof(SqliteServerDbContext))]
    [Migration("20260514000002_GamemodePreferences")]
    public partial class GamemodePreferences : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "gamemode_job_priorities",
                table: "profile",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gamemode_antag_preferences",
                table: "profile",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gamemode_threat_preferences",
                table: "profile",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "gamemode_job_priorities",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "gamemode_antag_preferences",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "gamemode_threat_preferences",
                table: "profile");
        }
    }
}
