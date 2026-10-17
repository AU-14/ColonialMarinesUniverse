using System.Linq;
using System.Net;
using Content.Shared.Database;

namespace Content.IntegrationTests.Tests.Preferences;

public sealed partial class ServerDbSqliteTests
{
    [Test]
    public async Task RMCChatBanAddressConstraintsRoundTripNullIpv4AndIpv6()
    {
        var db = GetDb(Pair.Server);
        var target = NewUserId();
        var admin = NewUserId();
        var ipv4 = IPAddress.Parse("198.51.100.42");
        var ipv6 = IPAddress.Parse("2001:db8:1234:5678::42");

        await db.UpdatePlayerRecord(target, "ChatBanTarget", IPAddress.Loopback, null);
        await db.UpdatePlayerRecord(admin, "ChatBanAdmin", IPAddress.Loopback, null);

        await db.AddChatBan(null, target, null, null, null, ChatType.Ooc, admin, "null-address");
        await db.AddChatBan(null, target, (ipv4, 32), null, null, ChatType.Looc, admin, "ipv4-address");
        await db.AddChatBan(null, target, (ipv6, 64), null, null, ChatType.Dead, admin, "ipv6-address");

        var bans = await db.GetAllChatBans(target.UserId);
        var nullAddress = bans.Single(ban => ban.Reason == "null-address");
        var ipv4Address = bans.Single(ban => ban.Reason == "ipv4-address");
        var ipv6Address = bans.Single(ban => ban.Reason == "ipv6-address");

        Assert.Multiple(() =>
        {
            Assert.That(bans, Has.Count.EqualTo(3));
            Assert.That(nullAddress.Address, Is.Null,
                "a chat ban without a last address must persist without an IP constraint");
            Assert.That(nullAddress.Type, Is.EqualTo(ChatType.Ooc));

            Assert.That(ipv4Address.Address, Is.Not.Null);
            Assert.That(ipv4Address.Address!.Value.Address, Is.EqualTo(ipv4));
            Assert.That(ipv4Address.Address.Value.Netmask, Is.EqualTo(32));
            Assert.That(ipv4Address.Type, Is.EqualTo(ChatType.Looc));

            Assert.That(ipv6Address.Address, Is.Not.Null);
            Assert.That(ipv6Address.Address!.Value.Address, Is.EqualTo(ipv6));
            Assert.That(ipv6Address.Address.Value.Netmask, Is.EqualTo(64));
            Assert.That(ipv6Address.Type, Is.EqualTo(ChatType.Dead));
        });
    }
}
