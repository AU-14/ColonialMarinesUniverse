using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Pulling;
using Content.Shared.DragDrop;
using Content.Shared.Humanoid;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

// CMU14: CM-SS13's GRAB_CARRY picks the victim up in one motion (living.dm:185-195), so a Yautja lifting an
// eligible xenomorph must not wait out FiremanCarriableComponent.Delay. Every other carrier keeps the do-after.
[TestFixture]
public sealed class YautjaCarryInstantTest
{
    private static async Task<bool> DraggedOntoSelf(string carrierProto, string targetProto)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid carrier = default;
        EntityUid target = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            carrier = entMan.SpawnEntity(carrierProto, map.GridCoords);
            target = entMan.SpawnEntity(targetProto, map.GridCoords);
            entMan.System<PullingSystem>().TryStartPull(carrier, target);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(5));

        await server.WaitPost(() =>
        {
            var toggle = new RMCPullToggleEvent();
            server.EntMan.EventBus.RaiseLocalEvent(carrier, ref toggle);
        });
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var drag = new DragDropDraggedEvent(carrier, carrier);
            server.EntMan.EventBus.RaiseLocalEvent(target, ref drag);
        });

        var carried = false;
        await server.WaitAssertion(() =>
        {
            // An excluded caste has no FiremanCarriable at all, which also counts as "not carried".
            carried = server.EntMan.TryGetComponent<FiremanCarriableComponent>(target, out var carriable) &&
                      carriable.BeingCarried;
        });

        await pair.CleanReturnAsync();
        return carried;
    }

    [Test]
    public async Task YautjaLiftsEligibleXenoWithoutWaiting()
    {
        Assert.That(await DraggedOntoSelf("CMUMobYautja", "CMXenoDrone"), Is.True,
            "a Yautja should start carrying an eligible xeno in the same tick as the drag");
    }

    [Test]
    public async Task NonYautjaStillWaitsForTheCarryDoAfter()
    {
        Assert.That(await DraggedOntoSelf("CMMobHuman", "CMMobHuman"), Is.False,
            "a marine carrying another marine still has to hold still for the do-after");
    }

    [Test]
    public async Task ExcludedXenoCasteCannotBeCarried()
    {
        Assert.That(await DraggedOntoSelf("CMUMobYautja", "CMXenoRavager"), Is.False,
            "a Ravager is MOB_SIZE_BIG and stays out of reach");
    }

    [Test]
    public async Task YautjaLiftsHumanWithoutWaiting()
    {
        Assert.That(await DraggedOntoSelf("CMUMobYautja", "CMMobHuman"), Is.True,
            "a Yautja should start carrying a human in the same tick as the drag");
    }
}
