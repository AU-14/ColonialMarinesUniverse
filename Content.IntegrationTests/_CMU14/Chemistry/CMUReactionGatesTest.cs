#pragma warning disable RA0002 // the test puts beakers on the chiller's ItemPlacer directly instead of through physics

using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Chemistry.Temperature;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._RMC14.Chemistry.ChemMaster;
using Content.Shared.Chemistry.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Placeable;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Chemistry;

// FLOW-21: reactions that need a lid-closed Mix, heat or cold, and the two cooling sources (chiller, ice brine).
[TestFixture]
public sealed class CMUReactionGatesTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task ChillerCoolsToItsMinimumAndNoFurther()
    {
        var map = await Pair.CreateTestMap();
        EntityUid chiller = default;
        EntityUid warm = default;
        EntityUid cold = default;

        await Server.WaitAssertion(() =>
        {
            chiller = SSpawnAtPosition("CMUChemistryChiller", map.GridCoords);
            Server.System<PowerReceiverSystem>().SetNeedsPower(chiller, false);
            // No power net on the test map recomputes Powered, so set it as ReagentGrinderWorkflowTest does.
            var receiver = SComp<ApcPowerReceiverComponent>(chiller);
            receiver.Powered = true;
            SEntMan.Dirty(chiller, receiver);
        });

        // the power net flips the receiver to powered on its next update
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<PowerReceiverSystem>().IsPowered(chiller), Is.True);

            var solutions = Server.System<SharedSolutionContainerSystem>();
            warm = SSpawnAtPosition("CMBeaker", map.GridCoords);
            cold = SSpawnAtPosition("CMBeaker", map.GridCoords);
            Assert.That(solutions.TryAddReagent(BeakerSolution(warm), "Water", FixedPoint2.New(10)), Is.True);
            Assert.That(solutions.TryAddReagent(BeakerSolution(cold), "Water", FixedPoint2.New(10)), Is.True);
            solutions.SetTemperature(BeakerSolution(warm), 293.15f);
            solutions.SetTemperature(BeakerSolution(cold), 240f);

            var placer = SComp<ItemPlacerComponent>(chiller);
            placer.PlacedEntities.Add(warm);
            placer.PlacedEntities.Add(cold);
            Assert.That(Server.System<CMUSolutionChillerSystem>().TryTurnOn(chiller), Is.True);
        });

        // 160 energy/s on 10u is 16 K/s: 5 s is far more than the 43 K down to 250 K
        await RunSeconds(5);

        await Server.WaitAssertion(() =>
        {
            var min = SComp<CMUSolutionChillerComponent>(chiller).MinTemperature;
            Assert.Multiple(() =>
            {
                Assert.That(BeakerSolution(warm).Comp.Solution.Temperature, Is.EqualTo(min).Within(0.01f),
                    "the chiller should cool a beaker down to its minimum");
                Assert.That(BeakerSolution(cold).Comp.Solution.Temperature, Is.EqualTo(240f).Within(0.01f),
                    "the chiller warmed a beaker that was already below its minimum");
            });
        });
    }

    [Test]
    public async Task IceBrineCoolsTheSolution()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var beaker = SSpawnAtPosition("CMBeaker", map.GridCoords);
            var soln = BeakerSolution(beaker);
            Assert.That(solutions.TryAddReagent(soln, "Water", FixedPoint2.New(40)), Is.True);
            Assert.That(solutions.TryAddReagent(soln, "TableSalt", FixedPoint2.New(1)), Is.True);
            solutions.SetTemperature(soln, 293.15f);

            Assert.That(solutions.TryAddReagent(soln, "Ice", FixedPoint2.New(15)), Is.True);

            var solution = soln.Comp.Solution;
            Assert.Multiple(() =>
            {
                Assert.That(solution.GetTotalPrototypeQuantity("Ice"), Is.EqualTo(FixedPoint2.Zero), "the salt should melt all the ice");
                Assert.That(solution.GetTotalPrototypeQuantity("Water"), Is.EqualTo(FixedPoint2.New(55)));
                Assert.That(solution.GetTotalPrototypeQuantity("TableSalt"), Is.EqualTo(FixedPoint2.New(1)), "salt is only a catalyst");
                Assert.That(solution.Temperature, Is.LessThan(270f), "ice brine should get a beaker under the cold gates");
                Assert.That(solution.Temperature, Is.GreaterThanOrEqualTo(255f - 0.01f), "ice brine stops at 255 K");
            });
        });
    }

    // CMUStabilizedMutagen: Mix plus maxTemp 270
    [Test]
    public async Task ColdGatedReactionNeedsMixAndCold()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var beaker = SSpawnAtPosition("CMBeaker", map.GridCoords);
            var soln = BeakerSolution(beaker);
            foreach (var (reagent, amount) in new[] { ("CMDylovene", 10), ("RMCUranium", 5), ("RMCGold", 1), ("CMUUnstableMutagen", 5) })
            {
                Assert.That(solutions.TryAddReagent(soln, reagent, FixedPoint2.New(amount)), Is.True, reagent);
            }

            var solution = soln.Comp.Solution;
            Close(beaker);
            Assert.That(Shake(beaker), Is.True);
            Assert.That(solution.GetTotalPrototypeQuantity("CMUStabilizedMutagen"), Is.EqualTo(FixedPoint2.Zero),
                "the cold-gated reaction fired at room temperature");

            solutions.SetTemperature(soln, 260f);
            Assert.That(solution.GetTotalPrototypeQuantity("CMUStabilizedMutagen"), Is.EqualTo(FixedPoint2.Zero),
                "the cold-gated reaction fired without a Mix");

            Assert.That(Shake(beaker), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(solution.GetTotalPrototypeQuantity("CMUStabilizedMutagen"), Is.EqualTo(FixedPoint2.New(15)),
                    "a cold, mixed beaker should make stabilized mutagen");
                Assert.That(solution.Temperature, Is.EqualTo(260f).Within(0.01f), "the reaction should not self-heat");
            });
        });
    }

    [Test]
    public async Task SpaceDrugsNeedHeat()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var beaker = SSpawnAtPosition("CMBeaker", map.GridCoords);
            var soln = BeakerSolution(beaker);
            foreach (var reagent in new[] { "RMCMercury", "RMCSugar", "RMCLithium" })
            {
                Assert.That(solutions.TryAddReagent(soln, reagent, FixedPoint2.New(5)), Is.True, reagent);
            }

            var solution = soln.Comp.Solution;
            Assert.That(solution.GetTotalPrototypeQuantity("RMCSpaceDrugs"), Is.EqualTo(FixedPoint2.Zero));

            solutions.SetTemperature(soln, 330f);
            Assert.That(solution.GetTotalPrototypeQuantity("RMCSpaceDrugs"), Is.EqualTo(FixedPoint2.Zero),
                "space drugs formed below 340 K");

            solutions.SetTemperature(soln, 340f);
            Assert.That(solution.GetTotalPrototypeQuantity("RMCSpaceDrugs"), Is.EqualTo(FixedPoint2.New(15)));
        });
    }

    [Test]
    public async Task NapalmUTAndBNeedMix()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var ut = Fill(map, ("RMCNapalm", 5), ("RMCPhoron", 5), ("RMCCLF3", 5));
            var b = Fill(map, ("RMCNapalm", 10), ("RMCPhoron", 5), ("RMCMethane", 5), ("RMCCLF3", 5));
            var utSolution = BeakerSolution(ut).Comp.Solution;
            var bSolution = BeakerSolution(b).Comp.Solution;

            Assert.Multiple(() =>
            {
                Assert.That(utSolution.GetTotalPrototypeQuantity("RMCNapalmUT"), Is.EqualTo(FixedPoint2.Zero),
                    "napalm UT formed without a Mix");
                Assert.That(bSolution.GetTotalPrototypeQuantity("RMCNapalmB"), Is.EqualTo(FixedPoint2.Zero),
                    "napalm B formed without a Mix");
                Assert.That(bSolution.GetTotalPrototypeQuantity("RMCNapalmUT"), Is.EqualTo(FixedPoint2.Zero));
            });

            Close(ut);
            Close(b);
            Assert.That(Shake(ut), Is.True);
            Assert.That(Shake(b), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(utSolution.GetTotalPrototypeQuantity("RMCNapalmUT"), Is.EqualTo(FixedPoint2.New(15)));
                Assert.That(bSolution.GetTotalPrototypeQuantity("RMCNapalmB"), Is.EqualTo(FixedPoint2.New(10)),
                    "napalm UT ate napalm B's reactants");
            });
        });
    }

    // FLOW-01 (GraphAudit-ProcgenIgnitionHeat): ClF3 and methane used to keep their batch's heat while losing units
    // (ClF3 + water ~733 K, methane ~1466 K) and carried it into every recipe they were poured into.
    [Test]
    public async Task PyrotechnicSynthesisDoesNotSelfHeat()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            // Water first: chlorine and fluorine without it detonate (RMCCLF3Bad).
            var clf3 = Fill(map, ("Water", 5), ("RMCChlorine", 5), ("RMCFluorine", 15));
            var methane = Fill(map, ("RMCCarbon", 5), ("RMCHydrogen", 20));
            var clf3Solution = BeakerSolution(clf3).Comp.Solution;
            var methaneSolution = BeakerSolution(methane).Comp.Solution;

            Assert.Multiple(() =>
            {
                Assert.That(clf3Solution.GetTotalPrototypeQuantity("RMCCLF3"), Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(clf3Solution.Temperature, Is.EqualTo(293.15f).Within(1f),
                    "ClF3 synthesis (and the water its by-products form) concentrated the batch's heat");
                Assert.That(methaneSolution.GetTotalPrototypeQuantity("RMCMethane"), Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(methaneSolution.Temperature, Is.EqualTo(293.15f).Within(1f),
                    "methane synthesis concentrated the batch's heat");
            });
        });
    }

    // STATE-02: adding a reagent at a given temperature blends its heat by quantity.
    [Test]
    public async Task AddingAReagentAtATemperatureBlendsByQuantity()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var beaker = Fill(map, ("Water", 10));
            var soln = BeakerSolution(beaker);
            solutions.SetTemperature(soln, 300f);
            Assert.That(solutions.TryAddReagent(soln, "Water", FixedPoint2.New(10), temperature: 200f), Is.True);
            Assert.That(soln.Comp.Solution.Temperature, Is.EqualTo(250f).Within(0.01f),
                "10u at 300 K plus 10u at 200 K should settle at 250 K");

            var empty = SSpawnAtPosition("CMBeaker", map.GridCoords);
            var emptySoln = BeakerSolution(empty);
            Assert.That(solutions.TryAddReagent(emptySoln, "Water", FixedPoint2.New(10), temperature: 260f), Is.True);
            Assert.That(emptySoln.Comp.Solution.Temperature, Is.EqualTo(260f).Within(0.01f),
                "reagent poured into an empty container keeps its own temperature");
        });
    }

    // FLOW-03: the ChemMaster moves reagents one at a time; each move must carry its temperature, and an emptied
    // buffer must not keep the heat of what it last held.
    [Test]
    public async Task ChemMasterKeepsTemperatureAndResetsAnEmptyBuffer()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var chemMaster = SSpawnAtPosition("CMChemMaster", map.GridCoords);
            var beaker = Fill(map, ("Water", 10));
            var beakerSoln = BeakerSolution(beaker);
            solutions.SetTemperature(beakerSoln, 260f);

            var master = SComp<RMCChemMasterComponent>(chemMaster);
            Assert.That(Server.System<ItemSlotsSystem>().TryInsert(chemMaster, master.BeakerSlot, beaker, null), Is.True);
            Assert.That(solutions.TryGetSolution(chemMaster, master.BufferSolutionId, out var buffer, out var bufferSolution), Is.True);

            Send(chemMaster, new RMCChemMasterBeakerTransferMsg("Water", FixedPoint2.New(10)));
            Assert.Multiple(() =>
            {
                Assert.That(bufferSolution!.GetTotalPrototypeQuantity("Water"), Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(bufferSolution.Temperature, Is.EqualTo(260f).Within(0.01f),
                    "a chilled reagent warmed to the buffer's temperature on the way in");
            });

            // A hot buffer, emptied back into the beaker: the beaker gets the heat and the buffer forgets it.
            solutions.SetTemperature(buffer!.Value, 600f);
            Send(chemMaster, new RMCChemMasterBufferTransferMsg("Water", FixedPoint2.New(10)));
            Assert.Multiple(() =>
            {
                Assert.That(bufferSolution!.Volume, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(bufferSolution.Temperature, Is.EqualTo(293.15f).Within(0.01f),
                    "an empty buffer kept the heat of what it last held");
                Assert.That(beakerSoln.Comp.Solution.Temperature, Is.EqualTo(600f).Within(0.01f),
                    "the reagent lost its heat on the way out of the buffer");
            });
        });
    }

    private void Send(EntityUid chemMaster, BoundUserInterfaceMessage msg)
    {
        msg.UiKey = RMCChemMasterUI.Key;
        msg.Actor = chemMaster;
        SEntMan.EventBus.RaiseLocalEvent(chemMaster, (object) msg);
    }

    private EntityUid Fill(Robust.UnitTesting.Pool.TestMapData map, params (string Reagent, int Amount)[] reagents)
    {
        var solutions = Server.System<SharedSolutionContainerSystem>();
        var beaker = SSpawnAtPosition("CMBeaker", map.GridCoords);
        var soln = BeakerSolution(beaker);
        foreach (var (reagent, amount) in reagents)
        {
            Assert.That(solutions.TryAddReagent(soln, reagent, FixedPoint2.New(amount)), Is.True, reagent);
        }

        return beaker;
    }

    private void Close(EntityUid beaker)
    {
        Server.System<OpenableSystem>().SetOpen(beaker, false);
    }

    // what the Mix verb's doafter does once it finishes
    private bool Shake(EntityUid beaker)
    {
        return Server.System<ShakeableSystem>().TryShake(beaker);
    }

    private Entity<SolutionComponent> BeakerSolution(EntityUid beaker)
    {
        var name = SComp<FitsInDispenserComponent>(beaker).Solution;
        Assert.That(Server.System<SharedSolutionContainerSystem>().TryGetSolution(beaker, name, out var soln, out _), Is.True);
        return soln!.Value;
    }
}
