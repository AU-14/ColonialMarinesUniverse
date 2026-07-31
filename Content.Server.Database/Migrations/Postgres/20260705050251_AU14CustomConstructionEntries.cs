using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
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
                    au14_custom_construction_entries_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    entry_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    yaml = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_edited_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
