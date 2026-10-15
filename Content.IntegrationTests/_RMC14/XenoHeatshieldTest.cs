using System.Numerics;
using Content.Shared._RMC14.Xenonids.Heatshield;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class XenoHeatshieldTest
{
    private const string FireSpewEffectPrototype = "RMCEffectXenoFireSpew";

    // CMU14: cover enemies in the extended outer rows while leaving targets behind the xeno untouched.
    [Test]
    public async Task BurningVomitBileIgnitesThreeRowsInFront()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid xeno = default;
        EntityUid targetNorth = default;
        EntityUid targetCenter = default;
        EntityUid targetSouth = default;
        EntityUid behind = default;
        Entity<ActionComponent> action = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                xeno = entMan.SpawnEntity("CMXenoDefenderHeatshield", map.GridCoords);
                targetNorth = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 1)));
                targetCenter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                targetSouth = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, -1)));
                behind = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-1, 0)));
                action = SpawnAction(entMan);

                var flammable = entMan.GetComponent<FlammableComponent>(xeno);
                flammable.OnFire = true;
                entMan.Dirty(xeno, flammable);
            });

            // await server.WaitRunTicks(1);
            await server.WaitPost(() => { RaiseVomitBile(server.EntMan, xeno, map.GridCoords.Offset(new Vector2(1, 0)), action); });

            await PoolManager.WaitUntil(server, ()
                => CountPrototype(server.EntMan, FireSpewEffectPrototype) == 9
                && IsOnFire(server.EntMan, targetNorth)
                && IsOnFire(server.EntMan, targetCenter)
                && IsOnFire(server.EntMan, targetSouth));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.Multiple(() =>
                {
                    Assert.That(IsOnFire(entMan, targetNorth), Is.True);
                    Assert.That(IsOnFire(entMan, targetCenter), Is.True);
                    Assert.That(IsOnFire(entMan, targetSouth), Is.True);
                    Assert.That(IsOnFire(entMan, behind), Is.False);
                    Assert.That(CountPrototype(entMan, FireSpewEffectPrototype), Is.EqualTo(9));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                DeletePrototypeEntities(entMan, FireSpewEffectPrototype);
                DeleteIfUidValid(entMan, xeno);
                DeleteIfUidValid(entMan, targetNorth);
                DeleteIfUidValid(entMan, targetCenter);
                DeleteIfUidValid(entMan, targetSouth);
                DeleteIfUidValid(entMan, behind);
                DeleteIfUidValid(entMan, action.Owner);
            });
        }

        await pair.CleanReturnAsync();
    }

    private static void DeleteIfUidValid(IEntityManager entMan, EntityUid uid)
    {
        if (uid != default && entMan.EntityExists(uid))
            entMan.DeleteEntity(uid);
    }

    private static Entity<ActionComponent> SpawnAction(IEntityManager entMan)
    {
        var action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
        return (action, entMan.EnsureComponent<ActionComponent>(action));
    }

    private static void RaiseVomitBile(
        IEntityManager entMan,
        EntityUid xeno,
        EntityCoordinates target,
        Entity<ActionComponent> action)
    {
        var ev = new XenoVomitBileActionEvent
        {
            Performer = xeno,
            Action = action,
            Target = target,
        };

        entMan.EventBus.RaiseLocalEvent(xeno, ev);
    }

    private static bool IsOnFire(IEntityManager entMan, EntityUid entity)
    {
        return entMan.GetComponent<FlammableComponent>(entity).OnFire;
    }

    private static int CountPrototype(IEntityManager entMan, string prototypeId)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var metadata))
        {
            if (metadata.EntityPrototype?.ID == prototypeId)
                count++;
        }

        return count;
    }

    private static void DeletePrototypeEntities(IEntityManager entMan, string prototypeId)
    {
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var metadata))
        {
            if (metadata.EntityPrototype?.ID == prototypeId)
                entMan.DeleteEntity(uid);
        }
    }
}
