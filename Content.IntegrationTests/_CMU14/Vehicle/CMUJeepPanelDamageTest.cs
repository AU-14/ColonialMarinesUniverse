using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// Hits wear the jeep's panel on the side they land; a welder mends a worn panel, but one worn right
/// down is wrecked past welding.
/// </summary>
[TestFixture]
public sealed class CMUJeepPanelDamageTest : GameTest
{
    private async Task<(TestMapData Map, EntityUid Jeep)> SpawnJeep()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
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
        });
        await RunTicksSync(5);
        return (map, jeep);
    }

    private CMUVehiclePartIntegrityComponent Panel(EntityUid jeep, string slot)
    {
        var item = SEntMan.System<SharedContainerSystem>().GetContainer(jeep, slot).ContainedEntities.Single();
        return SEntMan.GetComponent<CMUVehiclePartIntegrityComponent>(item);
    }

    private EntityUid PartEntity(EntityUid jeep, string part)
    {
        return SEntMan.GetComponent<CMUOpenVehicleComponent>(jeep).PartEntities
            .First(p => SEntMan.GetComponent<CMUVehiclePartComponent>(p).Part == part);
    }

    [Test]
    public async Task HitsWearThePanelOnTheirSide()
    {
        var (map, jeep) = await SpawnJeep();
        await Server.WaitAssertion(() =>
        {
            var slots = new[] { "jeep-hood", "jeep-door-driver", "jeep-door-passenger", "jeep-tailgate" };
            var whole = slots.ToDictionary(s => s, s => Panel(jeep, s).Integrity);
            var damage = new DamageSpecifier();
            damage.DamageDict["Structural"] = 10;
            var damageable = SEntMan.System<DamageableSystem>();

            // Facing south: the hood is towards -y, the driver's door +x, the passenger's -x.
            var hits = new (string Slot, EntityCoordinates From)[]
            {
                ("jeep-hood", new EntityCoordinates(map.Grid, 0.5f, -2.5f)),
                ("jeep-door-driver", new EntityCoordinates(map.Grid, 2.5f, 0.5f)),
                ("jeep-door-passenger", new EntityCoordinates(map.Grid, -1.5f, 0.5f)),
                ("jeep-tailgate", new EntityCoordinates(map.Grid, 0.5f, 3.5f)),
            };
            foreach (var (slot, from) in hits)
            {
                var before = slots.ToDictionary(s => s, s => Panel(jeep, s).Integrity);
                damageable.TryChangeDamage(jeep, damage, origin: SEntMan.SpawnEntity(null, from));
                foreach (var other in slots)
                {
                    if (other == slot)
                        Assert.That(Panel(jeep, other).Integrity, Is.LessThan(before[other]), $"A hit from {slot}'s side wears it.");
                    else
                        Assert.That(Panel(jeep, other).Integrity, Is.EqualTo(before[other]), $"A hit from {slot}'s side leaves {other} alone.");
                }
            }

            Assert.That(whole.Values, Is.All.GreaterThan(0f));
        });
    }

    [Test]
    public async Task AWelderMendsAWornPanelButNotAWreckedOne()
    {
        var (map, jeep) = await SpawnJeep();
        EntityUid user = default;
        EntityUid welder = default;
        var worn = 0f;
        await Server.WaitPost(() =>
        {
            user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1.8f, 0.5f));
            welder = SEntMan.SpawnEntity("CMWelder", new EntityCoordinates(map.Grid, 1.8f, 0.5f));
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, welder), Is.True);
            Assert.That(SEntMan.System<ItemToggleSystem>().TryActivate(welder, user), Is.True, "The welder should light.");

            var door = Panel(jeep, "jeep-door-driver");
            door.Integrity = door.MaxIntegrity / 4f;
            worn = door.Integrity;
            SEntMan.EventBus.RaiseLocalEvent(PartEntity(jeep, "door_driver"),
                new InteractUsingEvent(user, welder, PartEntity(jeep, "door_driver"), SEntMan.GetComponent<TransformComponent>(PartEntity(jeep, "door_driver")).Coordinates));
        });
        await RunSeconds(3f);
        await Server.WaitAssertion(() =>
            Assert.That(Panel(jeep, "jeep-door-driver").Integrity, Is.GreaterThan(worn), "A welder pass mends a worn door."));

        await Server.WaitPost(() =>
        {
            Panel(jeep, "jeep-door-driver").Integrity = 0f;
            SEntMan.EventBus.RaiseLocalEvent(PartEntity(jeep, "door_driver"),
                new InteractUsingEvent(user, welder, PartEntity(jeep, "door_driver"), SEntMan.GetComponent<TransformComponent>(PartEntity(jeep, "door_driver")).Coordinates));
        });
        await RunSeconds(3f);
        await Server.WaitAssertion(() =>
            Assert.That(Panel(jeep, "jeep-door-driver").Integrity, Is.Zero, "A wrecked door is past welding."));
    }
}
