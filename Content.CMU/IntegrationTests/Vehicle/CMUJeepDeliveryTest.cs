using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ColonyEconomy;
using Content.Server.Light.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared._RMC14.SupplyDrop;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Vehicle;

[TestFixture]
public sealed class CMUJeepDeliveryTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Destructive = true };

    [Test]
    public async Task DepartmentDispensesBeaconOnlyAfterAuthorizedPayment()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var console = SEntMan.SpawnEntity("AUDepartmentConsoleMedical", map.GridCoords);
            var department = SEntMan.GetComponent<DepartmentConsoleComponent>(console);
            var system = SEntMan.System<DepartmentConsoleSystem>();
            var physician = SEntMan.System<StationSpawningSystem>().SpawnPlayerMob(map.GridCoords,
                "AU14JobCivilianHeadPhysician", new HumanoidCharacterProfile(), station: null);
            var outsider = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var cost = department.Supplies[0].Cost;
            department.DepartmentBudget = cost - 1;

            Assert.That(system.TryBuySupply((console, department), physician, 0), Is.False);
            Assert.That(SEntMan.EntityQuery<CMUVehicleDropFlareComponent>().Count(), Is.Zero);
            Assert.That(department.DepartmentBudget, Is.EqualTo(cost - 1));

            department.DepartmentBudget = cost;
            Assert.That(system.TryBuySupply((console, department), outsider, 0), Is.False);
            Assert.That(system.TryBuySupply((console, department), physician, -1), Is.False);
            Assert.That(department.DepartmentBudget, Is.EqualTo(cost));

            Assert.That(system.TryBuySupply((console, department), physician, 0), Is.True);
            Assert.That(department.DepartmentBudget, Is.Zero);
            var beacon = SEntMan.EntityQuery<CMUVehicleDropFlareComponent>().Single().Owner;
            Assert.That(SEntMan.System<SharedHandsSystem>().IsHolding(physician, beacon), Is.True);
            Assert.That(system.TryBuySupply((console, department), physician, 0), Is.False);
            Assert.That(SEntMan.EntityQuery<CMUVehicleDropFlareComponent>().Count(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task BeaconWaitsForOpenGroundThenDeliversOneUsableMedicalJeep()
    {
        EntityUid planet = default;
        MapId mapId = default;
        EntityUid beacon = default;
        EntityUid laser = default;
        EntityUid crate = default;
        EntityCoordinates target = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            // Planet maps are also their terrain grid; supply-drop areas are resolved from that map.
            planet = maps.CreateMap(out mapId, runMapInit: true);
            var grid = SEntMan.EnsureComponent<MapGridComponent>(planet);
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            for (var x = -4; x <= 4; x++)
            for (var y = -4; y <= 4; y++)
                maps.SetTile(planet, grid, new Vector2i(x, y), floor);
            target = new EntityCoordinates(planet, .5f, .5f);
            beacon = SEntMan.SpawnEntity("CMUMedicalJeepBeacon", target);
        });
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityQuery<BeingSupplyDroppedComponent>().Count(), Is.Zero);
            SEntMan.System<ExpendableLightSystem>().TryActivate((beacon,
                SEntMan.GetComponent<ExpendableLightComponent>(beacon)));
        });
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityQuery<BeingSupplyDroppedComponent>().Count(), Is.Zero,
                "A beacon without a valid supply-drop area must not consume its delivery.");
            var area = SEntMan.EnsureComponent<AreaGridComponent>(planet);
            SEntMan.System<AreaSystem>().ReplaceArea(area, Vector2i.Zero, "RMCAreaDesertDamExterior");
            Assert.That(SEntMan.System<AreaSystem>().CanSupplyDrop(
                SEntMan.System<SharedTransformSystem>().ToMapCoordinates(target)), Is.True);
        });
        await RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            crate = SEntMan.EntityQuery<BeingSupplyDroppedComponent>().Single().Owner;
            laser = SEntMan.EntityQuery<FighterLaserComponent>().Single().Owner;
            var beam = SEntMan.GetComponent<FighterLaserComponent>(laser);
            Assert.That(beam.Incoming, Is.True);
            Assert.That(beam.BeamColor.B, Is.GreaterThan(beam.BeamColor.R));
            Assert.That(SEntMan.GetComponent<TransformComponent>(laser).Coordinates.Position,
                Is.EqualTo(SEntMan.System<SharedTransformSystem>().ToMapCoordinates(target).Position));
        });
        await RunSeconds(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(laser), Is.True, "Landing must clear the delivery beam.");
            Assert.That(SEntMan.HasComponent<BeingSupplyDroppedComponent>(crate), Is.False);
            Assert.That(SEntMan.GetComponent<CMUVehicleDropImpactComponent>(crate).ImpactAt, Is.Not.Null);
            var position = SEntMan.System<SharedTransformSystem>().GetMapCoordinates(crate);
            Assert.That(position.MapId, Is.EqualTo(mapId));
            Assert.That(position.Position, Is.EqualTo(SEntMan.System<SharedTransformSystem>().ToMapCoordinates(target).Position));
            SEntMan.System<CMUVehicleCrateSystem>().Open((crate, SEntMan.GetComponent<CMUVehicleCrateComponent>(crate)));
        });
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            var jeep = SEntMan.EntityQuery<CMUOpenVehicleComponent>().Single().Owner;
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(jeep).EntityPrototype!.ID, Is.EqualTo("CMUVehicleJeepMedical"));
            Assert.That(SEntMan.EntityQuery<BeingSupplyDroppedComponent>().Count(), Is.Zero,
                "The spent beacon must not launch another crate.");
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task FlareAndIntelShareJeepLimitAndCooldown(bool flareFirst)
    {
        EntityUid planet = default;
        EntityCoordinates target = default;
        EntityUid console = default;
        EntityUid buyer = default;
        EntityUid firstFlare = default;
        EntityUid secondFlare = default;
        EntityUid medicalBeacon = default;
        IntelTechTree tree = default!;
        var index = 0;
        var expectedPoints = flareFirst ? 100 : 90;
        var expectedFlareDeliveries = flareFirst ? 2 : 1;
        void Purchase()
        {
            var message = new TechPurchaseOptionBuiMsg(1, index)
            {
                Team = "clf",
                Actor = buyer,
                UiKey = TechControlConsoleUI.Key,
            };
            SEntMan.EventBus.RaiseLocalEvent(console, message);
        }

        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            planet = maps.CreateMap(out _, runMapInit: true);
            var grid = SEntMan.EnsureComponent<MapGridComponent>(planet);
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            for (var x = -4; x <= 4; x++)
            for (var y = -4; y <= 4; y++)
                maps.SetTile(planet, grid, new Vector2i(x, y), floor);

            target = new EntityCoordinates(planet, .5f, .5f);
            console = SEntMan.SpawnEntity("RMCTechTreeConsoleCLF", new EntityCoordinates(planet, -2.5f, -2.5f));
            buyer = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(planet, -2.5f, -1.5f));
            tree = SEntMan.System<IntelSystem>().EnsureTechTree("clf").Comp.Tree;
            tree.Tier = 1;
            tree.Points = 100;
            index = tree.Options[1].FindIndex(option => option.Name == "cmu-tech-clf-jeep");
            tree.Options[1][index] = tree.Options[1][index] with
            {
                // Exercise the real purchase path without dispatching a dropship or simulating half an hour.
                Cost = 10, CurrentCost = 10, PurchaseCooldown = TimeSpan.FromSeconds(8), Events = new(),
            };

            firstFlare = SEntMan.SpawnEntity("CMUFlareJeepDropCLF", target);
            secondFlare = SEntMan.SpawnEntity("CMUFlareJeepDropCLF", new EntityCoordinates(planet, 2.5f, .5f));
            foreach (var flare in new[] { firstFlare, secondFlare })
                SEntMan.System<ExpendableLightSystem>().TryActivate((flare, SEntMan.GetComponent<ExpendableLightComponent>(flare)));
        });
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(tree.Options[1][index].TimesPurchased, Is.Zero,
                "Flares without a valid drop area must not consume the shared allowance.");
            Assert.That(SEntMan.EntityQuery<BeingSupplyDroppedComponent>().Count(), Is.Zero);

            if (!flareFirst)
            {
                Purchase();
                Assert.That(tree.Points.Int(), Is.EqualTo(90));
                Purchase();
                Assert.That(tree.Points.Int(), Is.EqualTo(90), "A purchase during cooldown must not charge the team.");
            }

            var area = SEntMan.EnsureComponent<AreaGridComponent>(planet);
            for (var x = -4; x <= 4; x++)
            for (var y = -4; y <= 4; y++)
                SEntMan.System<AreaSystem>().ReplaceArea(area, new Vector2i(x, y), "RMCAreaDesertDamExterior");
        });
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityQuery<BeingSupplyDroppedComponent>().Count(), Is.EqualTo(flareFirst ? 1 : 0),
                "The first request must put every other flare on cooldown, whichever route supplied it.");
            Assert.That(tree.Options[1][index].TimesPurchased, Is.EqualTo(1));
            Purchase();
            Assert.That(tree.Points.Int(), Is.EqualTo(expectedPoints),
                "Both flare deliveries and intel purchases must block another intel purchase during cooldown.");
        });
        await RunSeconds(6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityQuery<BeingSupplyDroppedComponent>().Count(), Is.EqualTo(expectedFlareDeliveries),
                "Only one waiting flare may take the last delivery slot when the cooldown expires.");
            Assert.That(new[] { firstFlare, secondFlare }
                .Count(flare => SEntMan.GetComponent<CMUVehicleDropFlareComponent>(flare).Called), Is.EqualTo(expectedFlareDeliveries));
            Assert.That(tree.Points.Int(), Is.EqualTo(expectedPoints), "An already purchased flare must not charge intel points.");
            Assert.That(tree.Options[1][index].TimesPurchased, Is.EqualTo(2));
            Assert.That(SEntMan.GetComponent<TechControlConsoleComponent>(console).Tree.Options[1][index].TimesPurchased,
                Is.EqualTo(2), "Flare deliveries must update the intel console's displayed allowance.");
            Purchase();
            Assert.That(tree.Points.Int(), Is.EqualTo(expectedPoints));
        });
        await RunSeconds(13);
        await Server.WaitAssertion(() =>
        {
            Purchase();
            Assert.That(tree.Points.Int(), Is.EqualTo(expectedPoints), "An expired cooldown must not bypass the shared round cap.");
            Assert.That(tree.Options[1][index].TimesPurchased, Is.EqualTo(2));
            Assert.That(SEntMan.EntityQuery<CMUVehicleCrateComponent>()
                .Count(crate => SEntMan.GetComponent<MetaDataComponent>(crate.Owner).EntityPrototype?.ID == "CMUCrateJeepGunner"),
                Is.EqualTo(expectedFlareDeliveries), "The other flare must not deliver a third jeep after another cooldown.");

            medicalBeacon = SEntMan.SpawnEntity("CMUMedicalJeepBeacon", target);
            SEntMan.System<ExpendableLightSystem>().TryActivate((medicalBeacon,
                SEntMan.GetComponent<ExpendableLightComponent>(medicalBeacon)));
        });
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUVehicleDropFlareComponent>(medicalBeacon).Called, Is.True,
                "The CLF allowance must not restrict civilian medical deliveries.");
            Assert.That(SEntMan.EntityQuery<BeingSupplyDroppedComponent>().Count(), Is.EqualTo(1));
        });
        await RunSeconds(13);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.EntityQuery<BeingSupplyDroppedComponent>().Count(), Is.Zero,
                "Finish the medical delivery before the test map is removed."));
    }
}
