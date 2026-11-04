// cmu edit start
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Content.Server.Database.Migrations.Sqlite;

// CMU14: the character's religion.
[DbContext(typeof(SqliteServerDbContext))]
[Migration("20261014000000_ProfileReligion")]
public partial class ProfileReligion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("religion", "profile", type: "TEXT", nullable: false, defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("religion", "profile");
    }
}
// cmu edit end
