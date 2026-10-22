using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Round;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUWeaponIntegrationTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task ConsoleFactionUsesExplicitWhitelistBeforeShipFaction()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<ShipFactionComponent>(map.Grid).Faction = "govfor";
            var console = SEntMan.SpawnEntity("CMUMohawkMidwayGunneryea0f8d621b", map.GridCoords);
            var weapons = Server.System<SharedDropshipWeaponSystem>();
            Assert.That(weapons.GetConsoleFaction(console), Is.EqualTo("govfor"));
            SEntMan.AddComponent<WhitelistedShuttleComponent>(console).Faction = "opfor";
            Assert.That(weapons.GetConsoleFaction(console), Is.EqualTo("opfor"));
        });
    }
}
