using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class CMUBalanceRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cmu_balance_rating_polls",
                columns: table => new
                {
                    cmu_balance_rating_polls_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    round_id = table.Column<int>(type: "integer", nullable: false),
                    target = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    target_id = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    metric = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    opened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cmu_balance_rating_polls", x => x.cmu_balance_rating_polls_id);
                    table.CheckConstraint("CMUBalanceRatingMapMetric", "target <> 'Map' OR metric = 'Fun'");
                    table.CheckConstraint("CMUBalanceRatingPollMetric", "metric IN ('Power', 'Fun')");
                    table.CheckConstraint("CMUBalanceRatingPollTarget", "target IN ('Weapon', 'Xeno', 'Map')");
                    table.CheckConstraint("CMUBalanceRatingPollTimes", "closed_at IS NULL OR closed_at >= opened_at");
                    table.ForeignKey(
                        name: "FK_cmu_balance_rating_polls_player_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "player",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_cmu_balance_rating_polls_round_round_id",
                        column: x => x.round_id,
                        principalTable: "round",
                        principalColumn: "round_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cmu_balance_rating_responses",
                columns: table => new
                {
                    poll_id = table.Column<long>(type: "bigint", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<byte>(type: "smallint", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cmu_balance_rating_responses", x => new { x.poll_id, x.player_id });
                    table.CheckConstraint("CMUBalanceRatingResponseValue", "rating >= 1 AND rating <= 5");
                    table.ForeignKey(
                        name: "FK_cmu_balance_rating_responses_cmu_balance_rating_polls_poll_~",
                        column: x => x.poll_id,
                        principalTable: "cmu_balance_rating_polls",
                        principalColumn: "cmu_balance_rating_polls_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cmu_balance_rating_responses_player_player_id",
                        column: x => x.player_id,
                        principalTable: "player",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cmu_balance_rating_polls_created_by_id",
                table: "cmu_balance_rating_polls",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_cmu_balance_rating_polls_opened_at",
                table: "cmu_balance_rating_polls",
                column: "opened_at");

            migrationBuilder.CreateIndex(
                name: "IX_cmu_balance_rating_polls_round_id",
                table: "cmu_balance_rating_polls",
                column: "round_id");

            migrationBuilder.CreateIndex(
                name: "IX_cmu_balance_rating_polls_target_target_id_metric",
                table: "cmu_balance_rating_polls",
                columns: new[] { "target", "target_id", "metric" });

            migrationBuilder.CreateIndex(
                name: "IX_cmu_balance_rating_responses_player_id",
                table: "cmu_balance_rating_responses",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "IX_cmu_balance_rating_responses_recorded_at",
                table: "cmu_balance_rating_responses",
                column: "recorded_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cmu_balance_rating_responses");

            migrationBuilder.DropTable(
                name: "cmu_balance_rating_polls");
        }
    }
}
