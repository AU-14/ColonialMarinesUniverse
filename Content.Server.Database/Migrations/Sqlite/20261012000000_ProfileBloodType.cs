using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Content.Server.Database.Migrations.Sqlite;

// CMU14: the character's blood type.
[DbContext(typeof(SqliteServerDbContext))]
[Migration("20261012000000_ProfileBloodType")]
public partial class ProfileBloodType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("blood_type", "profile", type: "TEXT", nullable: false, defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("blood_type", "profile");
    }
}
