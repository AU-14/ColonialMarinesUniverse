// cmu edit start
using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Content.Server.Database.Migrations.Postgres;

// CMU14: yautja clan hall. clan sigil, notice and elite notes, plus petitions to join a clan
[DbContext(typeof(PostgresServerDbContext))]
[Migration("20261024000000_YautjaClanHall")]
public partial class YautjaClanHall : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("sigil", "yautja_clan", type: "text", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("notice", "yautja_clan", type: "text", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("elite_notes", "yautja_clan", type: "text", nullable: false, defaultValue: "");

        migrationBuilder.CreateTable(
            name: "yautja_clan_petition",
            columns: table => new
            {
                yautja_clan_petition_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                player_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                clan_id = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_yautja_clan_petition", x => x.yautja_clan_petition_id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_yautja_clan_petition_player_user_id",
            table: "yautja_clan_petition",
            column: "player_user_id",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("yautja_clan_petition");
        migrationBuilder.DropColumn("sigil", "yautja_clan");
        migrationBuilder.DropColumn("notice", "yautja_clan");
        migrationBuilder.DropColumn("elite_notes", "yautja_clan");
    }
}
// cmu edit end
