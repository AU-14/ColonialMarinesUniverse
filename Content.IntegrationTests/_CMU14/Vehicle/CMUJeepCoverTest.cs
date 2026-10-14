using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.BlurredVision;
using Content.Shared.Body.Part;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Maps;
using Content.Shared.Vehicle.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// A shut door stops shots from its side at the legs of the rider beside it, the bodywork taking
/// them; open, or at the chest, it doesn't. The raised windshield catches shots from ahead at the
/// front riders' heads and wears by them. Down, it lets grit into their eyes at speed over dirt.
/// </summary>
[TestFixture]
public sealed class CMUJeepCoverTest : GameTest
{
    private async Task<(TestMapData Map, EntityUid Jeep, EntityUid Driver)> SeatedJeep()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        EntityUid driver = default;
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
            driver = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1.45f, 0.75f));
        });
        await RunTicksSync(5);

        EntityUid seat = default;
        await Server.WaitPost(() =>
        {
            seat = SEntMan.GetComponent<CMUVehicleSeatsComponent>(jeep).SeatEntities
                .First(s => SEntMan.GetComponent<CMUVehicleSeatComponent>(s).Driver);
            SEntMan.System<CMUVehicleSeatSystem>().ClickSeat(seat, driver);
        });
        await RunSeconds(3f);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<BuckleComponent>(driver).BuckledTo, Is.EqualTo(seat), "The driver should be seated."));

        return (map, jeep, driver);
    }

    /// <summary>
    /// A rifle round from the shooter at the driver's given part; whether it got through.
    /// </summary>
    private bool Shoot(EntityUid driver, EntityUid shooter, BodyPartType part)
    {
        // A shooter who aims rolls for the part and can miss it; one who doesn't hits it.
        SEntMan.RemoveComponent<BodyZoneTargetingComponent>(shooter);
        SEntMan.System<SharedHitLocationSystem>().SetForcedHit(driver, part);
        var piercing = Server.ResolveDependency<IPrototypeManager>().Index<DamageTypePrototype>("Piercing");
        return SEntMan.System<DamageableSystem>().TryChangeDamage(driver, new DamageSpecifier(piercing, 10),
            origin: shooter, impact: DamageImpact.Projectile) != null;
    }

    [Test]
    public async Task ShutDoorStopsLegShotsFromItsSide()
    {
        var (map, jeep, driver) = await SeatedJeep();
        await Server.WaitAssertion(() =>
        {
            // Facing south, the driver's door is on the east side.
            var east = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 4.5f, 0.75f));
            var west = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, -3.5f, 0.75f));
            var jeepComp = SEntMan.GetComponent<CMUOpenVehicleComponent>(jeep);

            Assert.That(Shoot(driver, east, BodyPartType.Leg), Is.False, "The shut door takes a leg shot from its side.");
            Assert.That(Shoot(driver, east, BodyPartType.Torso), Is.True, "It doesn't reach the chest.");
            Assert.That(Shoot(driver, west, BodyPartType.Leg), Is.True, "It only covers its own side.");

            jeepComp.OpenPanels.Add("door_driver");
            Assert.That(Shoot(driver, east, BodyPartType.Leg), Is.True, "An open door stops nothing.");
        });
    }

    [Test]
    public async Task RaisedWindshieldCatchesShotsFromAheadAndWears()
    {
        var (map, jeep, driver) = await SeatedJeep();
        float before = 0;
        await Server.WaitAssertion(() =>
        {
            SEntMan.GetComponent<CMUVehicleCoverComponent>(jeep).WindshieldChance = 1f;
            var windshield = Windshield(jeep);
            before = SEntMan.GetComponent<CMUVehiclePartIntegrityComponent>(windshield).Integrity;

            // Facing south, ahead is south.
            var ahead = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1f, -4.5f));
            Assert.That(Shoot(driver, ahead, BodyPartType.Head), Is.False, "The windshield catches a head shot from ahead.");
            Assert.That(Shoot(driver, ahead, BodyPartType.Leg), Is.True, "It doesn't reach the legs.");

            SEntMan.GetComponent<CMUOpenVehicleComponent>(jeep).WindshieldDown = true;
            Assert.That(Shoot(driver, ahead, BodyPartType.Head), Is.True, "Folded down, it stops nothing.");
        });
        await RunTicksSync(2);

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<CMUVehiclePartIntegrityComponent>(Windshield(jeep)).Integrity, Is.LessThan(before),
                "The caught shot wears the glass."));
    }

    [Test]
    public async Task LoweredWindshieldLetsGritIntoTheEyesOverDirt()
    {
        var (map, jeep, driver) = await SeatedJeep();
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var dirt = Server.ResolveDependency<ITileDefinitionManager>()["CMPlanetDirt"];
            for (var x = -6; x <= 6; x++)
            {
                for (var y = -6; y <= 6; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), new Tile(dirt.TileId));
                }
            }

            SEntMan.GetComponent<CMUVehicleCoverComponent>(jeep).GritChance = 1000f;
            SEntMan.GetComponent<CMUOpenVehicleComponent>(jeep).WindshieldDown = true;
            var mover = SEntMan.GetComponent<GridVehicleMoverComponent>(jeep);
            mover.CurrentSpeed = mover.MaxSpeed;
        });
        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.HasComponent<RMCBlindedComponent>(driver), Is.True, "Grit should fly into the driver's eyes."));
    }

    private EntityUid Windshield(EntityUid jeep)
    {
        var slots = SEntMan.System<Content.Shared.Containers.ItemSlots.ItemSlotsSystem>();
        Assert.That(slots.TryGetSlot(jeep, "jeep-windshield", out var slot), Is.True);
        return slot!.Item!.Value;
    }
}
