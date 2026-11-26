using Content.IntegrationTests;
using Content.Server.CMU14.Electric;
using Content.Server.CMU14.Fluids;
using Content.Server.Power.Components;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Electric;
using Content.Shared.CMU14.Fluids;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using NUnit.Framework;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU.Industry;

[TestFixture]
public sealed class RecipeMachineLifecycleTest
{
    private static readonly EntProtoId Electrolyzer = "RUMCMachineElectrolyzer";
    private static readonly ProtoId<MachineRecipePrototype> WaterRecipe = "RUMCMRElectrolyzerWater";

    [Test]
    public async Task FabricatorWaitsForAChoiceAndFinishesThePaidBatch()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var recipes = entities.System<RecipeMachineSystem>();
            var slots = entities.System<ItemSlotsSystem>();
            var machine = entities.SpawnAtPosition("RUMCMachineFabricator", map.GridCoords);
            entities.GetComponent<ApcPowerReceiverComponent>(machine).Powered = true;
            var state = entities.GetComponent<RecipeMachineComponent>(machine);
            var steel = entities.SpawnAtPosition("CMSheetMetal", map.GridCoords);
            var stack = entities.GetComponent<StackComponent>(steel);
            entities.System<SharedStackSystem>().SetCount((steel, stack), 2);
            Assert.That(slots.TryInsert(machine, "input1", steel, null), Is.True);

            recipes.Update(1);
            Assert.That(state.RecipeChoices, Does.Contain("RUMCMRFabricatorBucket").And.Contain("RUMCMRFabricatorRackParts"));
            Assert.That(state.ActiveRecipe, Is.Null);
            Assert.That(stack.Count, Is.EqualTo(2));
            Select("RUMCMRFabricatorRackParts");
            recipes.Update(1);
            Assert.That(state.ActiveRecipe, Is.EqualTo("RUMCMRFabricatorRackParts"));
            Select("RUMCMRFabricatorBucket");
            recipes.Update(4);
            var output = slots.GetItemOrNull(machine, "output1");
            Assert.That(entities.GetComponent<MetaDataComponent>(output!.Value).EntityPrototype?.ID, Is.EqualTo("RMCRackParts"));

            void Select(string id)
            {
                var message = new CMUSelectMachineRecipeMessage(id) { UiKey = RecipeMachineUiKey.Key };
                entities.EventBus.RaiseLocalEvent(machine, (object) message);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExtruderKeepsPurchasedAndLocallyMadeSteelSeparate()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var machines = entities.System<RecipeMachineSystem>();
            var slots = entities.System<ItemSlotsSystem>();
            var extruder = entities.SpawnAtPosition("RUMCMachineExtruder", map.GridCoords);
            entities.GetComponent<ApcPowerReceiverComponent>(extruder).Powered = true;
            entities.GetComponent<RecipeMachineComponent>(extruder).Circuit = 1;
            var purchased = entities.SpawnAtPosition("CMSheetMetal1", map.GridCoords);
            var local = entities.SpawnAtPosition("CMSheetMetal1", map.GridCoords);
            entities.EnsureComponent<ASRSSellableComponent>(local);
            Assert.That(slots.TryInsert(extruder, "input1", purchased, null), Is.True);
            machines.Update(1);
            Assert.That(slots.TryInsert(extruder, "input2", local, null), Is.True);
            machines.Update(4);
            machines.Update(4);

            var ordinaryRods = slots.GetItemOrNull(extruder, "output1")!.Value;
            var localRods = slots.GetItemOrNull(extruder, "output2")!.Value;
            Assert.That(entities.HasComponent<ASRSSellableComponent>(ordinaryRods), Is.False);
            Assert.That(entities.HasComponent<ASRSSellableComponent>(localRods), Is.True);
            Assert.That(entities.GetComponent<StackComponent>(ordinaryRods).Count, Is.EqualTo(2));
            Assert.That(entities.GetComponent<StackComponent>(localRods).Count, Is.EqualTo(2));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IdleSifterWakesWhenAnOccupiedSlotGainsEnoughInput()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var recipes = entities.System<RecipeMachineSystem>();
            var stacks = entities.System<SharedStackSystem>();
            var slots = entities.System<ItemSlotsSystem>();
            var machine = entities.SpawnEntity("RUMCMachineSifter", map.GridCoords);
            entities.GetComponent<ApcPowerReceiverComponent>(machine).Powered = true;
            var powder = entities.SpawnEntity("RUMCStonePowderItem", map.GridCoords);
            stacks.SetCount(powder, 3);
            Assert.That(slots.TryInsert(machine, "input1", powder, null), Is.True);

            recipes.Update(4);
            Assert.That(slots.GetItemOrNull(machine, "output1"), Is.Null);
            stacks.SetCount(powder, 4);
            recipes.Update(4);

            var result = slots.GetItemOrNull(machine, "output1");
            Assert.That(entities.GetComponent<StackComponent>(result!.Value).StackTypeId.Id, Is.EqualTo("RUMCSand"));
            Assert.That(entities.GetComponent<StackComponent>(result.Value).Count, Is.EqualTo(4));
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("RUMCMRElectrolyzerWater")]
    [TestCase("RUMCMRElectrolyzerBrine")]
    public async Task ElectrolyzerRoutesProductsSeparatelyWithoutExportingFeedstock(string recipeId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var fluids = entities.System<FluidSystem>();
            var recipe = pair.Server.ResolveDependency<IPrototypeManager>().Index<MachineRecipePrototype>(recipeId);
            var machine = entities.SpawnEntity(Electrolyzer, map.GridCoords);
            var ports = entities.GetComponent<FluidPortsComponent>(machine);
            var power = entities.GetComponent<ApcPowerReceiverComponent>(machine);
            power.Powered = false;
            fluids.Add(machine, FluidKinds.Water, 100);
            foreach (var (kind, amount) in recipe.FluidInputs)
            {
                if (kind != FluidKinds.Water)
                    fluids.Add(machine, kind, amount);
            }

            var offsets = new[] { new Vector2i(0, 1), new Vector2i(1, 0), new Vector2i(0, -1) };
            var maps = entities.System<SharedMapSystem>();
            var first = entities.SpawnEntity("RUMCSteelPipe", map.GridCoords);
            var firstPipe = entities.GetComponent<FluidPipeComponent>(first);
            ports.Ports.First(p => p.Output).Links.Add(first);
            fluids.Update(0.5f);
            Assert.That(firstPipe.Amount, Is.Zero, "An unpowered electrolyzer must not pump its water feed into an output pipe.");
            ports.Ports.First(p => p.Output).Links.Clear();
            entities.DeleteEntity(first);

            var outputs = new Dictionary<string, FluidPipeComponent>();
            var index = 0;
            foreach (var kind in recipe.FluidOutputs.Keys)
            {
                var offset = offsets[index++];
                maps.SetTile(map.Grid, offset, map.Tile.Tile);
                var pipe = entities.SpawnEntity("RUMCSteelPipe", map.GridCoords.Offset(offset));
                var component = entities.GetComponent<FluidPipeComponent>(pipe);
                fluids.SetShape((pipe, component), CMUFluidPipeShape.Cross);
                var port = ports.Ports.Single(p => p.Output && p.Kind == kind);
                port.Links.Add(pipe);
                outputs.Add(kind, component);
            }

            power.Powered = true;
            entities.System<RecipeMachineSystem>().Update(recipe.Duration);
            for (var i = 0; i < 10; i++)
                fluids.Update(0.5f);

            foreach (var (kind, amount) in recipe.FluidOutputs)
            {
                Assert.That(outputs[kind].Kind, Is.EqualTo(kind));
                Assert.That(outputs[kind].Amount, Is.EqualTo(amount).Within(0.001f));
                Assert.That(fluids.GetAmount(machine, kind), Is.Zero.Within(0.001f));
            }
            Assert.That(fluids.GetAmount(machine, FluidKinds.Water),
                Is.EqualTo(100 - recipe.FluidInputs[FluidKinds.Water]).Within(0.001f));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InterruptedBatchWaitsForPowerAndOutputRoomWithoutDuplicatingProducts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var recipes = entities.System<RecipeMachineSystem>();
            var fluids = entities.System<FluidSystem>();
            var recipe = pair.Server.ResolveDependency<IPrototypeManager>().Index(WaterRecipe);
            var machine = entities.SpawnEntity(Electrolyzer, map.GridCoords);
            var operation = entities.GetComponent<RecipeMachineComponent>(machine);
            var tank = entities.GetComponent<FluidTankComponent>(machine);
            var power = entities.GetComponent<ApcPowerReceiverComponent>(machine);

            // Simulate the receiver's delivered power state. This exercises the machine's
            // lifecycle in isolation from the electrical network's own update schedule.
            power.Powered = true;
            foreach (var (kind, amount) in recipe.FluidInputs)
                fluids.Add(machine, kind, amount);

            recipes.Update(recipe.Duration / 4);
            Assert.That(operation.ActiveRecipe, Is.EqualTo(WaterRecipe.Id));
            foreach (var (kind, _) in recipe.FluidInputs)
                Assert.That(fluids.GetAmount(machine, kind), Is.Zero, "Inputs must be charged when the batch starts.");

            var runningLoad = power.Load;
            var progressBeforeOutage = operation.Progress;
            power.Powered = false;
            recipes.Update(recipe.Duration * 2);
            Assert.That(power.Load, Is.EqualTo(runningLoad), "A pending batch must keep its demand stable during an outage.");
            Assert.That(operation.Progress, Is.EqualTo(progressBeforeOutage), "An unpowered batch must pause.");
            foreach (var (kind, _) in recipe.FluidOutputs)
                Assert.That(fluids.GetAmount(machine, kind), Is.Zero, "Power loss must not finish the batch.");

            // Normal deliveries must leave the pending batch's product volume available.
            fluids.Add(machine, FluidKinds.Water, tank.Capacity);
            Assert.That(tank.Capacity - tank.Total,
                Is.EqualTo(recipe.FluidOutputs.Values.Sum()).Within(0.001f));

            // A forced inventory edit can still steal reserved space. Keep the blocked-batch recovery path safe.
            tank.Contents[FluidKinds.Water] += 1;
            power.Powered = true;
            recipes.Update(recipe.Duration);
            Assert.That(operation.Blocked, Is.True);
            foreach (var (kind, _) in recipe.FluidOutputs)
                Assert.That(fluids.GetAmount(machine, kind), Is.Zero, "A full tank must retain the pending batch.");

            fluids.Remove(machine, FluidKinds.Water, tank.Capacity);
            recipes.Update(recipe.Duration);
            Assert.That(operation.Active, Is.False);
            Assert.That(operation.Blocked, Is.False);
            foreach (var (kind, amount) in recipe.FluidOutputs)
                Assert.That(fluids.GetAmount(machine, kind), Is.EqualTo(amount).Within(0.001f));

            recipes.Update(recipe.Duration);
            foreach (var (kind, amount) in recipe.FluidOutputs)
                Assert.That(fluids.GetAmount(machine, kind), Is.EqualTo(amount).Within(0.001f),
                    "Resuming a completed batch must not produce it a second time.");
        });
        await pair.CleanReturnAsync();
    }
}
