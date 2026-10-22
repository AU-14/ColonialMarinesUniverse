using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Charges.Systems;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// Every rim design loops seamlessly as the wheels turn. Switched to its rim designs, the vehicle
/// spray painter sprays the design picked on a jeep's fitted wheel set in its colours, never the spare,
/// and won't on the body; painting the wheels afterwards only changes the design's base; the factory
/// design takes it off. The design stays on the wheel set, so it goes on with it to another jeep.
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

    private CMUVehicleWheelPaintComponent Wheels(EntityUid jeep, string slot = "wheel-1")
    {
        Assert.That(SEntMan.System<CMUVehiclePaintSystem>().TryGetWheelPaint(jeep, slot, out var wheels), Is.True);
        return wheels!.Value.Comp;
    }

    private void Spray(EntityUid user, EntityUid jeep, bool wheels)
    {
        var target = wheels
            ? SEntMan.GetComponent<CMUOpenVehicleComponent>(jeep).PartEntities
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
        Color spareBefore = default;
        var chargesBefore = 0;
        await Server.WaitPost(() =>
        {
            spareBefore = Wheels(jeep, "jeep-spare").Color;
            chargesBefore = SEntMan.System<SharedChargesSystem>().GetCurrentCharges(painter);
            SetPainter(painter, CMUVehicleSprayPainterMode.Design, "CMURimMesh", [Red, Gold, Black, White]);
            Spray(user, jeep, true);
        });
        await RunSeconds(7f);

        await Server.WaitAssertion(() =>
        {
            var wheels = Wheels(jeep);
            Assert.That(wheels.RimDesign?.Id, Is.EqualTo("CMURimMesh"));
            Assert.That(wheels.Color, Is.EqualTo(Red), "The design's base is the wheels' paint.");
            Assert.That(wheels.RimColors, Is.EqualTo(new List<Color> { Gold, Black, White }));
            var spare = Wheels(jeep, "jeep-spare");
            Assert.That(spare.RimDesign, Is.Null, "The spare keeps its own rims.");
            Assert.That(spare.Color, Is.EqualTo(spareBefore));
            Assert.That(SEntMan.GetComponent<CMUVehiclePaintComponent>(jeep).Color, Is.Null, "The body keeps its factory paint.");
            Assert.That(SEntMan.System<SharedChargesSystem>().GetCurrentCharges(painter), Is.LessThan(chargesBefore),
                "A rim design uses paint.");
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
            var wheels = Wheels(jeep);
            Assert.That(wheels.Color, Is.EqualTo(Blue));
            Assert.That(wheels.RimDesign?.Id, Is.EqualTo("CMURimTurbine"));
            Assert.That(wheels.RimColors, Is.EqualTo(new List<Color> { Gold, Black, White }));
        });
    }

    [Test]
    public async Task DesignModeLeavesTheBodyAlone()
    {
        var (jeep, user, painter) = await Setup();
        var chargesBefore = 0;
        await Server.WaitPost(() =>
        {
            chargesBefore = SEntMan.System<SharedChargesSystem>().GetCurrentCharges(painter);
            SetPainter(painter, CMUVehicleSprayPainterMode.Design, "CMURimMesh", [Red, Gold, Black, White]);
            Spray(user, jeep, false);
        });
        await RunSeconds(12f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUVehiclePaintComponent>(jeep).Color, Is.Null);
            Assert.That(Wheels(jeep).RimDesign, Is.Null);
            Assert.That(SEntMan.System<SharedChargesSystem>().GetCurrentCharges(painter), Is.EqualTo(chargesBefore),
                "Nothing sprayed, no paint used.");
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
            var wheels = Wheels(jeep);
            Assert.That(wheels.RimDesign, Is.Null);
            Assert.That(wheels.RimColors, Is.Empty);
            Assert.That(wheels.Color, Is.EqualTo(White));
        });
    }

    [Test]
    public async Task TheDesignGoesWithTheWheelSet()
    {
        var (jeep, user, painter) = await Setup();
        EntityUid other = default;
        await Server.WaitPost(() =>
        {
            SetPainter(painter, CMUVehicleSprayPainterMode.Design, "CMURimRally", [Red, Gold, Black, White]);
            Spray(user, jeep, true);
            var grid = SEntMan.GetComponent<TransformComponent>(jeep).ParentUid;
            other = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(grid, -3.5f, 0.5f));
        });
        await RunSeconds(7f);

        await Server.WaitAssertion(() =>
        {
            var containers = SEntMan.System<SharedContainerSystem>();
            var from = containers.GetContainer(jeep, "wheel-1");
            var to = containers.GetContainer(other, "wheel-1");
            var set = from.ContainedEntities.Single();
            SEntMan.DeleteEntity(to.ContainedEntities.Single());
            Assert.That(containers.Remove(set, from), Is.True);
            Assert.That(containers.Insert(set, to), Is.True);

            var wheels = Wheels(other);
            Assert.That(wheels.RimDesign?.Id, Is.EqualTo("CMURimRally"), "The rims stay on the wheel set.");
            Assert.That(wheels.Color, Is.EqualTo(Red));
        });
    }
}
