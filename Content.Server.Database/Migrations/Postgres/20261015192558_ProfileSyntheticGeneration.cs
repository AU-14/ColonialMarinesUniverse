using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    // CMU14: add synthetic generation and model suffix to saved profiles.
    /// <inheritdoc />
    public partial class ProfileSyntheticGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "synthetic_generation",
                table: "profile",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "synthetic_model_suffix",
                table: "profile",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "synthetic_generation",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "synthetic_model_suffix",
                table: "profile");
        }
    }
}
