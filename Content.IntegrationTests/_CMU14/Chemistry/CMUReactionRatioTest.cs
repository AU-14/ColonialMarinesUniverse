#pragma warning disable RA0002 // the test puts the beaker on the hotplate's ItemPlacer directly instead of through physics

using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Chemistry.Temperature;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Placeable;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Chemistry;

// FLOW-04 / TEST-02: phenol-formaldehyde (2 formaldehyde + 1 phenol, 374 K gate) must not depend on a methane catalyst.
[TestFixture]
public sealed class CMUReactionRatioTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    // 10 formaldehyde + 10 phenol makes 5 phenol-formaldehyde and leaves 5 phenol.
    [Test]
    public async Task PhenolFormaldehydeExcessReagentIsLeftOver()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var soln = BeakerSolution(SSpawnAtPosition("CMBeaker", map.GridCoords));
            Assert.That(solutions.TryAddReagent(soln, "CMUFormaldehyde", FixedPoint2.New(10)), Is.True);
            Assert.That(solutions.TryAddReagent(soln, "CMUPhenol", FixedPoint2.New(10)), Is.True);
            solutions.SetTemperature(soln, 380f);

            var solution = soln.Comp.Solution;
            Assert.Multiple(() =>
            {
                Assert.That(solution.GetTotalPrototypeQuantity("CMUPhenolFormaldehyde").Float(), Is.EqualTo(5f).Within(0.01f));
                Assert.That(solution.GetTotalPrototypeQuantity("CMUPhenol").Float(), Is.EqualTo(5f).Within(0.01f));
                Assert.That(solution.GetTotalPrototypeQuantity("CMUFormaldehyde").Float(), Is.EqualTo(0f).Within(0.01f));
            });
        });
    }

    // Regression: this failed before the methane catalyst was removed. The ungated CMUFormaldehyde reaction consumed
    // the methane whenever step 1 left oxygen and phoron over, so phenol-formaldehyde never formed.
    [Test]
    public async Task PhenolFormaldehydeFormsWhenFormaldehydeStepLeavesMethaneStarved()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var soln = BeakerSolution(SSpawnAtPosition("CMBeaker", map.GridCoords)); // 60u, the 31u fits
            foreach (var (reagent, amount) in new[]
                     {
                         ("RMCSilver", 5), ("RMCOxygen", 3), ("RMCPhoron", 3),
                         ("CMUFormaldehyde", 10), ("CMUPhenol", 5), ("RMCMethane", 5),
                     })
            {
                Assert.That(solutions.TryAddReagent(soln, reagent, FixedPoint2.New(amount)), Is.True, reagent);
            }

            solutions.SetTemperature(soln, 380f);

            Assert.That(soln.Comp.Solution.GetTotalPrototypeQuantity("CMUPhenolFormaldehyde").Float(), Is.GreaterThan(0f),
                "phenol-formaldehyde should form at 380 K whatever the formaldehyde step left over");
        });
    }

    // N2(c): the same off-ratio mix on a real hotplate set to 380 K reacts over ticks and never overshoots.
    [Test]
    public async Task PhenolFormaldehydeFormsOnAHotplate()
    {
        const float target = 380f;
        var map = await Pair.CreateTestMap();
        EntityUid hotplate = default;
        EntityUid beaker = default;

        await Server.WaitAssertion(() =>
        {
            hotplate = SSpawnAtPosition("ChemistryHotplate", map.GridCoords);
            Server.System<PowerReceiverSystem>().SetNeedsPower(hotplate, false);
            var receiver = SComp<ApcPowerReceiverComponent>(hotplate);
            receiver.Powered = true;
            SEntMan.Dirty(hotplate, receiver);
        });
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var solutions = Server.System<SharedSolutionContainerSystem>();
            beaker = SSpawnAtPosition("CMBeaker", map.GridCoords);
            var soln = BeakerSolution(beaker);
            Assert.That(solutions.TryAddReagent(soln, "CMUFormaldehyde", FixedPoint2.New(10)), Is.True);
            Assert.That(solutions.TryAddReagent(soln, "CMUPhenol", FixedPoint2.New(10)), Is.True);
            solutions.SetTemperature(soln, 293.15f);
            Assert.That(soln.Comp.Solution.GetTotalPrototypeQuantity("CMUPhenolFormaldehyde").Float(), Is.EqualTo(0f));

            SComp<ItemPlacerComponent>(hotplate).PlacedEntities.Add(beaker);
            var plates = Server.System<CMUThermostatPlateSystem>();
            plates.SetTarget(hotplate, target);
            plates.SetEnabled(hotplate, true);
        });

        await RunSeconds(20);

        await Server.WaitAssertion(() =>
        {
            var solution = BeakerSolution(beaker).Comp.Solution;
            Assert.Multiple(() =>
            {
                Assert.That(solution.GetTotalPrototypeQuantity("CMUPhenolFormaldehyde").Float(), Is.EqualTo(5f).Within(0.01f),
                    "the hotplate should drive the off-ratio mix through its 374 K gate");
                Assert.That(solution.GetTotalPrototypeQuantity("CMUPhenol").Float(), Is.EqualTo(5f).Within(0.01f));
                Assert.That(solution.Temperature, Is.LessThanOrEqualTo(target + 0.01f), "the hotplate overshot its target");
            });
        });
    }

    private Entity<SolutionComponent> BeakerSolution(EntityUid beaker)
    {
        var name = SComp<FitsInDispenserComponent>(beaker).Solution;
        Assert.That(Server.System<SharedSolutionContainerSystem>().TryGetSolution(beaker, name, out var soln, out _), Is.True);
        return soln!.Value;
    }
}
