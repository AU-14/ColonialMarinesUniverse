using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Pushups;
using Content.Shared.Buckle;
using Content.Shared.CMU14.Gym;
using Content.Shared.CMU14.Pushups;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Standing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Gym;

/// <summary>
///     The gym equipment ported from cmss13 runs on the same body-based limits as pushups.
/// </summary>
[TestFixture]
public sealed class CMUGymTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task WeightMachineStartsAndEndsASet()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var buckle = SEntMan.System<SharedBuckleSystem>();
            var machine = SEntMan.SpawnEntity("CMUFitnessWeightsBench", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);

            Assert.That(buckle.TryBuckle(user, user, machine), Is.True);
            var exercise = SEntMan.GetComponent<CMUPushupsComponent>(user);
            Assert.That(exercise.Active, Is.True, "Getting on the machine starts a set.");
            Assert.That(exercise.Exercise, Is.EqualTo(CMUExercise.BenchPress));
            Assert.That(exercise.Limit, Is.GreaterThan(0));
            Assert.That(SEntMan.GetComponent<CMUExerciseMachineComponent>(machine).User, Is.EqualTo(user));

            buckle.Unbuckle(user, user);
            Assert.That(exercise.Active, Is.False, "Getting off ends the set.");
            Assert.That(SEntMan.GetComponent<CMUExerciseMachineComponent>(machine).User, Is.Null);
        });
    }

    [Test]
    public async Task DumbbellCurlsWhileHeld()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hands = SEntMan.System<SharedHandsSystem>();
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var dumbbell = SEntMan.SpawnEntity("CMUDumbbell", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(user, dumbbell), Is.True);

            SEntMan.EventBus.RaiseLocalEvent(dumbbell, new UseInHandEvent(user));
            var exercise = SEntMan.GetComponent<CMUPushupsComponent>(user);
            Assert.That(exercise.Active, Is.True);
            Assert.That(exercise.Exercise, Is.EqualTo(CMUExercise.Curls));
            Assert.That(exercise.Equipment, Is.EqualTo(dumbbell));
        });
    }

    [Test]
    public async Task PunchingBagCountsPunches()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var bag = SEntMan.SpawnEntity("CMUFitnessPunchingBag", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);

            // An unarmed punch is the user hitting it with themselves.
            var punch = new AttackedEvent(user, user, map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(bag, punch);
            var exercise = SEntMan.GetComponent<CMUPushupsComponent>(user);
            Assert.That(exercise.SessionExercise, Is.EqualTo(CMUExercise.Punching));
            Assert.That(exercise.SessionAmount, Is.EqualTo(1));
            Assert.That(exercise.SessionLimit, Is.GreaterThan(0));

            // Punch until winded; then their arms are too tired to punch it again for a bit.
            var limit = exercise.SessionLimit;
            for (var i = 1; i < limit; i++)
            {
                SEntMan.EventBus.RaiseLocalEvent(bag, punch);
            }

            Assert.That(exercise.PunchTiredUntil, Is.Not.Null);
            var attempt = new AttackAttemptEvent(user, bag);
            SEntMan.EventBus.RaiseLocalEvent(user, attempt);
            Assert.That(attempt.Cancelled, Is.True, "Tired arms can't punch the bag.");
        });
    }

    [Test]
    public async Task FailedPushupsCanCarryOnOnTheKnees()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        await Server.WaitAssertion(() =>
        {
            user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pushups = SEntMan.System<CMUPushupsSystem>();
            Assert.That(pushups.TryStart(user, CMUExercise.Pushups), Is.True);
            SEntMan.GetComponent<CMUPushupsComponent>(user).Limit = 0;
        });

        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            var exercise = SEntMan.GetComponent<CMUPushupsComponent>(user);
            Assert.That(exercise.Active, Is.False, "Their arms gave out.");
            Assert.That(exercise.KneesOfferedUntil, Is.Not.Null, "They're offered the chance to keep going on their knees.");
            Assert.That(SEntMan.System<StandingStateSystem>().IsDown(user), Is.True, "They stay down for it.");

            Assert.That(SEntMan.System<CMUPushupsSystem>().TryStart(user, CMUExercise.KneePushups), Is.True);
            Assert.That(exercise.Exercise, Is.EqualTo(CMUExercise.KneePushups));
            Assert.That(exercise.Limit, Is.GreaterThan(0), "Knee pushups have their own fatigue.");
        });
    }

    [Test]
    public async Task TreadmillBuildsItsBackHalfAndRuns()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            maps.SetTile(map.Grid, map.Tile.GridIndices + new Vector2i(-1, 0), new Tile(tiles["Plating"].TileId));

            var console = SEntMan.SpawnEntity("CMUTreadmill", map.GridCoords);
            var treadmill = SEntMan.GetComponent<CMUTreadmillComponent>(console);
            Assert.That(treadmill.Other, Is.Not.Null, "The console builds the belt behind it.");
            var back = treadmill.Other!.Value;
            Assert.That(SEntMan.GetComponent<CMUTreadmillComponent>(back).Other, Is.EqualTo(console));

            // Clicking the belt gets them on and running.
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(0, 1)));
            SEntMan.EventBus.RaiseLocalEvent(back, new InteractHandEvent(user, back));
            var exercise = SEntMan.GetComponent<CMUPushupsComponent>(user);
            Assert.That(exercise.Active, Is.True);
            Assert.That(exercise.Exercise, Is.EqualTo(CMUExercise.Running));
            Assert.That(treadmill.Running, Is.True);
            Assert.That(SEntMan.GetComponent<CMUTreadmillComponent>(back).Running, Is.True);
            Assert.That(SEntMan.GetComponent<TransformComponent>(user).Coordinates.Position,
                Is.EqualTo(SEntMan.GetComponent<TransformComponent>(console).Coordinates.Position), "They're put on the treadmill.");

            SEntMan.DeleteEntity(console);
            Assert.That(SEntMan.Deleted(back) || SEntMan.IsQueuedForDeletion(back), Is.True);
        });
    }
}
