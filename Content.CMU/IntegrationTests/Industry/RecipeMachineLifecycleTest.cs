using Content.IntegrationTests;
using Content.Server.CMU14.Electric;
using Content.Server.CMU14.Fluids;
using Content.Server.Power.Components;
using Content.Shared.CMU14.Electric;
using Content.Shared.CMU14.Fluids;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU.Industry;

[TestFixture]
public sealed class RecipeMachineLifecycleTest
{
    private static readonly EntProtoId Electrolyzer = "RUMCElectrolyzer";
    private static readonly ProtoId<MachineRecipePrototype> WaterRecipe = "RUMCMRElectrolyzerWater";

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

            // Another delivery occupies the output tank while the batch is suspended.
            fluids.Add(machine, FluidKinds.Water, tank.Capacity);
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
