// cmu edit start
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Content.Server.Database.Migrations.Postgres;

// CMU14: the character's religion.
[DbContext(typeof(PostgresServerDbContext))]
[Migration("20261014000000_ProfileReligion")]
public partial class ProfileReligion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("religion", "profile", type: "text", nullable: false, defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("religion", "profile");
    }
}
// cmu edit end
