using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Explosion.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Ship;

/// <summary>
///     After a hijack crash, a hull window is as strong as a normal reinforced window: an HEDP going off right next to it
///     shatters it down to its frame.
/// </summary>
[TestFixture]
public sealed class CMUCrashBreakableHullTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task HullWindowBreaksLikeAReinforcedWindow()
    {
        var map = await Pair.CreateTestMap();
        var queued = typeof(ExplosionSystem).GetField("_explosionQueue", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var active = typeof(ExplosionSystem).GetField("_activeExplosion", BindingFlags.Instance | BindingFlags.NonPublic)!;

        EntityUid window = default;
        ExplosiveComponent hedp = default!;
        var center = new Vector2(10.5f, 10.5f);

        await Server.WaitAssertion(() =>
        {
            map.Grid.Comp.CanSplit = false;
            var maps = Server.System<SharedMapSystem>();
            for (var x = -6; x <= 6; x++)
            for (var y = -6; y <= 6; y++)
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i((int) center.X + x, (int) center.Y + y), map.Tile.Tile);

            window = SEntMan.SpawnEntity("CMWindowReinforcedAlmayerHull", new EntityCoordinates(map.Grid.Owner, center));
            SEntMan.AddComponents(window, SProtoMan.Index<EntityPrototype>("CMUCrashBreakableHullTemplate"), removeExisting: false);
            hedp = (ExplosiveComponent) SProtoMan.Index<EntityPrototype>("CMGrenadeHighExplosive").Components["Explosive"].Component;
        });

        await Server.WaitAssertion(() =>
        {
            // Goes off on the floor right next to the window.
            var origin = Server.System<SharedTransformSystem>()
                .ToMapCoordinates(new EntityCoordinates(map.Grid.Owner, center + new Vector2(1, 0)));
            Server.System<ExplosionSystem>().QueueExplosion(origin, hedp.ExplosionType, hedp.TotalIntensity,
                hedp.IntensitySlope, hedp.MaxIntensity, cause: null, maxTileBreak: 0, canCreateVacuum: false);
        });

        var drained = false;
        for (var ticks = 0; ticks < 600 && !drained; ticks += 10)
        {
            await Pair.RunTicksSync(10);
            await Server.WaitAssertion(() =>
            {
                var system = Server.System<ExplosionSystem>();
                drained = ((System.Collections.ICollection) queued.GetValue(system)!).Count == 0 &&
                          active.GetValue(system) == null;
            });
        }

        await Server.WaitAssertion(() =>
        {
            Assert.That(drained, Is.True);
            Assert.That(SEntMan.Deleted(window), Is.True, "a hull window survived an HEDP next to it");

            var frames = 0;
            var query = SEntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out _, out var meta, out var xform))
            {
                if (meta.EntityPrototype?.ID == "RMCWindowFrameAlmayer" &&
                    (xform.LocalPosition - center).Length() < 0.5f)
                {
                    frames++;
                }
            }

            Assert.That(frames, Is.EqualTo(1), "the shattered hull window should leave its frame");
        });
    }
}
