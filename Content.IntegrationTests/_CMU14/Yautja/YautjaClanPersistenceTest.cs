using System;
using System.Linq;
using System.Net;
using Content.Server.Database;
using Content.Shared._CMU14.Yautja;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaClanPersistenceTest
{
    [Test]
    public void ClanAndMemberRoundTripPreservesRankPermissionsAndHonor()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<SqliteServerDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = new SqliteServerDbContext(options);
        context.Database.EnsureCreated();

        var userId = Guid.NewGuid();
        context.Player.Add(CreatePlayer(userId, YautjaRank.Elder));
        var clan = new YautjaClan
        {
            Name = "Test Clan",
            Description = "Persistence test",
            Honor = 42,
            Color = "#5c7f32",
            Active = true,
        };
        context.YautjaClans.Add(clan);
        context.SaveChanges();

        context.YautjaClanMembers.Add(new YautjaClanMember
        {
            PlayerUserId = userId,
            ClanId = clan.Id,
            Rank = (int) YautjaRank.Elder,
            Permissions = (int) (YautjaClanPermission.UserModify | YautjaClanPermission.UserView),
            Honor = 13,
            IsLegacy = false,
        });
        context.SaveChanges();

        var loaded = context.YautjaClanMembers
            .Include(member => member.Clan)
            .Single(member => member.PlayerUserId == userId);

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Clan!.Name, Is.EqualTo("Test Clan"));
            Assert.That(loaded.Rank, Is.EqualTo((int) YautjaRank.Elder));
            Assert.That(loaded.Permissions, Is.EqualTo((int) (YautjaClanPermission.UserModify | YautjaClanPermission.UserView)));
            Assert.That(loaded.Honor, Is.EqualTo(13));
            Assert.That(loaded.IsLegacy, Is.False);
        });
    }

    [Test]
    public void ExistingPlayerRankCanBeMarkedAsLegacyMember()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<SqliteServerDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = new SqliteServerDbContext(options);
        context.Database.EnsureCreated();

        var userId = Guid.NewGuid();
        context.Player.Add(CreatePlayer(userId, YautjaRank.Elder));
        context.SaveChanges();

        var player = context.Player.Single(entry => entry.UserId == userId);
        context.YautjaClanMembers.Add(new YautjaClanMember
        {
            PlayerUserId = player.UserId,
            Rank = player.YautjaRank!.Value,
            Permissions = (int) YautjaClanPermission.UserView,
            Honor = 0,
            IsLegacy = true,
        });
        context.SaveChanges();

        var loaded = context.YautjaClanMembers.Single(member => member.PlayerUserId == userId);
        Assert.That(loaded.IsLegacy, Is.True);
        Assert.That(loaded.Rank, Is.EqualTo((int) YautjaRank.Elder));
        Assert.That(loaded.ClanId, Is.Null);
    }

    private static Player CreatePlayer(Guid userId, YautjaRank rank)
    {
        var now = DateTime.UtcNow;
        return new Player
        {
            UserId = userId,
            FirstSeenTime = now,
            LastSeenUserName = "Yautja Clan Test",
            LastSeenTime = now,
            LastSeenAddress = IPAddress.Loopback,
            YautjaRank = (int) rank,
        };
    }
}
