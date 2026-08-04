using Content.Server.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    [DbContext(typeof(PostgresServerDbContext))]
    [Migration("20260514000003_GamemodePreferences")]
    public partial class GamemodePreferences : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "gamemode_job_priorities",
                table: "profile",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gamemode_antag_preferences",
                table: "profile",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gamemode_threat_preferences",
                table: "profile",
                type: "text",
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
