using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Charges.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Lathe;
using Content.Shared.Research.Prototypes;
using Content.Shared._RMC14.Requisitions.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// A jeep crate opens into the jeep on its wheels with the spare in the hatch, the colony's crates come in a
/// showroom paint and only as cargo and medical jeeps, the autolathe prints the vehicle spray painter,
/// the painter on the wheels or a seat repaints only them, and space cleaner takes a jeep's dirt,
/// blood and crayon off.
/// </summary>
[TestFixture]
public sealed class CMUJeepKitAndGrimeTest : GameTest
{
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

    [Test]
    public async Task CrateOpensIntoAJeepOnItsWheelsWithTheSpareInTheHatch()
    {
        var map = await Floor();
        EntityUid crate = default;
        EntityUid user = default;
        await Server.WaitPost(() =>
        {
            crate = SEntMan.SpawnEntity("CMUCrateJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            user = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 2.5f));
        });
        await RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            var comp = SEntMan.GetComponent<CMUVehicleCrateComponent>(crate);
            Assert.That(SEntMan.System<CMUVehicleCrateSystem>().TryStartOpening((crate, comp), user), Is.True);
        });
        await RunSeconds(4f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(crate), Is.True, "The crate should give way to the jeep.");
            var jeep = SEntMan.EntityQuery<CMUOpenVehicleComponent>(true).Select(c => c.Owner).Single();
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(jeep).EntityPrototype?.ID, Is.EqualTo("CMUVehicleJeepCargo"));

            var containers = SEntMan.System<SharedContainerSystem>();
            Assert.That(containers.GetContainer(jeep, "wheel-1").ContainedEntities, Has.Count.EqualTo(1), "The jeep comes on its wheels.");
            var hatch = containers.GetContainer(jeep, CMUOpenVehicleSystem.SpareSlot).ContainedEntities;
            Assert.That(hatch, Has.Count.EqualTo(1), "The wheel set rides in the rear hatch.");
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(hatch[0]).EntityPrototype?.ID, Is.EqualTo("CMUJeepWheel"));
            Assert.That(SEntMan.GetComponent<CMUVehicleIgnitionComponent>(jeep).KeySlot.HasItem, Is.True, "The key comes in.");
            Assert.That(SEntMan.EntityQuery<CMUVehicleCrateRemainsComponent>(true).Count(), Is.EqualTo(2), "The floor and the falling walls stand where the crate was.");
        });
    }

    [Test]
    public async Task ColonyCrateOpensIntoAPaintedJeep()
    {
        var map = await Floor();
        EntityUid crate = default;
        List<Color> paints = new();
        await Server.WaitPost(() =>
        {
            crate = SEntMan.SpawnEntity("CMUCrateJeepMedicalColony", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            paints = SEntMan.GetComponent<CMUVehicleCrateComponent>(crate).Paints.ToList();
        });
        await RunTicksSync(5);

        await Server.WaitPost(() =>
            SEntMan.System<CMUVehicleCrateSystem>().Open((crate, SEntMan.GetComponent<CMUVehicleCrateComponent>(crate))));
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(paints, Has.Count.EqualTo(5));
            var jeep = SEntMan.EntityQuery<CMUOpenVehicleComponent>(true).Select(c => c.Owner).Single();
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(jeep).EntityPrototype?.ID, Is.EqualTo("CMUVehicleJeepMedical"));
            var color = SEntMan.GetComponent<CMUVehiclePaintComponent>(jeep).Color;
            Assert.That(color, Is.Not.Null);
            Assert.That(paints, Does.Contain(color!.Value));
        });
    }

    [Test]
    public async Task ColonyConsoleOnlyListsCargoAndMedicalKits()
    {
        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            Assert.That(prototypes.Index<EntityPrototype>("CMASRSConsoleColony")
                .TryComp<RequisitionsComputerComponent>(out var console, SEntMan.ComponentFactory), Is.True);
            var kits = console!.Categories
                .SelectMany(category => category.Entries)
                .Select(entry => entry.Crate.Id)
                .Where(crate => crate.StartsWith("CMUCrateJeep"))
                .ToList();
            Assert.That(kits, Is.EquivalentTo(new[] { "CMUCrateJeepCargoColony", "CMUCrateJeepMedicalColony" }));
        });
    }

    [Test]
    public async Task AutolathePrintsTheVehicleSprayPainter()
    {
        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            Assert.That(prototypes.Index<EntityPrototype>("CMAutolathe")
                .TryComp<LatheComponent>(out var lathe, SEntMan.ComponentFactory), Is.True);
            var recipes = lathe!.StaticPacks.SelectMany(pack => prototypes.Index(pack).Recipes);
            Assert.That(recipes, Does.Contain(new ProtoId<LatheRecipePrototype>("CMUVehicleSprayPainter")));
        });
    }

    [Test]
    public async Task SprayPainterOnTheWheelsPaintsOnlyThem()
    {
        var map = await Floor();
        var red = Color.FromHex("#D7262E");
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

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.System<CMUVehiclePaintSystem>().TryGetWheelPaint(jeep, "wheel-1", out var fitted), Is.True);
            Assert.That(fitted!.Value.Comp.Color, Is.EqualTo(Color.FromHex("#C8CDD2")), "Wheels come in grey metal.");

            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, painter), Is.True);
            var comp = SEntMan.GetComponent<CMUVehicleSprayPainterComponent>(painter);
            comp.Color = red;
            SEntMan.Dirty(painter, comp);

            var wheels = SEntMan.GetComponent<CMUOpenVehicleComponent>(jeep).PartEntities
                .First(part => SEntMan.GetComponent<CMUVehiclePartComponent>(part).Part == "wheels");
            SEntMan.System<SharedInteractionSystem>()
                .UserInteraction(user, SEntMan.GetComponent<TransformComponent>(wheels).Coordinates, wheels);
        });
        await RunSeconds(5f);

        await Server.WaitAssertion(() =>
        {
            var paint = SEntMan.GetComponent<CMUVehiclePaintComponent>(jeep);
            Assert.That(SEntMan.System<CMUVehiclePaintSystem>().TryGetWheelPaint(jeep, "wheel-1", out var fitted), Is.True);
            Assert.That(fitted!.Value.Comp.Color, Is.EqualTo(red));
            Assert.That(SEntMan.System<CMUVehiclePaintSystem>().TryGetWheelPaint(jeep, "jeep-spare", out var spare), Is.True);
            Assert.That(spare!.Value.Comp.Color, Is.EqualTo(Color.FromHex("#C8CDD2")), "The spare keeps its own paint.");
            Assert.That(paint.Color, Is.Null, "The body keeps its factory paint.");
            Assert.That(SEntMan.System<SharedChargesSystem>().GetCurrentCharges(painter), Is.EqualTo(29));
        });
    }

    [Test]
    public async Task SprayPainterOnASeatPaintsOnlyTheSeats()
    {
        var map = await Floor();
        var red = Color.FromHex("#D7262E");
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

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, painter), Is.True);
            var comp = SEntMan.GetComponent<CMUVehicleSprayPainterComponent>(painter);
            comp.Color = red;
            SEntMan.Dirty(painter, comp);

            var seat = SEntMan.EntityQuery<CMUVehiclePartComponent>(true)
                .First(part => part.Vehicle == jeep && part.Part == "seat_driver").Owner;
            SEntMan.System<SharedInteractionSystem>()
                .UserInteraction(user, SEntMan.GetComponent<TransformComponent>(seat).Coordinates, seat);
        });
        await RunSeconds(5f);

        await Server.WaitAssertion(() =>
        {
            var paint = SEntMan.GetComponent<CMUVehiclePaintComponent>(jeep);
            Assert.That(paint.SeatColor, Is.EqualTo(red));
            Assert.That(paint.Color, Is.Null, "The body keeps its factory paint.");
            Assert.That(paint.InteriorColor, Is.Null, "The tub inside keeps its factory paint.");
        });
    }

    [Test]
    public async Task SpaceCleanerTakesGrimeOff()
    {
        var map = await Floor();
        EntityUid jeep = default;
        await Server.WaitPost(() =>
        {
            jeep = SEntMan.SpawnEntity("CMUVehicleJeepTransport", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
        });
        await RunTicksSync(5);

        await Server.WaitPost(() =>
        {
            var grime = SEntMan.System<CMUVehicleGrimeSystem>();
            var comp = SEntMan.GetComponent<CMUVehicleGrimeComponent>(jeep);
            grime.SetDirt((jeep, comp), 0.8f);
            for (var i = 0; i < 6; i++)
            {
                grime.AddBlood(jeep, Color.DarkRed, new Vector3(-6, -10, 13), "up", true);
            }

            Assert.That(comp.Dirt, Is.GreaterThan(0.5f));
            Assert.That(SEntMan.GetComponent<CMUVehicleCrayonComponent>(jeep).Blood, Has.Count.EqualTo(6));

            // Two good sprays' worth.
            var reactive = SEntMan.System<ReactiveSystem>();
            var solution = new Solution("SpaceCleaner", 10);
            reactive.DoEntityReaction(jeep, solution, ReactionMethod.Touch);
        });
        await RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUVehicleGrimeComponent>(jeep).Dirt, Is.Zero);
            Assert.That(SEntMan.GetComponent<CMUVehicleCrayonComponent>(jeep).Blood, Is.Empty);
        });
    }
}
