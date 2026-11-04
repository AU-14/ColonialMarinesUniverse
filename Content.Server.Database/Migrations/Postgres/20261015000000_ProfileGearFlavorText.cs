// cmu edit start
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Content.Server.Database.Migrations.Postgres;

// CMU14: text written on a character's vendor armor and helmet.
[DbContext(typeof(PostgresServerDbContext))]
[Migration("20261015000000_ProfileGearFlavorText")]
public partial class ProfileGearFlavorText : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("armor_flavor_text", "profile", type: "text", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("helmet_flavor_text", "profile", type: "text", nullable: false, defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("armor_flavor_text", "profile");
        migrationBuilder.DropColumn("helmet_flavor_text", "profile");
    }
}
// cmu edit end
