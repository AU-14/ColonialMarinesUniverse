using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Content.Server.Database.Migrations.Postgres;

[DbContext(typeof(PostgresServerDbContext))]
[Migration("20260926000000_ForceOnForcePreferences")]
public partial class ForceOnForcePreferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("fo_f_side", "profile", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>("fo_f_fallback", "profile", type: "integer", nullable: false, defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("fo_f_side", "profile");
        migrationBuilder.DropColumn("fo_f_fallback", "profile");
    }
}
