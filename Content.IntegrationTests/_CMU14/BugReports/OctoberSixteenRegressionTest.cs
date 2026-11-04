using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.ActionBlocker;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class OctoberSixteenRegressionTest : GameTest
{
    [Test]
    public async Task DefibrillatorsDoNotFitInTheMedicalPouch()
    {
        await Server.WaitAssertion(() =>
        {
            var pouch = SEntMan.SpawnEntity("RMCPouchMedical", MapCoordinates.Nullspace);
            var defib = SEntMan.SpawnEntity("AU14CMDefibrillator", MapCoordinates.Nullspace);
            var storage = SEntMan.System<SharedStorageSystem>();

            Assert.That(storage.CanInsert(pouch, defib, out _, ignoreLocation: true), Is.False);

            SEntMan.DeleteEntity(defib);
            SEntMan.DeleteEntity(pouch);
        });
    }

    // standing on a coat rack's tile blocked clicks on anything past it, like the light fixture
    [Test]
    public async Task CoatRackDoesNotBlockInteractionsPastIt()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var rack = SEntMan.SpawnEntity("RMCCoatRack", map.GridCoords);
            var origin = new MapCoordinates(map.MapCoords.Position - new Vector2(.3f, 0), map.MapId);
            var past = new MapCoordinates(map.MapCoords.Position + new Vector2(1f, 0), map.MapId);

            Assert.That(SEntMan.System<SharedInteractionSystem>().InRangeUnobstructed(origin, past), Is.True);

            SEntMan.DeleteEntity(rack);
        });
    }

    // the being-pulled alert ignored whether the victim could move at all, unlike walking off
    [Test]
    public async Task DownedVictimCantClickOutOfADrag()
    {
        var map = await Pair.CreateTestMap();
        EntityUid xeno = default, victim = default, helper = default, walker = default;
        await Server.WaitPost(() =>
        {
            xeno = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            victim = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            helper = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            walker = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.System<MobStateSystem>().ChangeMobState(victim, MobState.Critical);

            var pulling = SEntMan.System<PullingSystem>();
            Assert.That(pulling.TryStartPull(xeno, victim), Is.True);
            Assert.That(pulling.TryStartPull(helper, walker), Is.True);
        });
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var pulling = SEntMan.System<PullingSystem>();
            var dragged = SEntMan.GetComponent<PullableComponent>(victim);
            var led = SEntMan.GetComponent<PullableComponent>(walker);

            // players always have a mover, the test mobs get one stripped while nobody's in them
            var blocker = SEntMan.System<ActionBlockerSystem>();
            foreach (var mob in new[] { victim, walker })
            {
                SEntMan.EnsureComponent<InputMoverComponent>(mob);
                blocker.UpdateCanMove(mob);
            }

            Assert.That(blocker.CanMove(walker), Is.True);

            // what the being-pulled alert does
            Assert.That(pulling.TryStopPull(victim, dragged, victim), Is.False);
            Assert.That(dragged.Puller, Is.EqualTo(xeno));

            // someone who can still walk keeps the click-free
            Assert.That(pulling.TryStopPull(walker, led, walker), Is.True);
            Assert.That(led.Puller, Is.Null);

            // and the xeno can still let go itself
            Assert.That(pulling.TryStopPull(victim, dragged, xeno), Is.True);
        });
    }
}
