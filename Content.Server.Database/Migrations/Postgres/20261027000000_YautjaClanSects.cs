// cmu edit start
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Content.Server.Database.Migrations.Postgres;

// CMU14: yautja clan sects, subdivisions a clan leader sets up, and which one each member is in
[DbContext(typeof(PostgresServerDbContext))]
[Migration("20261027000000_YautjaClanSects")]
public partial class YautjaClanSects : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("sect_id", "yautja_clan_member", type: "integer", nullable: true);

        migrationBuilder.CreateTable(
            name: "yautja_clan_sect",
            columns: table => new
            {
                yautja_clan_sect_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                clan_id = table.Column<int>(type: "integer", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                description = table.Column<string>(type: "text", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_yautja_clan_sect", x => x.yautja_clan_sect_id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_yautja_clan_sect_clan_id",
            table: "yautja_clan_sect",
            column: "clan_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("yautja_clan_sect");
        migrationBuilder.DropColumn("sect_id", "yautja_clan_member");
    }
}
// cmu edit end
