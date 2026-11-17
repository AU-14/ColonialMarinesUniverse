using Content.IntegrationTests;
using Content.Server.CMU14.Electric;
using Content.Server.CMU14.Fluids;
using Content.Server.Power.Components;
using Content.Shared.CMU14.Electric;
using Content.Shared.CMU14.Fluids;
using NUnit.Framework;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU.Industry;

[TestFixture]
public sealed class RecipeMachineLifecycleTest
{
    private static readonly EntProtoId Electrolyzer = "RUMCElectrolyzer";
    private static readonly ProtoId<MachineRecipePrototype> WaterRecipe = "RUMCMRElectrolyzerWater";

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

            var progressBeforeOutage = operation.Progress;
            power.Powered = false;
            recipes.Update(recipe.Duration * 2);
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
