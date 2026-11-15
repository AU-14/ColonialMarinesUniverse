using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Content.Server.Database.Migrations.Postgres;

// CMU14: round-start antag draw pity, persisted across server restarts.
[DbContext(typeof(PostgresServerDbContext))]
[Migration("20261016000000_CmuAntagPity")]
public partial class CmuAntagPity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS cmu_antag_pity (
                player_user_id uuid NOT NULL,
                missed integer NOT NULL DEFAULT 0,
                last_seen bigint NOT NULL DEFAULT 0,
                CONSTRAINT "PK_cmu_antag_pity" PRIMARY KEY (player_user_id)
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS cmu_antag_pity;
            """);
    }
}
