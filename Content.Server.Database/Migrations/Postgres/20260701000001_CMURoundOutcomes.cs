using System;
using Content.Server.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    [DbContext(typeof(PostgresServerDbContext))]
    [Migration("20260701000001_CMURoundOutcomes")]
    public partial class CMURoundOutcomes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cmu_round_outcomes",
                columns: table => new
                {
                    round_id = table.Column<int>(type: "integer", nullable: false),
                    preset_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    winner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    outcome = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    source = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    selected_threat_id = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    planet_id = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    govfor_platoon_id = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    opfor_platoon_id = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    player_count = table.Column<int>(type: "integer", nullable: false),
                    duration_seconds = table.Column<int>(type: "integer", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cmu_round_outcomes", x => x.round_id);
                    table.ForeignKey(
                        name: "FK_cmu_round_outcomes_round_round_id",
                        column: x => x.round_id,
                        principalTable: "round",
                        principalColumn: "round_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cmu_round_outcomes_preset_id",
                table: "cmu_round_outcomes",
                column: "preset_id");

            migrationBuilder.CreateIndex(
                name: "IX_cmu_round_outcomes_recorded_at",
                table: "cmu_round_outcomes",
                column: "recorded_at");

            migrationBuilder.CreateIndex(
                name: "IX_cmu_round_outcomes_selected_threat_id",
                table: "cmu_round_outcomes",
                column: "selected_threat_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cmu_round_outcomes");
        }
    }
}
