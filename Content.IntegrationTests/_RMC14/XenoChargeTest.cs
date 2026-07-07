using System.Numerics;
using Content.Shared._RMC14.Xenonids.Charge;
using Content.Shared.Actions.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class XenoChargeTest
{
    [Test]
    public async Task CrusherChargeWindupDoesNotCancelWhenMoved()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid crusher = default;
        EntityUid action = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var transform = entMan.System<SharedTransformSystem>();

                crusher = entMan.SpawnEntity("RMCXenoCrusher", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));

                var actionEnt = SpawnAction(entMan);
                action = actionEnt.Owner;

                var charge = new XenoChargeActionEvent
                {
                    Performer = crusher,
                    Action = actionEnt,
                    Target = map.GridCoords.Offset(new Vector2(8, 0.5f)),
                };
                entMan.EventBus.RaiseLocalEvent(crusher, charge);

                Assert.That(charge.Handled, Is.True);

                transform.SetCoordinates(crusher, map.GridCoords.Offset(new Vector2(1, 0.5f)));
            });

            await pair.RunSeconds(0.65f);

            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.HasComponent<XenoChargingComponent>(crusher), Is.True);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                if (entMan.EntityExists(action))
                    entMan.DeleteEntity(action);

                if (entMan.EntityExists(crusher))
                    entMan.DeleteEntity(crusher);
            });

            await pair.CleanReturnAsync();
        }
    }

    private static Entity<ActionComponent> SpawnAction(IEntityManager entMan)
    {
        var action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
        return (action, entMan.EnsureComponent<ActionComponent>(action));
    }
}
