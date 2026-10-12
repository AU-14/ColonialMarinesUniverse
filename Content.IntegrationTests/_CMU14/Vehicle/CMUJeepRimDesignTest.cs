using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Charges.Systems;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// Every rim design loops seamlessly as the wheels turn. Switched to its rim designs, the vehicle
/// spray painter sprays the design picked on a jeep's wheels in its colours, and won't on the body;
/// painting the wheels afterwards only changes the design's base; the factory design takes it off.
/// </summary>
[TestFixture]
public sealed class CMUJeepRimDesignTest : GameTest
{
    private static readonly Color Red = Color.FromHex("#D7262E");
    private static readonly Color Gold = Color.FromHex("#C9A23A");
    private static readonly Color Black = Color.FromHex("#1E1F21");
    private static readonly Color White = Color.FromHex("#E6E9EC");
    private static readonly Color Blue = Color.FromHex("#2E4F8A");

    private async Task<TestMapData> Floor()
    {
        var map = await Pair.CreateTestMap();
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
        });
        return map;
    }

    private async Task<(EntityUid Jeep, EntityUid User, EntityUid Painter)> Setup()
    {
        var map = await Floor();
        EntityUid jeep = default;
        EntityUid user = default;
        EntityUid painter = default;
        await Server.WaitPost(() =>
        {
            jeep = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1.45f, 0.75f));
            painter = SEntMan.SpawnEntity("CMUVehicleSprayPainter", new EntityCoordinates(map.Grid, 1.45f, 0.75f));
        });
        await RunTicksSync(5);
        await Server.WaitPost(() => Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, painter), Is.True));
        return (jeep, user, painter);
    }

    private void SetPainter(EntityUid painter, CMUVehicleSprayPainterMode mode, string design, List<Color> colors, Color? paint = null)
    {
        var comp = SEntMan.GetComponent<CMUVehicleSprayPainterComponent>(painter);
        comp.Mode = mode;
        comp.Design = design;
        comp.DesignColors = colors;
        if (paint is { } color)
            comp.Color = color;

        SEntMan.Dirty(painter, comp);
    }

    private void Spray(EntityUid user, EntityUid jeep, bool wheels)
    {
        var target = wheels
            ? SEntMan.GetComponent<CMUJeepComponent>(jeep).PartEntities
                .First(part => SEntMan.GetComponent<CMUVehiclePartComponent>(part).Part == "wheels")
            : jeep;
        SEntMan.System<SharedInteractionSystem>()
            .UserInteraction(user, SEntMan.GetComponent<TransformComponent>(target).Coordinates, target);
    }

    [Test]
    public async Task EveryDesignLoopsAsTheWheelsTurn()
    {
        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            var designs = prototypes.EnumeratePrototypes<CMURimDesignPrototype>().ToList();
            Assert.That(designs, Has.Count.GreaterThanOrEqualTo(12));
            Assert.That(designs.Count(d => d.Factory), Is.EqualTo(1), "One design is the plain rims.");
            foreach (var design in designs)
            {
                for (var u = -CMURimDesigns.Radius; u <= CMURimDesigns.Radius; u++)
                {
                    for (var v = -CMURimDesigns.Radius; v <= CMURimDesigns.Radius; v++)
                    {
                        if (!CMURimDesigns.OnRim(u, v))
                            continue;

                        Assert.That(CMURimDesigns.Sample(design, u, v, CMURimDesigns.Frames),
                            Is.EqualTo(CMURimDesigns.Sample(design, u, v, 0)),
                            $"{design.ID} at ({u}, {v}) jumps where its frame loop starts again.");
                    }
                }
            }
        });
    }

    [Test]
    public async Task DesignModeSpraysTheDesignOnTheWheels()
    {
        var (jeep, user, painter) = await Setup();
        await Server.WaitPost(() =>
        {
            SetPainter(painter, CMUVehicleSprayPainterMode.Design, "CMURimMesh", [Red, Gold, Black, White]);
            Spray(user, jeep, true);
        });
        await RunSeconds(7f);

        await Server.WaitAssertion(() =>
        {
            var paint = SEntMan.GetComponent<CMUVehiclePaintComponent>(jeep);
            Assert.That(paint.RimDesign?.Id, Is.EqualTo("CMURimMesh"));
            Assert.That(paint.WheelColor, Is.EqualTo(Red), "The design's base is the wheels' paint.");
            Assert.That(paint.RimColors, Is.EqualTo(new List<Color> { Gold, Black, White }));
            Assert.That(paint.Color, Is.Null, "The body keeps its factory paint.");
            Assert.That(SEntMan.System<SharedChargesSystem>().GetCurrentCharges(painter), Is.EqualTo(28));
        });
    }

    [Test]
    public async Task PaintOnDesignedWheelsOnlyChangesTheBase()
    {
        var (jeep, user, painter) = await Setup();
        await Server.WaitPost(() =>
        {
            SetPainter(painter, CMUVehicleSprayPainterMode.Design, "CMURimTurbine", [Red, Gold, Black, White]);
            Spray(user, jeep, true);
        });
        await RunSeconds(7f);
        await Server.WaitPost(() =>
        {
            SetPainter(painter, CMUVehicleSprayPainterMode.Paint, "CMURimTurbine", [Red, Gold, Black, White], Blue);
            Spray(user, jeep, true);
        });
        await RunSeconds(5f);

        await Server.WaitAssertion(() =>
        {
            var paint = SEntMan.GetComponent<CMUVehiclePaintComponent>(jeep);
            Assert.That(paint.WheelColor, Is.EqualTo(Blue));
            Assert.That(paint.RimDesign?.Id, Is.EqualTo("CMURimTurbine"));
            Assert.That(paint.RimColors, Is.EqualTo(new List<Color> { Gold, Black, White }));
        });
    }

    [Test]
    public async Task DesignModeLeavesTheBodyAlone()
    {
        var (jeep, user, painter) = await Setup();
        await Server.WaitPost(() =>
        {
            SetPainter(painter, CMUVehicleSprayPainterMode.Design, "CMURimMesh", [Red, Gold, Black, White]);
            Spray(user, jeep, false);
        });
        await RunSeconds(12f);

        await Server.WaitAssertion(() =>
        {
            var paint = SEntMan.GetComponent<CMUVehiclePaintComponent>(jeep);
            Assert.That(paint.Color, Is.Null);
            Assert.That(paint.RimDesign, Is.Null);
            Assert.That(SEntMan.System<SharedChargesSystem>().GetCurrentCharges(painter), Is.EqualTo(30));
        });
    }

    [Test]
    public async Task TheFactoryDesignTakesADesignOff()
    {
        var (jeep, user, painter) = await Setup();
        await Server.WaitPost(() =>
        {
            SetPainter(painter, CMUVehicleSprayPainterMode.Design, "CMURimRally", [Red, Gold, Black, White]);
            Spray(user, jeep, true);
        });
        await RunSeconds(7f);
        await Server.WaitPost(() =>
        {
            SetPainter(painter, CMUVehicleSprayPainterMode.Design, "CMURimFactory", [White, Gold, Black, White]);
            Spray(user, jeep, true);
        });
        await RunSeconds(7f);

        await Server.WaitAssertion(() =>
        {
            var paint = SEntMan.GetComponent<CMUVehiclePaintComponent>(jeep);
            Assert.That(paint.RimDesign, Is.Null);
            Assert.That(paint.RimColors, Is.Empty);
            Assert.That(paint.WheelColor, Is.EqualTo(White));
        });
    }
}
