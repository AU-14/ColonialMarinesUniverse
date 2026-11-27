using Content.IntegrationTests.Fixtures;
using Content.Server.Cargo.Systems;
using Content.Server.Cargo.Components;
using Content.Server._RMC14.Requisitions;
using Content.Server.CMU14.ColonyEconomy;
using Content.Server.CMU14.Electric;
using Content.Server.CMU14.Fluids;
using Content.Server.Power.Components;
using Content.Server.CMU14.Portable;
using Content.Shared.ActionBlocker;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU14.CCVar;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Electric;
using Content.Shared.CMU14.Fluids;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.CMU14.Portable;
using Content.Shared.CMU14.ColonyEconomy;
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
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU.Industry;

public sealed class CMUIndustryPipeworkTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

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
            SEntMan.EnsureComponent<ASRSSellableComponent>(tank);
            SEntMan.EnsureComponent<ASRSSellableComponent>(canister);
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

            var mixed = SSpawnAtPosition("RUMCOilBarrel", map.GridCoords);
            SEntMan.EnsureComponent<ASRSSellableComponent>(mixed);
            Server.System<FluidSystem>().Add(mixed, "RUMCDiesel", 40);
            Server.System<FluidSystem>().Add(mixed, "Water", 1);
            Assert.That(Server.System<CMUIndustryExportSystem>().CanExport(mixed), Is.False);

            // only machine-made kits export now, a bought one is worth nothing to the ASRS
            var kit = SSpawnAtPosition("RUMCKitDriveItem", map.GridCoords);
            SEntMan.EnsureComponent<ASRSSellableComponent>(kit);
            Assert.That(Server.System<CMUIndustryExportSystem>().CanExport(kit), Is.True);
            var one = pricing.GetPrice(kit);
            Assert.That(one, Is.GreaterThan(0));
            Server.System<SharedStackSystem>().SetCount(kit, 3);
            Assert.That(pricing.GetPrice(kit), Is.EqualTo(one * 3));
        });
    }

    [Test]
    public async Task DefaultAsrsCreditsCrudeOilToItsSharedAccountOnly()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var requisitions = Server.System<RequisitionsSystem>();
            var balance = requisitions.GetFunds("unassigned");
            var otherBalance = requisitions.GetFunds("colony");
            var elevator = SSpawnAtPosition("CMCargoElevator", map.GridCoords);
            var lift = SComp<RequisitionsElevatorComponent>(elevator);
            lift.Mode = RequisitionsElevatorMode.Lowering;
            lift.NextMode = RequisitionsElevatorMode.Lowered;
            lift.ToggledAt = SGameTiming.CurTime - TimeSpan.FromSeconds(5);
            lift.LowerDelay = TimeSpan.Zero;
            lift.LoweringSound = null;
            var barrel = SSpawnAtPosition("RUMCOilBarrel", map.GridCoords);
            SEntMan.EnsureComponent<ASRSSellableComponent>(barrel);
            var fluids = Server.System<FluidSystem>();
            fluids.Add(barrel, "RUMCCrudeOil", SComp<FluidTankComponent>(barrel).Capacity);
            var value = (int) Math.Floor(Server.System<PricingSystem>().GetPrice(barrel));
            Assert.That(value, Is.GreaterThan(0), "A full crude-oil barrel must have export value.");

            requisitions.Update(0.1f);

            Assert.That(requisitions.GetFunds("unassigned"), Is.EqualTo(balance + value), "The default platform must credit its normalized account.");
            Assert.That(requisitions.GetFunds("colony"), Is.EqualTo(otherBalance), "The sale must not pay another faction.");
            Assert.That(SEntMan.IsQueuedForDeletion(barrel), Is.True);
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
            var requisitions = Server.System<RequisitionsSystem>();
            var faction = inCrate ? "test-crated-fuel" : "test-loose-fuel";
            var openingBalance = requisitions.GetFunds(faction);
            var elevator = SSpawnAtPosition("CMCargoElevator", map.GridCoords);
            var lift = SComp<RequisitionsElevatorComponent>(elevator);
            lift.Faction = faction;
            lift.Mode = RequisitionsElevatorMode.Lowering;
            lift.NextMode = RequisitionsElevatorMode.Lowered;
            lift.ToggledAt = SGameTiming.CurTime - TimeSpan.FromSeconds(5);
            lift.LowerDelay = TimeSpan.Zero;
            lift.LoweringSound = null;
            var barrel = SSpawnAtPosition("RUMCOilBarrel", map.GridCoords);
            var kit = SSpawnAtPosition("RUMCKitDriveItem", map.GridCoords);
            SEntMan.EnsureComponent<ASRSSellableComponent>(barrel);
            SEntMan.EnsureComponent<ASRSSellableComponent>(kit);
            Server.System<FluidSystem>().Add(barrel, "RUMCDiesel", 200);
            var pricing = Server.System<PricingSystem>();
            var expected = pricing.GetPrice(barrel) + pricing.GetPrice(kit);
            var shipment = kit;
            if (inCrate)
            {
                var crate = SSpawnAtPosition("RMCCrateBase", map.GridCoords);
                shipment = crate;
                var canister = SSpawnAtPosition("RUMCFuelCanister", map.GridCoords);
                SEntMan.EnsureComponent<ASRSSellableComponent>(canister);
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
                expected += SComp<RequisitionsCrateComponent>(crate).Reward
                            + pricing.GetPrice(canister);
            }
            Assert.That(expected, Is.GreaterThan(0));
            requisitions.Update(0.1f);
            Assert.That(requisitions.GetFunds(faction), Is.EqualTo(openingBalance + Math.Floor(expected)));
            Assert.That(SEntMan.IsQueuedForDeletion(barrel), Is.True);
            Assert.That(SEntMan.IsQueuedForDeletion(shipment), Is.True);
        });
    }

    [TestCase("colony")]
    [TestCase("govfor")]
    [TestCase("opfor")]
    [TestCase("corporate")]
    public async Task AsrsSellableStacksPayTheMatchingFactionBudget(string faction)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(AU14CCVars.SellCargoRewards, true);
            var requisitions = Server.System<RequisitionsSystem>();
            var balance = requisitions.GetFunds(faction);
            var otherBalance = requisitions.GetFunds("unassigned");

            var elevator = SSpawnAtPosition("CMCargoElevator", map.GridCoords);
            var lift = SComp<RequisitionsElevatorComponent>(elevator);
            lift.Faction = faction;
            lift.Mode = RequisitionsElevatorMode.Lowering;
            lift.NextMode = RequisitionsElevatorMode.Lowered;
            lift.ToggledAt = SGameTiming.CurTime - TimeSpan.FromSeconds(5);
            lift.LowerDelay = TimeSpan.Zero;
            lift.LoweringSound = null;

            var product = SSpawnAtPosition("RUMCKitDriveItem", map.GridCoords);
            SEntMan.EnsureComponent<ASRSSellableComponent>(product);
            Server.System<SharedStackSystem>().SetCount(product, 4);
            var value = (int) Math.Floor(Server.System<PricingSystem>().GetPrice(product));
            Assert.That(value, Is.GreaterThan(0));

            requisitions.Update(0.1f);

            Assert.That(requisitions.GetFunds(faction), Is.EqualTo(balance + value));
            Assert.That(requisitions.GetFunds("unassigned"), Is.EqualTo(otherBalance));
            Assert.That(SEntMan.IsQueuedForDeletion(product), Is.True);
        });
    }

    [Test]
    public async Task SplitIndustryOutputKeepsItsPriceAndRejectsMixingWithPurchasedStock()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var stacks = Server.System<SharedStackSystem>();
            var pricing = Server.System<PricingSystem>();
            var marked = SSpawnAtPosition("RUMCAlexandritePowderItem", map.GridCoords);
            var purchased = SSpawnAtPosition("RUMCAlexandritePowderItem", map.GridCoords);
            SEntMan.EnsureComponent<ASRSSellableComponent>(marked);
            stacks.SetCount(marked, 3);
            var originalValue = pricing.GetPrice(marked);
            Assert.That(originalValue, Is.Positive);

            var split = stacks.Split((marked, SComp<StackComponent>(marked)), 1, map.GridCoords)!.Value;
            Assert.That(SEntMan.HasComponent<ASRSSellableComponent>(split), Is.True);
            Assert.That(pricing.GetPrice(marked) + pricing.GetPrice(split), Is.EqualTo(originalValue));
            Assert.That(stacks.TryMergeStacks(purchased, split, out _), Is.False);
            Assert.That(stacks.TryMergeStacks(split, purchased, out _), Is.False);
            Assert.That(stacks.TryMergeStacks(split, marked, out _), Is.True);
            HashSet<EntityUid> remaining = [marked, purchased];
            stacks.MergeStacks(ref remaining);
            Assert.That(remaining, Is.EquivalentTo(new[] { marked, purchased }));
            Assert.That(pricing.GetPrice(marked), Is.EqualTo(originalValue));
            Assert.That(SComp<StackComponent>(purchased).Count, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SifterLeavesPurchasedOutputSeparateFromItsProduction()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var machine = SSpawnAtPosition("RUMCMachineSifter", map.GridCoords);
            var recipe = Server.ResolveDependency<IPrototypeManager>().Index<MachineRecipePrototype>("RUMCMRSifterStonePowderToSand");
            var slots = Server.System<ItemSlotsSystem>();
            var powder = SSpawnAtPosition("RUMCStonePowderItem", map.GridCoords);
            Server.System<SharedStackSystem>().SetCount(powder, recipe.Inputs["RUMCStonePowder"]);
            var purchased = SSpawnAtPosition("RUMCSandItem", map.GridCoords);
            var purchasedCount = SComp<StackComponent>(purchased).Count;
            Assert.That(slots.TryInsert(machine, "input1", powder, null), Is.True);
            Assert.That(slots.TryInsert(machine, "output1", purchased, null), Is.True);

            SComp<ApcPowerReceiverComponent>(machine).Powered = true;
            Server.System<RecipeMachineSystem>().Update(recipe.Duration);
            var output = slots.GetItemOrNull(machine, "output2")!.Value;
            Assert.That(SComp<StackComponent>(output).Count, Is.EqualTo(recipe.Outputs["RUMCSand"]));
            Assert.That(SEntMan.HasComponent<ASRSSellableComponent>(output), Is.True);
            Assert.That(SComp<StackComponent>(purchased).Count, Is.EqualTo(purchasedCount));
            Assert.That(SEntMan.HasComponent<ASRSSellableComponent>(purchased), Is.False);
        });
    }

    [Test]
    public async Task OreBoxAndLooseCargoSettleFractionalValueAcrossTrips()
    {
        const string faction = "test-fractional-cargo";
        var map = await Pair.CreateTestMap();
        EntityUid elevator = default;
        var openingBalance = 0;
        var unitValue = 0.0;
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(AU14CCVars.SellCargoRewards, true);
            var requisitions = Server.System<RequisitionsSystem>();
            openingBalance = requisitions.GetFunds(faction);
            elevator = SSpawnAtPosition("CMCargoElevator", map.GridCoords);
            SComp<RequisitionsElevatorComponent>(elevator).Faction = faction;
            var crate = SSpawnAtPosition("RUMCMachineMagneticOreBox", map.GridCoords);
            var containers = Server.System<SharedContainerSystem>();
            var storage = containers.GetContainer(crate, "storagebase");
            var purchased = SSpawnAtPosition("RUMCAlexandritePowderItem", map.GridCoords);
            var mined = SSpawnAtPosition("RUMCAlexandritePowderItem", map.GridCoords);
            SEntMan.EnsureComponent<ASRSSellableComponent>(mined);
            Server.System<SharedStackSystem>().SetCount(mined, 2);
            var damagedPrice = SSpawnAtPosition("CMScrewdriver", map.GridCoords);
            SEntMan.EnsureComponent<CargoSellWhitelistComponent>(damagedPrice);
            SEntMan.EnsureComponent<StaticPriceComponent>(damagedPrice).Price = double.NaN;
            Assert.That(requisitions.TryAppraiseAsrsCargo(damagedPrice, out _), Is.False);
            Assert.That(requisitions.TryAppraiseAsrsCargo(purchased, out _), Is.False);
            Assert.That(requisitions.TryAppraiseAsrsCargo(mined, out unitValue), Is.True);
            Assert.That(unitValue % 1, Is.GreaterThan(0), "Use a product with a fractional price to exercise settlement.");
            Assert.That(containers.Insert(mined, storage), Is.True);
            Assert.That(containers.Insert(purchased, storage), Is.True);
            Assert.That(requisitions.TryAppraiseAsrsCargo(crate, out var crateValue), Is.True);
            Assert.That(crateValue, Is.EqualTo(unitValue));

            Lower();
            Assert.That(SEntMan.IsQueuedForDeletion(crate), Is.True);
            Assert.That(requisitions.GetFunds(faction), Is.EqualTo(openingBalance + Math.Floor(unitValue)));
        });

        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            var loose = SSpawnAtPosition("RUMCAlexandritePowderItem", map.GridCoords);
            SEntMan.EnsureComponent<ASRSSellableComponent>(loose);
            Server.System<SharedStackSystem>().SetCount(loose, 2);
            Lower();
            Assert.That(Server.System<RequisitionsSystem>().GetFunds(faction), Is.EqualTo(openingBalance + Math.Floor(2 * unitValue)));
        });

        void Lower()
        {
            var lift = SComp<RequisitionsElevatorComponent>(elevator);
            lift.Faction = faction;
            lift.Mode = RequisitionsElevatorMode.Lowering;
            lift.NextMode = RequisitionsElevatorMode.Lowered;
            lift.ToggledAt = SGameTiming.CurTime - TimeSpan.FromSeconds(5);
            lift.LowerDelay = TimeSpan.Zero;
            lift.LoweringSound = null;
            Server.System<RequisitionsSystem>().Update(0.1f);
        }
    }

    [Test]
    public async Task AsrsCratedAndLooseGemsExhaustOneRoundOrder()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(AU14CCVars.SellCargoRewards, true);
            var requisitions = Server.System<RequisitionsSystem>();
            var openingBalance = requisitions.GetFunds("colony");
            var elevator = SSpawnAtPosition("CMCargoElevator", map.GridCoords);
            var lift = SComp<RequisitionsElevatorComponent>(elevator);
            lift.Faction = "colony";
            lift.Mode = RequisitionsElevatorMode.Lowering;
            lift.NextMode = RequisitionsElevatorMode.Lowered;
            lift.ToggledAt = SGameTiming.CurTime - TimeSpan.FromSeconds(5);
            lift.LowerDelay = TimeSpan.Zero;
            lift.LoweringSound = null;

            var crate = SSpawnAtPosition("RMCCrateBase", map.GridCoords);
            var containers = Server.System<SharedContainerSystem>();
            var storage = containers.GetContainer(crate, "entity_storage");
            // Leave 50 credits of listed diamond demand for this load, all at half price.
            Server.System<CMUIndustryExportSystem>().FillRoundOrders([new("stack:RUMCGemDiamond", 3950)], settle: true);
            Assert.That(containers.Insert(Gem("RUMCGemDiamondItem", 7), storage), Is.True);
            Gem("RUMCGemDiamondItem", 7);

            var refund = SComp<RequisitionsCrateComponent>(crate).Reward;
            Assert.That(requisitions.TryAppraiseAsrsCargo(crate, out var firstQuote), Is.True);
            Assert.That(firstQuote, Is.GreaterThan(refund));
            requisitions.Update(0.1f);
            Assert.That(requisitions.GetFunds("colony"), Is.EqualTo(openingBalance + 25 + refund));

            var diamond = Gem("RUMCGemDiamondItem", 1);
            var ruby = Gem("RUMCGemRubyItem", 1);
            Assert.That(requisitions.TryAppraiseAsrsCargo(diamond, out var diamondOffer, "govfor"), Is.True);
            Assert.That(diamondOffer, Is.Zero);
            Assert.That(requisitions.TryAppraiseAsrsCargo(ruby, out var rubyOffer), Is.True);
            Assert.That(rubyOffer, Is.EqualTo(Server.System<PricingSystem>().GetPrice(ruby)).And.Positive);
        });

        EntityUid Gem(string id, int count)
        {
            var gem = SSpawnAtPosition(id, map.GridCoords);
            SEntMan.EnsureComponent<ASRSSellableComponent>(gem);
            Server.System<SharedStackSystem>().SetCount(gem, count);
            return gem;
        }
    }

    [Test]
    public async Task AsrsSellableDebugToolAppliesMarker()
    {
        await Server.WaitAssertion(() =>
        {
            var tool = SEntMan.SpawnEntity("CMUASRSSellableDebugTool", MapCoordinates.Nullspace);
            var target = SEntMan.SpawnEntity("RUMCKitDriveItem", MapCoordinates.Nullspace);
            Assert.That(SEntMan.HasComponent<ASRSSellableComponent>(target), Is.False);

            var interaction = new AfterInteractEvent(tool, tool, target, default, true);
            SEntMan.EventBus.RaiseLocalEvent(tool, interaction);

            Assert.Multiple(() =>
            {
                Assert.That(interaction.Handled, Is.True);
                Assert.That(SEntMan.HasComponent<ASRSSellableComponent>(target), Is.True);
            });

            // Applying the marker again is intentionally harmless.
            var repeated = new AfterInteractEvent(tool, tool, target, default, true);
            SEntMan.EventBus.RaiseLocalEvent(tool, repeated);
            Assert.That(repeated.Handled, Is.True);

            SEntMan.DeleteEntity(tool);
            SEntMan.DeleteEntity(target);
        });
    }
}
