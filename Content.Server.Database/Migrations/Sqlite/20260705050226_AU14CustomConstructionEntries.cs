using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AU14CustomConstructionEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "au14_custom_construction_entries",
                columns: table => new
                {
                    au14_custom_construction_entries_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    entry_key = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    yaml = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_edited_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_au14_custom_construction_entries", x => x.au14_custom_construction_entries_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_au14_custom_construction_entries_kind_entry_key",
                table: "au14_custom_construction_entries",
                columns: new[] { "kind", "entry_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "au14_custom_construction_entries");
        }
    }
}
