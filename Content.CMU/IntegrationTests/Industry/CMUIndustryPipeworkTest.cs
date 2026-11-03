using Content.IntegrationTests.Fixtures;
using Content.Server.Cargo.Systems;
using Content.Server.Cargo.Components;
using Content.Server._RMC14.Requisitions;
using Content.Server.CMU14.ColonyEconomy;
using Content.Server.CMU14.Fluids;
using Content.Server.CMU14.Portable;
using Content.Shared.ActionBlocker;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU14.CCVar;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Fluids;
using Content.Shared.CMU14.Portable;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.CMU.Industry;

public sealed class CMUIndustryPipeworkTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestIndustryAccount
          components:
          - type: RequisitionsAccount
            faction: colony
        """;

    [Test]
    public async Task RotationAndIsolationChangeActualFlowWithoutLosingFluid()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            maps.SetTile(map.Grid, new Vector2i(1, 0), map.Tile.Tile);
            var first = SSpawnAtPosition("RUMCBrassPipe", map.GridCoords);
            var second = SSpawnAtPosition("RUMCBrassPipe", map.GridCoords.Offset(new Vector2(1, 0)));
            var a = SComp<FluidPipeComponent>(first);
            var b = SComp<FluidPipeComponent>(second);
            a.Kind = "Water";
            a.Amount = 30;
            var fluids = Server.System<FluidSystem>();
            fluids.Update(0.5f);
            Assert.That(b.Amount, Is.Zero, "Parallel north/south pipes must not leak sideways.");

            var transform = Server.System<SharedTransformSystem>();
            transform.SetLocalRotation(first, Angle.FromDegrees(90));
            transform.SetLocalRotation(second, Angle.FromDegrees(90));
            fluids.Update(0.5f);
            Assert.That(b.Amount, Is.GreaterThan(0), "Rotating both ends toward each other must enable flow.");
            Assert.That(a.Amount + b.Amount, Is.EqualTo(30).Within(0.001f));

            var before = b.Amount;
            transform.SetLocalRotation(second, Angle.Zero);
            fluids.Update(0.5f);
            Assert.That(b.Amount, Is.EqualTo(before), "Rotating an end away must disconnect the old neighbours.");
            fluids.SetShape((second, b), CMUFluidPipeShape.Cross);
            fluids.Update(0.5f);
            Assert.That(b.Amount, Is.GreaterThan(before));
            before = b.Amount;
            fluids.SetIsolated(second, true);
            fluids.Update(0.5f);
            Assert.That(b.Amount, Is.EqualTo(before));
        });
    }

    [TestCase("RUMCOilBarrel", 0, 1)]
    [TestCase("RUMCOilTank", 2, 0)]
    public async Task MachineLinksRejectGapsAndStopWhenEquipmentMoves(string prototype, int x, int y)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            // Keep a continuous floor so the engine does not split the equipment onto different grids.
            for (var tileX = 0; tileX <= 50; tileX++)
                maps.SetTile(map.Grid, new Vector2i(tileX, 0), map.Tile.Tile);
            foreach (var tile in new[] { new Vector2i(x, y), new Vector2i(0, -1) })
                maps.SetTile(map.Grid, tile, map.Tile.Tile);
            var tank = SSpawnAtPosition(prototype, map.GridCoords);
            var pipe = SSpawnAtPosition("RUMCBrassPipe", map.GridCoords.Offset(new Vector2(x, y)));
            if (x != 0)
                Server.System<SharedTransformSystem>().SetLocalRotation(pipe, Angle.FromDegrees(90));
            var user = SSpawnAtPosition("CMMobHuman", map.GridCoords.Offset(new Vector2(0, -1)));
            var driver = SSpawnAtPosition("CMScrewdriver", SComp<TransformComponent>(user).Coordinates);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, driver), Is.True);
            var ports = SComp<FluidPortsComponent>(tank);
            var links = Server.System<FluidLinkSystem>();
            Assert.That(links.TryConnect((tank, ports), "liquid_out", pipe, user), Is.True);
            Assert.That(links.TryConnect((tank, ports), "liquid_in", pipe, user), Is.False,
                "One selected pipe cannot silently occupy both input and output.");
            var fluids = Server.System<FluidSystem>();
            fluids.Add(tank, "Water", 100);
            fluids.Update(0.5f);
            var before = SComp<FluidPipeComponent>(pipe).Amount;
            Assert.That(before, Is.GreaterThan(0));

            var transform = Server.System<SharedTransformSystem>();
            transform.Unanchor(pipe);
            transform.SetCoordinates(pipe, map.GridCoords.Offset(new Vector2(50, 0)));
            transform.AnchorEntity(pipe);
            fluids.Update(0.5f);
            Assert.That(SComp<FluidPipeComponent>(pipe).Amount, Is.EqualTo(before));
            Assert.That(ports.Ports.Find(p => p.Output)!.Links, Is.Empty);
            Assert.That(links.TryConnect((tank, ports), "liquid_out", pipe, user), Is.False,
                "A selected pipe 50 tiles away must not be a wireless connection.");
        });
    }

    [Test]
    public async Task AdjacentTanksStaySeparateUntilTheirPortsAreConnected()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Server.System<SharedMapSystem>().SetTile(map.Grid, new Vector2i(1, 0), map.Tile.Tile);
            var source = SSpawnAtPosition("RUMCOilBarrel", map.GridCoords);
            var destination = SSpawnAtPosition("RUMCOilBarrel", map.GridCoords.Offset(new Vector2(1, 0)));
            var fluids = Server.System<FluidSystem>();
            fluids.Add(source, "RUMCDiesel", 100);
            fluids.Update(0.5f);
            Assert.That(fluids.GetAmount(destination, "RUMCDiesel"), Is.Zero);
            Assert.That(fluids.GetAmount(source, "RUMCDiesel"), Is.EqualTo(100));
        });
    }

    [Test]
    public async Task DroppingPortableMachineryDoesNotRemoveItsPullingWeight()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid barrel = default;
        var carryingSpeed = 0f;
        await Server.WaitAssertion(() =>
        {
            Server.System<SharedMapSystem>().SetTile(map.Grid, new Vector2i(1, 0), map.Tile.Tile);
            user = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            barrel = SSpawnAtPosition("RUMCOilBarrel", map.GridCoords.Offset(new Vector2(1, 0)));
            var movement = Server.System<MovementSpeedModifierSystem>();
            movement.RefreshMovementSpeedModifiers(user);
            var baseSpeed = SComp<MovementSpeedModifierComponent>(user).CurrentWalkSpeed;
            Assert.That(Server.System<PortableStructureSystem>().TryPickUp((barrel, SComp<PortableStructureComponent>(barrel)), user), Is.True);
            movement.RefreshMovementSpeedModifiers(user);
            carryingSpeed = SComp<MovementSpeedModifierComponent>(user).CurrentWalkSpeed;
            Assert.That(carryingSpeed, Is.LessThan(baseSpeed));
            Assert.That(Server.System<ActionBlockerSystem>().CanThrow(user, barrel), Is.False);
            Assert.That(Server.System<SharedHandsSystem>().TryDrop(user, barrel), Is.True);
        });
        await Pair.RunTicksSync(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<PullingSystem>().TryStartPull(user, barrel), Is.True);
            Server.System<MovementSpeedModifierSystem>().RefreshMovementSpeedModifiers(user);
            Assert.That(SComp<MovementSpeedModifierComponent>(user).CurrentWalkSpeed, Is.EqualTo(carryingSpeed).Within(0.001f));
        });
    }

    [Test]
    public async Task FuelKeepsItsExportValueWhenPouredBetweenTankAndCanister()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var tank = SSpawnAtPosition("RUMCOilBarrel", map.GridCoords);
            var canister = SSpawnAtPosition("RUMCFuelCanister", map.GridCoords);
            var user = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var pricing = Server.System<PricingSystem>();
            Assert.That(solutions.TryGetRefillableSolution(canister, out var solution, out _), Is.True);
            Assert.That(solutions.TryAddReagent(solution.Value, "RUMCDiesel", FixedPoint2.New(40), out _), Is.True);
            var originalValue = pricing.GetPrice(canister) + pricing.GetPrice(tank);
            Assert.That(originalValue, Is.GreaterThan(0));
            var pour = new InteractUsingEvent(user, canister, tank, map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(tank, pour);
            Assert.That(Server.System<FluidSystem>().GetAmount(tank, "RUMCDiesel"), Is.EqualTo(40));
            Assert.That(pricing.GetPrice(tank) + pricing.GetPrice(canister), Is.EqualTo(originalValue).Within(0.001));
            Assert.That(Server.System<CMUIndustryExportSystem>().CanExport(tank), Is.True);

            var kit = SSpawnAtPosition("RUMCKitDriveItem", map.GridCoords);
            Assert.That(Server.System<CMUIndustryExportSystem>().CanExport(kit), Is.True);
            var one = pricing.GetPrice(kit);
            Assert.That(one, Is.GreaterThan(0));
            Server.System<SharedStackSystem>().SetCount(kit, 3);
            Assert.That(pricing.GetPrice(kit), Is.EqualTo(one * 3));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AsrsDeparturePaysForIndustrialFuelAndTradeGoods(bool inCrate)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(AU14CCVars.SellCargoRewards, true);
            var accountUid = SEntMan.SpawnEntity("CMUTestIndustryAccount", MapCoordinates.Nullspace);
            var account = SComp<RequisitionsAccountComponent>(accountUid);
            var elevator = SSpawnAtPosition("CMCargoElevator", map.GridCoords);
            var lift = SComp<RequisitionsElevatorComponent>(elevator);
            lift.Faction = "colony";
            lift.Mode = RequisitionsElevatorMode.Lowering;
            lift.NextMode = RequisitionsElevatorMode.Lowered;
            lift.ToggledAt = SGameTiming.CurTime - TimeSpan.FromSeconds(5);
            lift.LowerDelay = TimeSpan.Zero;
            lift.LoweringSound = null;
            var barrel = SSpawnAtPosition("RUMCOilBarrel", map.GridCoords);
            var kit = SSpawnAtPosition("RUMCKitDriveItem", map.GridCoords);
            Server.System<FluidSystem>().Add(barrel, "RUMCDiesel", 200);
            var pricing = Server.System<PricingSystem>();
            var expected = (int) Math.Round(pricing.GetPrice(barrel)) + (int) Math.Round(pricing.GetPrice(kit));
            var shipment = kit;
            if (inCrate)
            {
                var crate = SSpawnAtPosition("RMCCrateBase", map.GridCoords);
                shipment = crate;
                var canister = SSpawnAtPosition("RUMCFuelCanister", map.GridCoords);
                var solutions = Server.System<SharedSolutionContainerSystem>();
                Assert.That(solutions.TryGetRefillableSolution(canister, out var solution, out _), Is.True);
                Assert.That(solutions.TryAddReagent(solution.Value, "RUMCDiesel", FixedPoint2.New(40), out _), Is.True);
                var containers = Server.System<SharedContainerSystem>();
                var storage = containers.GetContainer(crate, "entity_storage");
                Assert.That(containers.Insert(canister, storage), Is.True);
                Assert.That(containers.Insert(kit, storage), Is.True);
                var tool = SSpawnAtPosition("CMScrewdriver", map.GridCoords);
                SEntMan.EnsureComponent<StaticPriceComponent>(tool).Price = 500;
                Assert.That(containers.Insert(tool, storage), Is.True);
                expected += SComp<RequisitionsCrateComponent>(crate).Reward + (int) Math.Round(pricing.GetPrice(canister));
            }
            Assert.That(expected, Is.GreaterThan(0));
            Server.System<RequisitionsSystem>().Update(0.1f);
            Assert.That(account.Balance, Is.EqualTo(expected));
            Assert.That(SEntMan.IsQueuedForDeletion(barrel), Is.True);
            Assert.That(SEntMan.IsQueuedForDeletion(shipment), Is.True);
            SEntMan.DeleteEntity(accountUid);
        });
    }
}
