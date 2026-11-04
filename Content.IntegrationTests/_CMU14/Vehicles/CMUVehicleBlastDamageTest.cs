using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Containers.ItemSlots;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Vehicles;

// rockets and ieds barely scratched vehicles: the blast never got the rocket's anti-vehicle multiplier
// and was then shrunk again by every module's small-arms multiplier
public sealed class CMUVehicleBlastDamageTest : GameTest
{
    [TestCase("RMCProjectileRocket84mm", 0.06f, 0.25f)]
    [TestCase("AU14IED", 0.12f, 0.4f)]
    public async Task BlastNextToAnApcTakesARealBite(string explosive, float minFraction, float maxFraction)
    {
        var map = await Pair.CreateTestMap();
        EntityUid apc = default;
        float before = 0;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            map.Grid.Comp.CanSplit = false;
            for (var x = -12; x < 13; x++)
            for (var y = -12; y < 13; y++)
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);

            apc = SEntMan.SpawnEntity("VehicleAPC", new EntityCoordinates(map.Grid.Owner, new Vector2(0.5f, 0.5f)));
            var slots = Server.System<ItemSlotsSystem>();
            foreach (var (slot, module) in new[]
                     {
                         ("primary", "RMCAPCDualCannon"), ("secondary", "RMCAPCFrontCannon"),
                         ("support", "RMCAPCFlareLauncher"), ("wheel-1", "RMCAPCWheel"),
                     })
            {
                var item = SEntMan.SpawnEntity(module, new EntityCoordinates(map.Grid.Owner, new Vector2(-8.5f, 0.5f)));
                Assert.That(slots.TryInsert(apc, slot, item, null), Is.True, slot);
            }
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            before = Integrity(apc);
            Assert.That(before, Is.GreaterThan(0f));
            var bomb = SEntMan.SpawnEntity(explosive, new EntityCoordinates(map.Grid.Owner, new Vector2(2.5f, 0.5f)));
            Server.System<TriggerSystem>().Trigger(bomb);
        });
        await Pair.RunTicksSync(60);
        await Server.WaitAssertion(() =>
        {
            var lost = (before - Integrity(apc)) / before;
            TestContext.Out.WriteLine($"{explosive}: {lost:P1} of {before} APC integrity");
            Assert.That(lost, Is.InRange(minFraction, maxFraction));
        });
    }

    private float Integrity(EntityUid vehicle)
    {
        return Server.System<VehicleTopologySystem>().GetMountedSlots(vehicle)
            .Where(slot => slot.Item != null)
            .Select(slot => slot.Item!.Value)
            .Distinct()
            .Sum(item => SEntMan.TryGetComponent(item, out HardpointIntegrityComponent? part) ? part.Integrity : 0f);
    }
}
