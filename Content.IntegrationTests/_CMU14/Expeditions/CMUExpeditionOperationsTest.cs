using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Expeditions;
using Content.Shared._RMC14.Entrenching;
using Content.Shared.CMU14.Expeditions;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Expeditions;

[TestFixture]
public sealed class CMUExpeditionOperationsTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    [Test]
    public async Task AutomaticLandingZoneAndGuardOrderBuildPhysicalCover()
    {
        EntityUid map = default, guard = default;
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
            Assert.That(found, Is.True, $"Dig and build a real native mound: state={agent.State}, tool={agent.WorkItem}, action={agent.WorkDoAfter}.");
            SEntMan.DeleteEntity(map);
        });
    }
}
