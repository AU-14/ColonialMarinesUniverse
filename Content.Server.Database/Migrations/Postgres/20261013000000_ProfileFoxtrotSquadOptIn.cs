// cmu edit start
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Content.Server.Database.Migrations.Postgres;

// CMU14: opting a character into the Foxtrot squad.
[DbContext(typeof(PostgresServerDbContext))]
[Migration("20261013000000_ProfileFoxtrotSquadOptIn")]
public partial class ProfileFoxtrotSquadOptIn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("foxtrot_squad_opt_in", "profile", type: "boolean", nullable: false, defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("foxtrot_squad_opt_in", "profile");
    }
}
// cmu edit end
