using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class YautjaClanWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "yautja_whitelist_flags",
                table: "player",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "yautja_clan",
                columns: table => new
                {
                    yautja_clan_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    honor = table.Column<int>(type: "INTEGER", nullable: false),
                    color = table.Column<string>(type: "TEXT", nullable: false),
                    active = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_yautja_clan", x => x.yautja_clan_id);
                });

            migrationBuilder.CreateTable(
                name: "yautja_clan_member",
                columns: table => new
                {
                    yautja_clan_member_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    player_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    clan_id = table.Column<int>(type: "INTEGER", nullable: true),
                    rank = table.Column<int>(type: "INTEGER", nullable: false),
                    permissions = table.Column<int>(type: "INTEGER", nullable: false),
                    honor = table.Column<int>(type: "INTEGER", nullable: false),
                    is_legacy = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_yautja_clan_member", x => x.yautja_clan_member_id);
                    table.ForeignKey(
                        name: "FK_yautja_clan_member_player_player_user_id",
                        column: x => x.player_user_id,
                        principalTable: "player",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_yautja_clan_member_yautja_clan_clan_id",
                        column: x => x.clan_id,
                        principalTable: "yautja_clan",
                        principalColumn: "yautja_clan_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_yautja_clan_member_clan_id",
                table: "yautja_clan_member",
                column: "clan_id");

            migrationBuilder.CreateIndex(
                name: "IX_yautja_clan_member_player_user_id",
                table: "yautja_clan_member",
                column: "player_user_id",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO yautja_clan_member (player_user_id, clan_id, rank, permissions, honor, is_legacy)
                SELECT user_id,
                       NULL,
                       CASE WHEN yautja_rank IN (0, 2, 3, 4, 5, 6) THEN yautja_rank ELSE 2 END,
                       CASE yautja_rank
                           WHEN 0 THEN 8
                           WHEN 5 THEN 11
                           WHEN 6 THEN 28
                           ELSE 3
                       END,
                       0,
                       1
                FROM player
                WHERE yautja_rank IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "yautja_clan_member");

            migrationBuilder.DropTable(
                name: "yautja_clan");

            migrationBuilder.DropColumn(
                name: "yautja_whitelist_flags",
                table: "player");
        }
    }
}
