using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class FixMissingRankName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE rank
                    ADD COLUMN IF NOT EXISTS rank_name text NOT NULL DEFAULT '';

                ALTER TABLE rank
                    ALTER COLUMN rank_name DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The original rank migration owns this column. Rolling back this repair must not remove it.
        }
    }
}
