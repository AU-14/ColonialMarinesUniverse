using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Atmos;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Atmos;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Storage.EntitySystems;
using Content.Shared._RMC14.Vendors;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class OctoberEighteenSurvivalRegressionTest : GameTest
{
    // bug 985: hands get searched before worn storage, so a dry tank in hand kept winning over the full spare
    // in the pack and swapping tanks on bosenmori just reconnected the empty one
    [Test]
    public async Task InternalsPickATankWithAirOverAnEmptyOne()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();
            var hands = SEntMan.System<SharedHandsSystem>();
            var storage = SEntMan.System<SharedStorageSystem>();
            var internals = SEntMan.System<SharedInternalsSystem>();

            var mob = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var mask = SEntMan.SpawnEntity("CMMaskGas", map.GridCoords);
            var pack = SEntMan.SpawnEntity("CMBackpackMarine", map.GridCoords);
            var empty = SEntMan.SpawnEntity("CMUFieldOxygenTank", map.GridCoords);
            var full = SEntMan.SpawnEntity("CMUFieldOxygenTankFilled", map.GridCoords);

            Assert.That(inventory.TryEquip(mob, mask, "mask", silent: true, force: true), Is.True);
            Assert.That(inventory.TryEquip(mob, pack, "back", silent: true, force: true), Is.True);
            Assert.That(storage.Insert(pack, full, out _, playSound: false), Is.True);
            Assert.That(hands.TryPickupAnyHand(mob, empty, checkActionBlocker: false), Is.True);

            Assert.That(internals.FindBestGasTank(mob)?.Owner, Is.EqualTo(full));

            // nothing with air left: the dry tank is still offered, same as before
            SEntMan.GetComponent<GasTankComponent>(full).Air.Clear();
            Assert.That(internals.FindBestGasTank(mob), Is.Not.Null);
        });
    }

    // bug 976: ghost role call-ins only carry their loadout, which on bosenmori meant no mask and no air
    [Test]
    public async Task GhostRoleOnABreathingMapGetsAMaskAndAFullTankOnce()
    {
        var map = await Pair.CreateTestMap();
        EntityUid mob = default;
        await Server.WaitPost(() =>
        {
            SEntMan.EnsureComponent<CMUBreathingRequiredMapComponent>(map.MapUid);
            mob = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.EnsureComponent<GhostRoleComponent>(mob);
        });

        await Pair.RunSeconds(4f);

        EntityUid tank = default;
        await Server.WaitAssertion(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();
            Assert.That(inventory.TryGetSlotEntity(mob, "mask", out var mask), Is.True);
            Assert.That(SEntMan.HasComponent<BreathToolComponent>(mask), Is.True);

            var best = SEntMan.System<SharedInternalsSystem>().FindBestGasTank(mob);
            Assert.That(best, Is.Not.Null);
            Assert.That(SharedInternalsSystem.CMUHasAir(best!.Value), Is.True);
            tank = best.Value.Owner;
            Assert.That(SEntMan.HasComponent<CMUCallInBreathingKitGivenComponent>(mob), Is.True);
        });

        // topping up again shouldn't stack a second kit on them
        await Server.WaitAssertion(() =>
        {
            var tanksBefore = SEntMan.EntityQuery<GasTankComponent>().Count();
            SEntMan.System<CMUMapBreathingSystem>()
                .GiveCallInKit(mob, SEntMan.GetComponent<CMUBreathingRequiredMapComponent>(map.MapUid));

            Assert.That(SEntMan.EntityQuery<GasTankComponent>().Count(), Is.EqualTo(tanksBefore));
            Assert.That(SEntMan.System<SharedInternalsSystem>().FindBestGasTank(mob)?.Owner, Is.EqualTo(tank));
        });
    }
}
