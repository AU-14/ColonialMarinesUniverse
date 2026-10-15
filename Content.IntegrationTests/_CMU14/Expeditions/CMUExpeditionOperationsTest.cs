using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Expeditions;
using Content.Server.CMU14.Fighter;
using Content.Shared.CMU14.Fighter;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Entrenching;
using Content.Shared.CMU14.Expeditions;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Expeditions;

[TestFixture, NonParallelizable]
public sealed class CMUExpeditionOperationsTest : GameTest
{
    // fixed seed, these assert emergent fights and a fresh seed per pair made a different one miss each run
    public override PoolSettings PoolSettings => new() { Dirty = true, ServerSeed = 1414 };

    // learned tactics carry over between rounds on a recycled server, start every case from the baseline
    [SetUp]
    public async Task ResetExpeditionLearning() =>
        await Server.WaitPost(() => Server.System<CMUExpeditionAgentSystem>().ResetLearnedExperience());

    [Test]
    public async Task AutomaticLandingZoneAndGuardOrderBuildPhysicalCover()
    {
        EntityUid map = default, guard = default, fighter = default, pilot = default;
        EntityCoordinates destination = default;
        await Server.WaitAssertion(() =>
        {
            var generator = Server.System<CMUExpeditionSystem>();
            Assert.That(generator.TryGenerate("CMUExpeditionWoodland", 42, CMUExpeditionLandform.RiverValley,
                CMUExpeditionStory.CrashRecovery, out map, out var error), Is.True, error);
            var expedition = SEntMan.GetComponent<CMUExpeditionMapComponent>(map);
            expedition.AutoOpen = true;
            Assert.That(expedition.LandingBeacon, Is.Null);
            for (var i = 0; i < 900 && !expedition.Ready; i++) generator.Update(0);
            Assert.That(expedition.Ready, Is.True);
            Assert.That(expedition.LandingBeacon, Is.Not.Null);
            var original = expedition.LandingBeacon;
            Assert.That(generator.OpenLandingZone(map), Is.True);
            Assert.That(expedition.LandingBeacon, Is.EqualTo(original));
            var lz = expedition.Plan.LandingZone;
            var start = new EntityCoordinates(map, new Vector2(lz.X + .5f, lz.Y + .5f));
            guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", start);
            destination = start.Offset(new Vector2(4, 0));
            Assert.That(Server.System<CMUExpeditionAgentSystem>().OrderPosition(guard, destination, true), Is.True);
        });
        await Pair.RunSeconds(40);
        await Server.WaitAssertion(() =>
        {
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            Assert.That(Server.System<SharedTransformSystem>().InRange(SEntMan.GetComponent<TransformComponent>(guard).Coordinates,
                destination, 1), Is.True, "A move order must physically relocate the guard.");
            var built = SEntMan.EntityQueryEnumerator<DirtMoundComponent, TransformComponent>();
            var found = false;
            while (built.MoveNext(out _, out _, out var xform))
                found |= xform.MapUid == map && Vector2.Distance(xform.LocalPosition, destination.Position) < 3;
            Assert.That(found, Is.True, $"Dig and build a real native mound: state={agent.State}, tool={agent.WorkItem}, action={agent.WorkDoAfter}, decision={agent.FortificationDecision}.");
            SEntMan.DeleteEntity(guard);
            // Move the aircraft test away from the newly built barricade.
            var site = destination.Offset(new Vector2(-8, -4));
            fighter = SEntMan.SpawnEntity("CMUFighterGround", site);
            pilot = SEntMan.SpawnEntity("MobHuman", site.Offset(new Vector2(2.4f, 0)));
            SEntMan.EnsureComponent<MarineComponent>(pilot).Faction = "govfor";
            var flights = Server.System<FighterSystem>();
            Assert.That(flights.TryLaunchExpedition(pilot, map), Is.False, "An unseated user cannot launch an aircraft.");
            Assert.That(flights.TryBoardGround(pilot, fighter), Is.True);
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            var ground = SEntMan.GetComponent<FighterGroundComponent>(fighter);
            var flights = Server.System<FighterSystem>();
            var launch = SEntMan.GetComponent<TransformComponent>(fighter).Coordinates;
            Assert.That(flights.TryLaunchExpedition(pilot, map), Is.True);
            var flight = SEntMan.GetComponent<FighterAircraftComponent>(ground.Aircraft!.Value);
            Assert.That(flight.TerrainMap, Is.EqualTo(map));
            Assert.That(flight.ViewMap, Is.EqualTo(SEntMan.GetComponent<CMUExpeditionMapComponent>(map).UpperMaps[0]));
            Assert.That(flight.GroundState, Is.EqualTo(FighterGroundState.TakingOff));
            Assert.That(ground.LaunchCoordinates, Is.EqualTo(launch), "Changing theater must preserve the physical return site.");
            Assert.That(flights.TryLaunchExpedition(pilot, map), Is.False, "No theater changes during takeoff.");
            SEntMan.DeleteEntity(fighter);
            SEntMan.DeleteEntity(map);
        });
    }
}
