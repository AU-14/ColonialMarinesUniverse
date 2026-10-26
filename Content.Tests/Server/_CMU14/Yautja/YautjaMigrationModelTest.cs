using Content.Server.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NUnit.Framework;

namespace Content.Tests.Server.CMU14.Yautja;

[TestFixture]
public sealed class YautjaMigrationModelTest
{
    [Test]
    public void SqliteMigrationSnapshotIncludesYautjaProfileAndClans()
    {
        using var db = new SqliteServerDbContext(new DbContextOptionsBuilder<SqliteServerDbContext>()
            .UseSqlite("Data Source=:memory:").Options);
        Assert.That(db.Database.HasPendingModelChanges(), Is.False);
    }

    [Test]
    public void PostgresMigrationSnapshotIncludesYautjaProfileAndClans()
    {
        using var db = new PostgresServerDbContext(new DbContextOptionsBuilder<PostgresServerDbContext>()
            .UseNpgsql("Host=localhost;Database=unused").Options);
        Assert.That(db.Database.HasPendingModelChanges(), Is.False);
    }

    [Test]
    public void PostgresUpgradeFromForceOnForceRepairsMissingYautjaSchema()
    {
        using var db = new PostgresServerDbContext(new DbContextOptionsBuilder<PostgresServerDbContext>()
            .UseNpgsql("Host=localhost;Database=unused").Options);

        // The imported Yautja migrations predate an already deployed migration. An upgrade
        // script starting at that deployment must still repair its missing Yautja schema.
        var script = db.GetService<IMigrator>().GenerateScript("20260926000000_ForceOnForcePreferences");
        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("ADD COLUMN IF NOT EXISTS yautja_rank integer NULL"));
            Assert.That(script, Does.Contain("ADD COLUMN IF NOT EXISTS yautja_whitelist_flags integer NOT NULL DEFAULT 0"));
            Assert.That(script, Does.Contain("ADD COLUMN IF NOT EXISTS yautja_profile text NULL"));
            Assert.That(script, Does.Contain("CREATE TABLE IF NOT EXISTS yautja_clan ("));
            Assert.That(script, Does.Contain("CREATE TABLE IF NOT EXISTS yautja_clan_member ("));
            Assert.That(script, Does.Contain("ON CONFLICT (player_user_id) DO NOTHING"));
        });
    }
}
