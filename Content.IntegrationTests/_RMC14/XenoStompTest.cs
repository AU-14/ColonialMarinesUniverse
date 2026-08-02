using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Xenonids.Stomp;
using Content.Shared.DoAfter;
using Content.Shared.Physics;
using Content.Shared.StatusEffect;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class XenoStompTest
{
    [Test]
    public async Task BurrowerStompPassesBarricadesButNotWalls()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid burrower = default;
        EntityUid barricade = default;
        EntityUid marine = default;
        EntityUid wall = default;
        EntityUid wallMarine = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                burrower = entMan.SpawnEntity("CMXenoBurrower", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
                barricade = entMan.SpawnEntity("CMBarricadeMetal", map.GridCoords.Offset(new Vector2(0.5f, 1.5f)));
                marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, 2.5f)));
                wall = entMan.SpawnEntity("WallSolid", map.GridCoords.Offset(new Vector2(1.5f, 0.5f)));
                wallMarine = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2.5f, 0.5f)));
            });

            await pair.RunTicksSync(1);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var physics = entMan.System<SharedPhysicsSystem>();
                var status = entMan.System<StatusEffectQuerySystem>();
                var transform = entMan.System<SharedTransformSystem>();
                var origin = transform.GetMapCoordinates(burrower);
                var barricadeTarget = transform.GetMapCoordinates(marine);
                var barricadeDiff = barricadeTarget.Position - origin.Position;
                var barricadeRay = new CollisionRay(
                    origin.Position,
                    Vector2.Normalize(barricadeDiff),
                    (int) CollisionGroup.BarricadeImpassable);
                var barricadeHit = physics.IntersectRay(
                    origin.MapId,
                    barricadeRay,
                    barricadeDiff.Length(),
                    burrower,
                    returnOnFirstHit: true).Single();

                Assert.That(barricadeHit.HitEntity, Is.EqualTo(barricade));

                var wallTarget = transform.GetMapCoordinates(wallMarine);
                var wallDiff = wallTarget.Position - origin.Position;
                var wallRay = new CollisionRay(
                    origin.Position,
                    Vector2.Normalize(wallDiff),
                    (int) (CollisionGroup.Impassable | CollisionGroup.InteractImpassable));
                var wallHit = physics.IntersectRay(
                    origin.MapId,
                    wallRay,
                    wallDiff.Length(),
                    burrower,
                    returnOnFirstHit: true).Single();

                Assert.That(wallHit.HitEntity, Is.EqualTo(wall));

                var stomp = new XenoStompDoAfterEvent();
                stomp.DoAfter = new DoAfter(
                    0,
                    new DoAfterArgs(entMan, burrower, TimeSpan.Zero, stomp, burrower),
                    TimeSpan.Zero);
                entMan.EventBus.RaiseLocalEvent(burrower, stomp);

                Assert.That(stomp.Handled, Is.True);
                Assert.That(status.TryGetTime(marine, "Stun", out _), Is.True);
                Assert.That(status.TryGetTime(wallMarine, "Stun", out _), Is.False);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                if (entMan.EntityExists(burrower))
                    entMan.DeleteEntity(burrower);

                if (entMan.EntityExists(barricade))
                    entMan.DeleteEntity(barricade);

                if (entMan.EntityExists(marine))
                    entMan.DeleteEntity(marine);

                if (entMan.EntityExists(wall))
                    entMan.DeleteEntity(wall);

                if (entMan.EntityExists(wallMarine))
                    entMan.DeleteEntity(wallMarine);
            });

            await pair.CleanReturnAsync();
        }
    }
}
