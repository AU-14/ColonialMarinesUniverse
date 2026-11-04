using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.TacticalMap;
using Content.Shared._RMC14.TacticalMap;

namespace Content.IntegrationTests.CMU14.TacticalMap;

[TestFixture]
public sealed class CMUCommsTowerVisibilityTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestCase("AU14CommsArrayGovfor")]
    [TestCase("AU14CommsMastFieldGovfor")]
    public async Task XenosSeeFixedCommunicationsWithoutRevealingHumanContacts(string towerPrototype)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var viewer = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            var tower = SEntMan.SpawnEntity(towerPrototype, map.GridCoords);
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
#pragma warning disable RA0002 // Arrange map contacts and inspect the published audience-specific snapshot.
            var snapshot = new TacticalMapComponent();
            snapshot.MarineBlips[tower.Id] = new TacticalMapBlip { Indices = new(1, 1) };
            snapshot.MarineBlips[human.Id] = new TacticalMapBlip { Indices = new(2, 1) };
            var user = SEntMan.EnsureComponent<TacticalMapUserComponent>(viewer);
            user.Xenos = true;
            user.LiveUpdate = true;
            Server.System<TacticalMapSystem>().UpdateUserData((viewer, user), snapshot);
            Assert.That(user.XenoStructureBlips.ContainsKey(tower.Id), Is.True,
                "fixed communications are public infrastructure on the hive map");
            Assert.That(user.XenoStructureBlips.ContainsKey(human.Id) || user.XenoBlips.ContainsKey(human.Id), Is.False,
                "showing towers must not disclose human positions");
#pragma warning restore RA0002
        });
    }
}
