using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Tracker.SquadLeader;
using Content.Shared.CMU14.Visor;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Squads;

[TestFixture]
public sealed class SquadVisorTrackerTest : GameTest
{
    [Test]
    public async Task ActiveSquadOpticGrantsAndRemovesTheTracker()
    {
        var map = await Pair.CreateTestMap();
        EntityUid marine = default;
        EntityUid helmet = default;

        await Server.WaitAssertion(() =>
        {
            marine = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            helmet = SEntMan.SpawnEntity("ArmorHelmetM10", map.GridCoords);
            Assert.That(Server.System<InventorySystem>().TryEquip(marine, helmet, "head", silent: true, force: true), Is.True);
            SEntMan.RemoveComponent<CMUSquadVisorActiveComponent>(helmet);
            SEntMan.RemoveComponent<SquadLeaderTrackerComponent>(marine);
        });

        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<CMUSquadVisorActiveComponent>(helmet);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<CMUSquadVisorWearerComponent>(marine), Is.True);
                Assert.That(SEntMan.HasComponent<SquadLeaderTrackerComponent>(marine), Is.True,
                    "switching the squad optic down should give a tracker to someone without one");
            });

            SEntMan.RemoveComponent<CMUSquadVisorActiveComponent>(helmet);
        });

        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<CMUSquadVisorWearerComponent>(marine), Is.False);
                Assert.That(SEntMan.HasComponent<SquadLeaderTrackerComponent>(marine), Is.False,
                    "switching the optic up should take back the tracker it gave");
            });
        });
    }
}
