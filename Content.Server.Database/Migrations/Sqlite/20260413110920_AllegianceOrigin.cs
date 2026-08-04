using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AllegianceOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "allegiance",
                table: "profile",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origin",
                table: "profile",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "allegiance",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "origin",
                table: "profile");
        }
    }
}
