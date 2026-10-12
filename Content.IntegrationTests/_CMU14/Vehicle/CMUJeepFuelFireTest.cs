using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// The jeep's tank is a fuel solution filled from a can through the fuel door, its racks start full and
/// only take items through the rack itself, and water on a burning engine puts it out.
/// </summary>
[TestFixture]
public sealed class CMUJeepFuelFireTest : GameTest
{
    private async Task<(EntityUid Jeep, EntityUid User)> SpawnJeep()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        EntityUid user = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -4; x <= 4; x++)
            {
                for (var y = -4; y <= 4; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            jeep = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1.45f, 0.75f));
        });
        await RunTicksSync(5);
        return (jeep, user);
    }

    private Solution Contents(EntityUid uid, string solution)
    {
        Assert.That(SEntMan.System<SharedSolutionContainerSystem>().TryGetSolution(uid, solution, out _, out var contents), Is.True);
        return contents!;
    }

    private EntityUid Rack(EntityUid jeep, string slot)
    {
        Assert.That(SEntMan.System<SharedContainerSystem>().TryGetContainer(jeep, slot, out var container), Is.True);
        return container!.ContainedEntities.Single();
    }

    private void Click(EntityUid user, EntityUid target)
    {
        SEntMan.System<SharedInteractionSystem>()
            .UserInteraction(user, SEntMan.GetComponent<TransformComponent>(target).Coordinates, target);
    }

    private EntityUid HoldRackCan(EntityUid jeep, EntityUid user)
    {
        Assert.That(SEntMan.System<ItemSlotsSystem>().TryEject(jeep, CMUJeepSystem.JerryCanSlot, null, out var can), Is.True);
        Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, can!.Value), Is.True);
        return can.Value;
    }

    [Test]
    public async Task RacksStartFull()
    {
        var (jeep, _) = await SpawnJeep();
        await Server.WaitAssertion(() =>
        {
            var spray = Contents(Rack(jeep, "jeep-extinguisher"), "spray");
            Assert.That(spray.GetTotalPrototypeQuantity("Water").Float(), Is.EqualTo(30f));
            Assert.That(spray.AvailableVolume.Float(), Is.Zero);

            var fuel = Contents(Rack(jeep, CMUJeepSystem.JerryCanSlot), "fuel");
            Assert.That(fuel.GetTotalPrototypeQuantity("WeldingFuel").Float(), Is.EqualTo(60f));
            Assert.That(fuel.AvailableVolume.Float(), Is.Zero);

            Assert.That(Contents(jeep, "fuel").GetTotalPrototypeQuantity("WeldingFuel").Float(), Is.EqualTo(45f));
        });
    }

    [Test]
    public async Task FuelDoorMovesFuelFromTheCan()
    {
        var (jeep, user) = await SpawnJeep();
        EntityUid can = default;
        EntityUid door = default;
        await Server.WaitPost(() =>
        {
            can = HoldRackCan(jeep, user);
            door = SEntMan.GetComponent<CMUJeepComponent>(jeep).PartEntities
                .First(part => SEntMan.GetComponent<CMUVehiclePartComponent>(part).Part == "fuel_door");
            var comp = SEntMan.GetComponent<CMUJeepComponent>(jeep);
            comp.OpenPanels.Add("fuel_door");
            SEntMan.Dirty(jeep, comp);
            Click(user, door);
        });
        await RunSeconds(11f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Contents(jeep, "fuel").GetTotalPrototypeQuantity("WeldingFuel").Float(), Is.EqualTo(60f).Within(0.01f));
            Assert.That(Contents(can, "fuel").GetTotalPrototypeQuantity("WeldingFuel").Float(), Is.EqualTo(45f).Within(0.01f));
            Assert.That(SEntMan.System<SharedHandsSystem>().IsHolding(user, can), Is.True, "The can stays in hand.");
            Assert.That(SEntMan.System<SharedContainerSystem>().TryGetContainer(jeep, CMUJeepSystem.JerryCanSlot, out var rack) &&
                        rack.ContainedEntities.Count == 0, Is.True, "The rack stays empty.");
        });
    }

    [Test]
    public async Task ClickingTheJeepKeepsTheCanInHand()
    {
        var (jeep, user) = await SpawnJeep();
        EntityUid can = default;
        await Server.WaitPost(() =>
        {
            can = HoldRackCan(jeep, user);
            Click(user, jeep);
        });
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<SharedHandsSystem>().IsHolding(user, can), Is.True));
    }

    [Test]
    public async Task WaterPutsOutTheEngineFire()
    {
        var (jeep, _) = await SpawnJeep();
        await Server.WaitPost(() =>
        {
            var comp = SEntMan.GetComponent<CMUJeepComponent>(jeep);
            comp.EngineIntegrity = 0f;
            SEntMan.Dirty(jeep, comp);
        });
        await RunSeconds(1f);

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUJeepComponent>(jeep).Burning, Is.True);
            var water = new Solution("Water", 2);
            SEntMan.System<ReactiveSystem>().DoEntityReaction(jeep, water, ReactionMethod.Touch);
        });
        await RunSeconds(8f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(jeep), Is.False, "Put out in time, the jeep shouldn't blow up.");
            var comp = SEntMan.GetComponent<CMUJeepComponent>(jeep);
            Assert.That(comp.Burning, Is.False);
            Assert.That(comp.EngineIntegrity, Is.EqualTo(1f));
        });
    }
}
