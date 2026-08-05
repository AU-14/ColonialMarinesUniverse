using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class RegulationAppearance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "regulation_facial_hair_color",
                table: "profile",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "regulation_facial_hair_name",
                table: "profile",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "regulation_hair_color",
                table: "profile",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "regulation_hair_name",
                table: "profile",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "regulation_facial_hair_color",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "regulation_facial_hair_name",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "regulation_hair_color",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "regulation_hair_name",
                table: "profile");
        }
    }
}
