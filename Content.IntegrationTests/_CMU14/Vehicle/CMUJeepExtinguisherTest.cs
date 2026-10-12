using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Fluids.Components;
using Content.Shared.Gravity;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item.ItemToggle;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// The extinguisher off the jeep's rack sprays like any other, from beside the jeep, from a seat or
/// on a planet whose ground is its map.
/// </summary>
[TestFixture]
public sealed class CMUJeepExtinguisherTest : GameTest
{
    private async Task<(EntityUid Jeep, EntityUid User)> SpawnJeep()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        EntityUid user = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -6; x <= 6; x++)
            {
                for (var y = -6; y <= 6; y++)
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

    private EntityUid HoldRackExtinguisher(EntityUid jeep, EntityUid user)
    {
        Assert.That(SEntMan.System<ItemSlotsSystem>().TryEject(jeep, CMUJeepSystem.ExtinguisherSlot, null, out var extinguisher), Is.True);
        Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, extinguisher!.Value), Is.True);
        Assert.That(SEntMan.System<ItemToggleSystem>().TryActivate(extinguisher.Value, user), Is.True, "The safety should come off.");
        return extinguisher.Value;
    }

    private void Spray(EntityUid extinguisher, EntityUid user, Vector2 offset)
    {
        var transform = SEntMan.System<SharedTransformSystem>();
        var from = transform.GetMapCoordinates(user);
        SEntMan.System<SpraySystem>().Spray((extinguisher, SEntMan.GetComponent<SprayComponent>(extinguisher)), user,
            from.Offset(offset));
    }

    [Test]
    public async Task SprayingBesideTheJeep()
    {
        var (jeep, user) = await SpawnJeep();
        EntityUid extinguisher = default;
        await Server.WaitPost(() => extinguisher = HoldRackExtinguisher(jeep, user));

        foreach (var offset in new[] { new Vector2(-2, 0), new Vector2(3, 0), new Vector2(0, 3) })
        {
            await Server.WaitPost(() => Spray(extinguisher, user, offset));
            await RunSeconds(2f);
        }

        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(jeep), Is.False));
    }

    [Test]
    public async Task SprayingFromTheDriversSeat()
    {
        var (jeep, user) = await SpawnJeep();
        EntityUid seat = default;
        await Server.WaitPost(() =>
        {
            seat = SEntMan.GetComponent<CMUVehicleSeatsComponent>(jeep).SeatEntities
                .First(s => SEntMan.GetComponent<CMUVehicleSeatComponent>(s).Driver);
            SEntMan.System<CMUVehicleSeatSystem>().ClickSeat(seat, user);
        });
        await RunSeconds(3f);

        EntityUid extinguisher = default;
        await Server.WaitPost(() => extinguisher = HoldRackExtinguisher(jeep, user));

        foreach (var offset in new[] { new Vector2(-2, 0), new Vector2(3, 0), new Vector2(0, 3), new Vector2(0, -3) })
        {
            await Server.WaitPost(() => Spray(extinguisher, user, offset));
            await RunSeconds(2f);
        }

        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(jeep), Is.False));
    }

    /// <summary>
    /// On a planet the ground is the map itself, saved with a dynamic body (as Stable Garrison is).
    /// The spray's pushback must leave it asleep: the physics step can't move a map.
    /// </summary>
    [Test]
    public async Task SprayingOnAPlanetLeavesTheGroundStill()
    {
        var floor = (await Pair.CreateTestMap()).Tile.Tile;
        EntityUid planet = default;
        EntityUid user = default;
        EntityUid extinguisher = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            planet = maps.CreateMap(out _);
            var grid = SEntMan.EnsureComponent<MapGridComponent>(planet);
            var gravity = SEntMan.EnsureComponent<GravityComponent>(planet);
            gravity.Enabled = true;
            gravity.Inherent = true;
            SEntMan.Dirty(planet, gravity);
            for (var x = -4; x <= 4; x++)
            {
                for (var y = -4; y <= 4; y++)
                {
                    maps.SetTile(planet, grid, new Vector2i(x, y), floor);
                }
            }

            var physics = SEntMan.System<SharedPhysicsSystem>();
            var body = SEntMan.EnsureComponent<PhysicsComponent>(planet);
            physics.SetBodyType(planet, BodyType.Dynamic, body: body);

            user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(planet, 0.5f, 0.5f));
            extinguisher = SEntMan.SpawnEntity("CMFireExtinguisherPortable", new EntityCoordinates(planet, 0.5f, 0.5f));
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, extinguisher), Is.True);
            Assert.That(SEntMan.System<ItemToggleSystem>().TryActivate(extinguisher, user), Is.True);
        });
        await RunTicksSync(5);

        await Server.WaitPost(() => Spray(extinguisher, user, new Vector2(3, 0)));
        await RunSeconds(2f);

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<PhysicsComponent>(planet).Awake, Is.False, "The planet's ground shouldn't wake up."));
    }
}
