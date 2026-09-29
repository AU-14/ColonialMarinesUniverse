using Content.Server.Database;
using Microsoft.EntityFrameworkCore;
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
}
