using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Stacks;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class OctoberTwentyTwoRegressionTest : GameTest
{

    // BUG-1050: shells and loaded shotguns put through a supply crate
    [Test]
    public async Task ShotgunShellsSurviveASupplyCrate()
    {
        var map = await Pair.CreateTestMap();
        EntityUid crate = default, shells = default, shotgun = default;
        var roundsBefore = 0;
        await Server.WaitAssertion(() =>
        {
            var storage = SEntMan.System<SharedEntityStorageSystem>();
            crate = SEntMan.SpawnEntity("RMCCrateSupply", map.GridCoords);
            shells = SEntMan.SpawnEntity("CMShellShotgunBuckshot", map.GridCoords);
            shotgun = SEntMan.SpawnEntity("RMCWeaponShotgunM37A1", map.GridCoords);

            var ev = new GetAmmoCountEvent();
            SEntMan.EventBus.RaiseLocalEvent(shotgun, ref ev);
            roundsBefore = ev.Count;

            storage.OpenStorage(crate);
            Assert.That(storage.Insert(shells, crate), Is.True);
            Assert.That(storage.Insert(shotgun, crate), Is.True);
            storage.CloseStorage(crate);
        });
        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() =>
        {
            SEntMan.System<SharedEntityStorageSystem>().OpenStorage(crate);
            Assert.That(SEntMan.Deleted(shells), Is.False, "shells deleted");
            Assert.That(SEntMan.GetComponent<StackComponent>(shells).Count, Is.EqualTo(5));
            Assert.That(SEntMan.Deleted(shotgun), Is.False, "shotgun deleted");

            var ev = new GetAmmoCountEvent();
            SEntMan.EventBus.RaiseLocalEvent(shotgun, ref ev);
            Assert.That(ev.Count, Is.EqualTo(roundsBefore));
        });
    }
}
