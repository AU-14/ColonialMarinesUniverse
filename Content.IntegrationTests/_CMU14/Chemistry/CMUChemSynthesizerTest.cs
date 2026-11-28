using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Automation;
using Content.Server.CMU14.Chemistry.Reagents;
using Content.Server.CMU14.Chemistry.Synthesizer;
using Content.Server.CMU14.Fluids;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using System.Linq;
using Content.Server.CMU14.Chemistry.Research;
using Content.Shared.CMU14.Automation;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.CMU14.Chemistry.Research;
using Content.Shared.CMU14.Chemistry.Synthesizer;
using Content.Shared.CMU14.Fluids;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Power.Components;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Chemistry;

/// <summary>Shared helpers for the chemical synthesizer tests (N10, GraphAudit-ChemBotanyPlan).</summary>
public abstract class CMUChemSynthesizerTestBase : GameTest
{
    protected const string Synthesizer = "CMUChemSynthesizer";

    protected EntityUid SpawnSynthesizer(Robust.UnitTesting.Pool.TestMapData map)
    {
        var synth = SSpawnAtPosition(Synthesizer, map.GridCoords);
        Server.System<PowerReceiverSystem>().SetNeedsPower(synth, false);
        // No power net on the test map recomputes Powered, so set it as CMUReactionGatesTest does.
        var receiver = SComp<ApcPowerReceiverComponent>(synth);
        receiver.Powered = true;
        SEntMan.Dirty(synth, receiver);
        return synth;
    }

    protected EntityUid Beaker(Robust.UnitTesting.Pool.TestMapData map, params (string Reagent, int Amount)[] reagents)
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

    protected void Insert(EntityUid synth, string slot, EntityUid item)
    {
        Assert.That(Server.System<ItemSlotsSystem>().TryInsert(synth, slot, item, null), Is.True, slot);
    }

    protected Entity<SolutionComponent> BeakerSolution(EntityUid beaker)
    {
        var name = SComp<FitsInDispenserComponent>(beaker).Solution;
        Assert.That(Server.System<SharedSolutionContainerSystem>().TryGetSolution(beaker, name, out var soln, out _), Is.True);
        return soln!.Value;
    }

    protected FixedPoint2 Quantity(EntityUid beaker, string reagent)
    {
        return BeakerSolution(beaker).Comp.Solution.GetTotalPrototypeQuantity(reagent);
    }

    protected EntityUid Report(Robust.UnitTesting.Pool.TestMapData map, string reagentId, bool valid, int genTier = 1)
    {
        var report = SSpawnAtPosition("CMUResearchReportFull", map.GridCoords);
        var comp = SComp<ResearchReportComponent>(report);
        comp.Data = new GeneratedReagentData
        {
            ID = reagentId,
            Name = reagentId,
            GenTier = genTier,
            Effects = [],
            Recipe = [],
        };
        comp.Valid = valid;
        comp.Completed = valid;
        return report;
    }

    protected GeneratedReagentData Generate(string id, int tier, string[] ingredients, float minTemp = 0f,
        params (string Property, int Level)[] effects)
    {
        var data = new GeneratedReagentData
        {
            ID = id,
            Name = id,
            GenTier = tier,
            Class = ReagentClass.Ultra,
            Effects = [],
            Recipe = [],
            RequiredMinTemp = minTemp,
        };
        foreach (var (property, level) in effects)
            data.Effects[property] = level;
        foreach (var ingredient in ingredients)
            data.Recipe[ingredient] = (1, false);
        SEntMan.EventBus.RaiseEvent(EventSource.Local, new GenerateReagentEvent(data));
        return data;
    }

    protected Entity<SolutionComponent> Chamber(EntityUid synth)
    {
        Assert.That(Server.System<SharedSolutionContainerSystem>().TryGetSolution(synth, "chamber", out var soln, out _), Is.True);
        return soln!.Value;
    }

    /// <summary>A vial (the sample slot's whitelist) holding <paramref name="amount"/> of one reagent.</summary>
    protected EntityUid Vial(Robust.UnitTesting.Pool.TestMapData map, string reagent, int amount)
    {
        var vial = SSpawnAtPosition("RMCVial", map.GridCoords);
        AddTo(VialSolution(vial), reagent, amount);
        return vial;
    }

    protected Entity<SolutionComponent> VialSolution(EntityUid vial)
    {
        Assert.That(Server.System<SharedSolutionContainerSystem>().TryGetSolution(vial, "beaker", out var soln, out _), Is.True);
        return soln!.Value;
    }

    protected void AddTo(Entity<SolutionComponent> solution, string reagent, int amount)
    {
        Assert.That(Server.System<SharedSolutionContainerSystem>().TryAddReagent(solution, reagent, FixedPoint2.New(amount)), Is.True, reagent);
    }

    /// <summary>Inserts as a player would (a non-null user), which the synthesizer's automation gates never refuse.</summary>
    protected void InsertAsPlayer(Robust.UnitTesting.Pool.TestMapData map, EntityUid synth, string slot, EntityUid item)
    {
        var user = SEntMan.SpawnEntity(null, map.GridCoords);
        Assert.That(Server.System<ItemSlotsSystem>().TryInsert(synth, slot, item, user), Is.True, slot);
    }

    protected FluidPort Port(EntityUid synth, string id)
    {
        return SComp<FluidPortsComponent>(synth).Ports.Single(p => p.Id == id);
    }
}

[TestFixture]
public sealed class CMUChemSynthesizerTest : CMUChemSynthesizerTestBase
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private const string PF = "CMUPhenolFormaldehyde";
    private const string Formaldehyde = "CMUFormaldehyde";
    private const string Phenol = "CMUPhenol";

    private (EntityUid Synth, EntityUid Formaldehyde, EntityUid Phenol, EntityUid Output, EntityUid Waste) Setup(
        Robust.UnitTesting.Pool.TestMapData map, int formaldehyde, int phenol, int batches, float energyPerSecond)
    {
        var synth = SpawnSynthesizer(map);
        var inFormaldehyde = Beaker(map, (Formaldehyde, formaldehyde));
        var inPhenol = Beaker(map, (Phenol, phenol));
        var output = Beaker(map);
        var waste = Beaker(map);
        Insert(synth, "input1", inFormaldehyde);
        Insert(synth, "input2", inPhenol);
        Insert(synth, "output", output);
        Insert(synth, "waste", waste);

        var system = Server.System<CMUChemSynthesizerSystem>();
        Assert.That(system.SetProgram(synth, PF), Is.True);
        system.SetBatches(synth, batches);
        SComp<CMUChemSynthesizerComponent>(synth).EnergyPerSecond = energyPerSecond;
        return (synth, inFormaldehyde, inPhenol, output, waste);
    }

    // PF: 2 formaldehyde + 1 phenol, minTemp 374, no catalyst (N2).
    [Test]
    public async Task StockHeatGatedReactionExactYield()
    {
        var map = await Pair.CreateTestMap();
        (EntityUid Synth, EntityUid Formaldehyde, EntityUid Phenol, EntityUid Output, EntityUid Waste) rig = default;

        await Server.WaitAssertion(() =>
        {
            rig = Setup(map, 10, 5, 5, 3000f);
            Assert.That(Server.System<CMUChemSynthesizerSystem>().TryStart(rig.Synth), Is.True);
        });

        await RunSeconds(6);

        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Idle), comp.Fault);
                Assert.That(comp.Fault, Is.Null);
                Assert.That(Quantity(rig.Output, PF), Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(BeakerSolution(rig.Output).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(5)),
                    "only the product goes to the output beaker");
                Assert.That(Quantity(rig.Formaldehyde, Formaldehyde), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(Quantity(rig.Phenol, Phenol), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(Chamber(rig.Synth).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero), "the chamber should end empty");
                Assert.That(BeakerSolution(rig.Waste).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero), "exact amounts leave no waste");
            });
        });
    }

    [Test]
    public async Task MissingInputFaultsWithoutPartialBatch()
    {
        var map = await Pair.CreateTestMap();
        (EntityUid Synth, EntityUid Formaldehyde, EntityUid Phenol, EntityUid Output, EntityUid Waste) rig = default;

        await Server.WaitAssertion(() =>
        {
            // 5 batches need 5 phenol; only 2 are loaded
            rig = Setup(map, 10, 2, 5, 3000f);
            Assert.That(Server.System<CMUChemSynthesizerSystem>().TryStart(rig.Synth), Is.True);
        });

        await RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Fault));
                Assert.That(comp.Fault, Is.EqualTo(CMUChemSynthesizerSystem.FaultMissingInput));
                Assert.That(BeakerSolution(rig.Output).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(Chamber(rig.Synth).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero),
                    "a short input must not leave a partial batch in the chamber");
                // nothing is pulled when an input is short, so the inputs are untouched and nothing reached waste
                Assert.That(Quantity(rig.Formaldehyde, Formaldehyde), Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(Quantity(rig.Phenol, Phenol), Is.EqualTo(FixedPoint2.New(2)));
                Assert.That(BeakerSolution(rig.Waste).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
                // FLOW-02: outside continuous mode the run is exactly Batches and a short input never waits
                Assert.That(comp.RunBatches, Is.EqualTo(5));
                Assert.That(comp.Waiting, Is.False);
            });
        });
    }

    private void Drain(EntityUid beaker)
    {
        Server.System<SharedSolutionContainerSystem>().RemoveAllSolution(BeakerSolution(beaker));
    }

    private static bool Running(CMUChemSynthesizerComponent comp)
    {
        return comp.Stage is not (CMUChemSynthesizerStage.Idle or CMUChemSynthesizerStage.Fault);
    }

    // FLOW-02: the right-click verb starts an idle programmed machine, stops a running one, and needs power to start.
    [Test]
    public async Task AlternativeVerbStartsAndStops()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var rig = Setup(map, 10, 5, 5, 10f);
            var user = SSpawnAtPosition("MobHuman", map.GridCoords);
            var verbs = SEntMan.System<SharedVerbSystem>();
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);

            AlternativeVerb? Find(string key)
            {
                var text = Loc.GetString(key);
                return verbs.GetLocalVerbs(rig.Synth, user, typeof(AlternativeVerb), force: true)
                    .OfType<AlternativeVerb>().FirstOrDefault(v => v.Text == text);
            }

            Assert.That(Find("cmu-chem-synth-verb-stop"), Is.Null, "an idle machine offers no Stop");
            var start = Find("cmu-chem-synth-verb-start");
            Assert.That(start, Is.Not.Null);
            // Below the slots' eject verbs (priority 0): alt-click must take a beaker out, never start or stop a run.
            Assert.That(start!.Priority, Is.LessThan(0), "Start would be the alt-click default");
            start!.Act!.Invoke();
            Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Metering), comp.Fault);

            Assert.That(Find("cmu-chem-synth-verb-start"), Is.Null, "a running machine offers no Start");
            var stop = Find("cmu-chem-synth-verb-stop");
            Assert.That(stop, Is.Not.Null);
            Assert.That(stop!.Priority, Is.LessThan(0), "Stop would be the alt-click default and dump the chamber");
            stop.Act!.Invoke();
            Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Idle));

            var receiver = SComp<ApcPowerReceiverComponent>(rig.Synth);
            receiver.Powered = false;
            Assert.That(Find("cmu-chem-synth-verb-start"), Is.Null, "an unpowered machine cannot be started");
        });
    }

    // FLOW-02: inputs for 3 batches with a maximum of 2 run 2 then 1 back to back, then wait without a fault.
    [Test]
    public async Task ContinuousRunsBackToBackThenWaits()
    {
        var map = await Pair.CreateTestMap();
        (EntityUid Synth, EntityUid Formaldehyde, EntityUid Phenol, EntityUid Output, EntityUid Waste) rig = default;
        var runs = new List<int>();

        await Server.WaitAssertion(() =>
        {
            rig = Setup(map, 6, 3, 2, 3000f);
            var system = Server.System<CMUChemSynthesizerSystem>();
            system.SetContinuous(rig.Synth, true);
            Assert.That(system.TryStart(rig.Synth), Is.True);
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            Assert.That(comp.RunBatches, Is.EqualTo(2));
            runs.Add(comp.RunBatches);
        });

        for (var i = 0; i < 20; i++)
        {
            await RunSeconds(1);
            var done = false;
            await Server.WaitAssertion(() =>
            {
                var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
                Assert.That(comp.Stage, Is.Not.EqualTo(CMUChemSynthesizerStage.Fault), comp.Fault);
                if (Running(comp) && runs[^1] != comp.RunBatches)
                    runs.Add(comp.RunBatches);

                done = comp.Waiting;
            });

            if (done)
                break;
        }

        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            Assert.Multiple(() =>
            {
                Assert.That(runs, Is.EqualTo(new[] { 2, 1 }), "a 2-batch run, then a 1-batch run");
                Assert.That(comp.Waiting, Is.True, "out of inputs, it waits");
                Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Idle));
                Assert.That(comp.Fault, Is.Null, "waiting is not a fault");
                Assert.That(comp.Continuous, Is.True);
                Assert.That(Quantity(rig.Output, PF), Is.EqualTo(FixedPoint2.New(3)));
                Assert.That(Quantity(rig.Formaldehyde, Formaldehyde), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(Quantity(rig.Phenol, Phenol), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(BeakerSolution(rig.Waste).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
            });
        });
    }

    // FLOW-02: a waiting machine stays idle while empty and resumes on its own once the missing input arrives.
    [Test]
    public async Task ContinuousWaitingResumesWhenInputArrives()
    {
        var map = await Pair.CreateTestMap();
        (EntityUid Synth, EntityUid Formaldehyde, EntityUid Phenol, EntityUid Output, EntityUid Waste) rig = default;

        await Server.WaitAssertion(() =>
        {
            rig = Setup(map, 4, 2, 2, 3000f);
            Drain(rig.Phenol);
            var system = Server.System<CMUChemSynthesizerSystem>();
            system.SetContinuous(rig.Synth, true);
            Assert.That(system.TryStart(rig.Synth), Is.False, "nothing to run yet");
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);

            // Stop while waiting ends the wait; continuous stays set, and the next start waits again.
            Assert.That(comp.Waiting, Is.True);
            system.Stop(rig.Synth);
            Assert.That(comp.Waiting, Is.False, "Stop did not end the wait");
            Assert.That(comp.Continuous, Is.True);
            Assert.That(system.TryStart(rig.Synth), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Waiting, Is.True);
                Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Idle));
                Assert.That(comp.Fault, Is.Null);
                Assert.That(comp.PlanBlockReagent, Is.EqualTo(Phenol), "the window names what it waits for");
            });
        });

        await RunSeconds(2.5f);

        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            Assert.That(comp.Waiting, Is.True);
            Assert.That(comp.Fault, Is.Null);
            Assert.That(Quantity(rig.Formaldehyde, Formaldehyde), Is.EqualTo(FixedPoint2.New(4)), "a waiting machine pulls nothing");
            AddTo(BeakerSolution(rig.Phenol), Phenol, 2);
        });

        // the wait check runs once per powered second
        await RunSeconds(1.5f);

        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            Assert.That(comp.Waiting, Is.False);
            Assert.That(Running(comp), Is.True, $"{comp.Stage} {comp.Fault}");
            Assert.That(comp.RunBatches, Is.EqualTo(2));
        });

        await RunSeconds(8);

        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            Assert.Multiple(() =>
            {
                Assert.That(Quantity(rig.Output, PF), Is.EqualTo(FixedPoint2.New(2)));
                Assert.That(comp.Waiting, Is.True, "used up again, it waits again");
                Assert.That(comp.Fault, Is.Null);
            });
        });
    }

    // FLOW-02: a real fault (here the program is gone) ends continuous waiting; the mode itself stays on.
    [Test]
    public async Task ContinuousRealFaultStops()
    {
        var map = await Pair.CreateTestMap();
        (EntityUid Synth, EntityUid Formaldehyde, EntityUid Phenol, EntityUid Output, EntityUid Waste) rig = default;

        await Server.WaitAssertion(() =>
        {
            rig = Setup(map, 10, 5, 2, 3000f);
            Drain(rig.Phenol);
            var system = Server.System<CMUChemSynthesizerSystem>();
            system.SetContinuous(rig.Synth, true);
            Assert.That(system.TryStart(rig.Synth), Is.False);
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            Assert.That(comp.Waiting, Is.True);
            comp.Program = null;
        });

        await RunSeconds(1.5f);

        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Fault));
                Assert.That(comp.Fault, Is.EqualTo(CMUChemSynthesizerSystem.FaultNoProgram));
                Assert.That(comp.Waiting, Is.False, "a real fault ends the waiting");
                Assert.That(comp.Continuous, Is.True, "the mode itself is kept");
            });
        });

        await RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Fault), "a faulted machine does not retry");
            Assert.That(comp.Waiting, Is.False);
        });
    }

    [Test]
    public async Task PowerLossPausesWithoutDuplication()
    {
        var map = await Pair.CreateTestMap();
        (EntityUid Synth, EntityUid Formaldehyde, EntityUid Phenol, EntityUid Output, EntityUid Waste) rig = default;
        var pausedTemperature = 0f;

        await Server.WaitAssertion(() =>
        {
            // slow heating so the run is still conditioning when the power goes
            rig = Setup(map, 10, 5, 5, 10f);
            Assert.That(Server.System<CMUChemSynthesizerSystem>().TryStart(rig.Synth), Is.True);
        });

        // metering at 1 s, first conditioning step at 2 s
        await RunSeconds(2.5f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<CMUChemSynthesizerComponent>(rig.Synth).Stage, Is.EqualTo(CMUChemSynthesizerStage.Conditioning));
            Assert.That(Chamber(rig.Synth).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(15)));
            // Powered was forced on by hand (no power net on the test map), so cut it the same way.
            var receiver = SComp<ApcPowerReceiverComponent>(rig.Synth);
            receiver.Powered = false;
            SEntMan.Dirty(rig.Synth, receiver);
        });

        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<PowerReceiverSystem>().IsPowered(rig.Synth), Is.False);
            pausedTemperature = Chamber(rig.Synth).Comp.Solution.Temperature;
        });

        await RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            var chamber = Chamber(rig.Synth).Comp.Solution;
            Assert.Multiple(() =>
            {
                Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Conditioning), "an unpowered machine must not advance");
                Assert.That(chamber.Temperature, Is.EqualTo(pausedTemperature).Within(0.01f), "an unpowered machine must not heat");
                Assert.That(chamber.Volume, Is.EqualTo(FixedPoint2.New(15)));
                Assert.That(Quantity(rig.Formaldehyde, Formaldehyde), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(Quantity(rig.Phenol, Phenol), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(BeakerSolution(rig.Output).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
            });

            comp.EnergyPerSecond = 3000f;
            var powered = SComp<ApcPowerReceiverComponent>(rig.Synth);
            powered.Powered = true;
            SEntMan.Dirty(rig.Synth, powered);
        });

        await RunSeconds(4);

        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Idle), comp.Fault);
                Assert.That(Quantity(rig.Output, PF), Is.EqualTo(FixedPoint2.New(5)), "resuming must yield exactly one batch run");
                Assert.That(Chamber(rig.Synth).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(BeakerSolution(rig.Waste).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(Quantity(rig.Formaldehyde, Formaldehyde), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(Quantity(rig.Phenol, Phenol), Is.EqualTo(FixedPoint2.Zero));
            });
        });
    }

    // Reprints and simulator reports carry Valid=false without the partial prototype.
    [Test]
    public async Task InvalidReportIsRefused()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var invalid = SpawnSynthesizer(map);
            Insert(invalid, "program", Report(map, PF, valid: false));
            var valid = SpawnSynthesizer(map);
            Insert(valid, "program", Report(map, PF, valid: true));

            var invalidComp = SComp<CMUChemSynthesizerComponent>(invalid);
            var validComp = SComp<CMUChemSynthesizerComponent>(valid);
            Assert.Multiple(() =>
            {
                Assert.That(invalidComp.Program, Is.Null);
                Assert.That(invalidComp.Fault, Is.EqualTo(CMUChemSynthesizerSystem.FaultInvalidReport));
                Assert.That(validComp.Program, Is.EqualTo(PF), validComp.Fault);
                Assert.That(validComp.ProgramProduct, Is.EqualTo(PF));
            });
        });
    }

    // M3: reactants only in the fluid tank. Tank amounts are truncated to FixedPoint2 (9.999 is short of 10) and exactly
    // the truncated amount leaves the tank (10.004 leaves 0.004), so nothing is duplicated (not FLOW-15).
    [Test]
    public async Task FluidFedExactYield()
    {
        var map = await Pair.CreateTestMap();
        EntityUid synth = default;
        EntityUid output = default;
        EntityUid waste = default;

        await Server.WaitAssertion(() =>
        {
            synth = SpawnSynthesizer(map);
            output = Beaker(map);
            waste = Beaker(map);
            Insert(synth, "output", output);
            Insert(synth, "waste", waste);

            var fluids = Server.System<FluidSystem>();
            Assert.That(fluids.Add(synth, Formaldehyde, 9.999f), Is.EqualTo(9.999f).Within(0.0001f));
            Assert.That(fluids.Add(synth, Phenol, 5f), Is.EqualTo(5f).Within(0.0001f));

            var system = Server.System<CMUChemSynthesizerSystem>();
            Assert.That(system.SetProgram(synth, PF), Is.True);
            system.SetBatches(synth, 5);
            SComp<CMUChemSynthesizerComponent>(synth).EnergyPerSecond = 3000f;
            Assert.That(system.TryStart(synth), Is.True);
        });

        await RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            var fluids = Server.System<FluidSystem>();
            var comp = SComp<CMUChemSynthesizerComponent>(synth);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Fault, Is.EqualTo(CMUChemSynthesizerSystem.FaultMissingInput), "9.999 truncates to 9.99, short of 10");
                Assert.That(fluids.GetAmount(synth, Formaldehyde), Is.EqualTo(9.999f).Within(0.0001f), "a short meter pulls nothing");
                Assert.That(fluids.GetAmount(synth, Phenol), Is.EqualTo(5f).Within(0.0001f));
                Assert.That(Chamber(synth).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
            });

            fluids.Add(synth, Formaldehyde, 0.005f);
            Assert.That(Server.System<CMUChemSynthesizerSystem>().TryStart(synth), Is.True);
        });

        await RunSeconds(6);

        await Server.WaitAssertion(() =>
        {
            var fluids = Server.System<FluidSystem>();
            var comp = SComp<CMUChemSynthesizerComponent>(synth);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Idle), comp.Fault);
                Assert.That(Quantity(output, PF), Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(BeakerSolution(output).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(fluids.GetAmount(synth, Formaldehyde), Is.EqualTo(0.004f).Within(0.001f),
                    "exactly the metered 10u must leave the tank");
                Assert.That(fluids.GetAmount(synth, Phenol), Is.Zero.Within(0.0001f));
                Assert.That(BeakerSolution(waste).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero), "exact amounts leave no waste");
                Assert.That(Chamber(synth).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
            });
        });
    }

    // FLOW-13: the product port carries only the product; surplus reactant in the tank never leaves through it.
    [Test]
    public async Task ProductLeavesThroughPortNoReactantLeak()
    {
        var map = await Pair.CreateTestMap();
        EntityUid synth = default;
        EntityUid output = default;
        EntityUid pipe = default;

        await Server.WaitAssertion(() =>
        {
            synth = SpawnSynthesizer(map);
            output = Beaker(map);
            Insert(synth, "output", output);
            Insert(synth, "waste", Beaker(map));

            var fluids = Server.System<FluidSystem>();
            fluids.Add(synth, Formaldehyde, 30f); // 20 more than the run needs
            fluids.Add(synth, Phenol, 5f);

            pipe = SSpawnAtPosition("RUMCSteelPipe", map.GridCoords);
            Port(synth, "product_out").Links.Add(pipe);

            var system = Server.System<CMUChemSynthesizerSystem>();
            Assert.That(system.SetProgram(synth, PF), Is.True);
            Assert.That(Port(synth, "product_out").Kind, Is.EqualTo(PF));
            system.SetBatches(synth, 5);
            SComp<CMUChemSynthesizerComponent>(synth).EnergyPerSecond = 3000f;
            Assert.That(system.TryStart(synth), Is.True);
        });

        await RunSeconds(8);

        await Server.WaitAssertion(() =>
        {
            var fluids = Server.System<FluidSystem>();
            var comp = SComp<CMUChemSynthesizerComponent>(synth);
            var received = SComp<FluidPipeComponent>(pipe);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Idle), comp.Fault);
                Assert.That(Port(synth, "product_out").Links, Does.Contain(pipe), "the link should still be valid");
                Assert.That(received.Kind, Is.EqualTo(PF), "only the product may leave through the product port");
                Assert.That(received.Amount, Is.EqualTo(5f).Within(0.01f));
                Assert.That(fluids.GetAmount(synth, PF), Is.Zero.Within(0.01f), "the port should have carried the product away");
                Assert.That(fluids.GetAmount(synth, Formaldehyde), Is.EqualTo(20f).Within(0.001f), "surplus reactant must stay in the tank");
                Assert.That(fluids.GetAmount(synth, Phenol), Is.Zero.Within(0.0001f));
                Assert.That(Quantity(output, PF), Is.EqualTo(FixedPoint2.Zero), "a linked port takes the product instead of the beaker");
            });
        });
    }

    // FLOW-13: a port with no kind pushes the tank's largest content; unprogrammed, the product port must be off.
    [Test]
    public async Task UnprogrammedProductPortPushesNothing()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var synth = SpawnSynthesizer(map);
            var fluids = Server.System<FluidSystem>();
            fluids.Add(synth, Formaldehyde, 30f);
            var pipe = SSpawnAtPosition("RUMCSteelPipe", map.GridCoords);
            var port = Port(synth, "product_out");
            port.Links.Add(pipe);
            Assert.That(port.Kind, Is.Null);
            Assert.That(port.Scale, Is.Zero, "an unprogrammed product port must be switched off at spawn");

            for (var i = 0; i < 4; i++)
                fluids.Update(0.5f);

            Assert.That(SComp<FluidPipeComponent>(pipe).Amount, Is.Zero, "an unprogrammed machine must not pump reactants out");
            Assert.That(fluids.GetAmount(synth, Formaldehyde), Is.EqualTo(30f).Within(0.0001f));

            // programmed: the port carries only the product, of which there is none yet
            Assert.That(Server.System<CMUChemSynthesizerSystem>().SetProgram(synth, PF), Is.True);
            Assert.That(port.Kind, Is.EqualTo(PF));
            Assert.That(port.Scale, Is.GreaterThan(0f));
            for (var i = 0; i < 4; i++)
                fluids.Update(0.5f);

            Assert.That(SComp<FluidPipeComponent>(pipe).Amount, Is.Zero);
            Assert.That(fluids.GetAmount(synth, Formaldehyde), Is.EqualTo(30f).Within(0.0001f));
        });
    }

    // The running cycle reserves tank room for its products, so inflows cannot take it; STATE-03 purge of dead kinds.
    [Test]
    public async Task TankReservation()
    {
        var map = await Pair.CreateTestMap();
        (EntityUid Synth, EntityUid Formaldehyde, EntityUid Phenol, EntityUid Output, EntityUid Waste) rig = default;
        const float capacity = 360f;

        await Server.WaitAssertion(() =>
        {
            rig = Setup(map, 10, 5, 5, 10f);
            var tank = (rig.Synth, SComp<FluidTankComponent>(rig.Synth));
            Assert.That(tank.Item2.Capacity, Is.EqualTo(capacity));
            Assert.That(Server.System<FluidSystem>().Room(tank, FluidKinds.Water), Is.EqualTo(capacity).Within(0.001f));
            Assert.That(Server.System<CMUChemSynthesizerSystem>().TryStart(rig.Synth), Is.True);
        });

        await RunSeconds(2.5f);

        await Server.WaitAssertion(() =>
        {
            var fluids = Server.System<FluidSystem>();
            var system = Server.System<CMUChemSynthesizerSystem>();
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            var tank = (rig.Synth, SComp<FluidTankComponent>(rig.Synth));
            Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Conditioning));
            Assert.That(system.GetReservedFluid(comp), Is.EqualTo(5f).Within(0.001f), "5 batches of 1u product");
            Assert.That(fluids.Room(tank, FluidKinds.Water), Is.EqualTo(capacity).Within(0.001f),
                "an unlinked product port sends the product to the beaker, so nothing is reserved");

            // the reservation holds room for the port's product only while the port is linked
            var pipe = SSpawnAtPosition("RUMCSteelPipe", map.GridCoords);
            var port = Port(rig.Synth, "product_out");
            port.Links.Add(pipe);
            Assert.That(fluids.Room(tank, FluidKinds.Water), Is.EqualTo(capacity - 5f).Within(0.001f));
            Assert.That(fluids.Add(rig.Synth, FluidKinds.Water, 400f), Is.EqualTo(capacity - 5f).Within(0.001f),
                "inflows must not fill the reserved room");
            port.Links.Remove(pipe);
            comp.EnergyPerSecond = 3000f;
        });

        await RunSeconds(4);

        await Server.WaitAssertion(() =>
        {
            var fluids = Server.System<FluidSystem>();
            var system = Server.System<CMUChemSynthesizerSystem>();
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            var tank = (rig.Synth, SComp<FluidTankComponent>(rig.Synth));
            Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Idle), comp.Fault);
            Assert.That(Quantity(rig.Output, PF), Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(system.GetReservedFluid(comp), Is.Zero, "an idle machine reserves nothing");
            Assert.That(fluids.Room(tank, FluidKinds.Water), Is.EqualTo(5f).Within(0.001f));

            // STATE-03: a kind that no longer resolves to a reagent is dropped, real ones stay
            const string gone = "CMUSynthesizerTest-NoSuchReagent";
            fluids.Remove(rig.Synth, FluidKinds.Water, 10f);
            fluids.Add(rig.Synth, gone, 5f);
            system.PurgeUnknownFluids((rig.Synth, comp));
            Assert.That(fluids.GetAmount(rig.Synth, gone), Is.Zero);
            Assert.That(fluids.GetAmount(rig.Synth, FluidKinds.Water), Is.EqualTo(capacity - 15f).Within(0.001f));
        });
    }

    // E4: automation (a null user) is gated by slot; players are not.
    [Test]
    public async Task InserterSlotGates()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            // unpowered: the machine never steps, so the stage can be set by hand
            var synth = SSpawnAtPosition(Synthesizer, map.GridCoords);
            var slots = Server.System<ItemSlotsSystem>();
            var comp = SComp<CMUChemSynthesizerComponent>(synth);
            ItemSlot Slot(string id)
            {
                Assert.That(slots.TryGetSlot(synth, id, out var slot), Is.True, id);
                return slot!;
            }

            var empty = Beaker(map);
            var full = Beaker(map, (Phenol, 5));
            Assert.That(slots.TryInsert(synth, "input1", empty, null), Is.False, "inputs take only beakers with something in them");
            Assert.That(slots.TryInsert(synth, "input1", full, null), Is.True);
            Assert.That(slots.TryInsert(synth, "output", Beaker(map, (PF, 1)), null), Is.False, "output takes only empty beakers");
            Assert.That(slots.TryInsert(synth, "waste", Beaker(map, (PF, 1)), null), Is.False, "waste takes only empty beakers");
            var output = Beaker(map);
            Assert.That(slots.TryInsert(synth, "output", output, null), Is.True);
            var vial = Vial(map, PF, 30);
            Assert.That(slots.TryInsert(synth, "sample", vial, null), Is.False, "the sample is never loaded by automation");
            InsertAsPlayer(map, synth, "sample", vial);

            // inputs leave only once drained
            Assert.That(slots.CanEject(synth, Slot("input1"), null), Is.False);
            Server.System<SharedSolutionContainerSystem>().RemoveAllSolution(BeakerSolution(full));
            Assert.That(slots.CanEject(synth, Slot("input1"), null), Is.True);

            // output: not while empty, not while busy unless full
            Assert.That(slots.CanEject(synth, Slot("output"), null), Is.False, "an empty output stays");
            AddTo(BeakerSolution(output), PF, 5);
            Assert.That(slots.CanEject(synth, Slot("output"), null), Is.True, "a resting machine's product may leave");
            comp.Stage = CMUChemSynthesizerStage.Reacting;
            Assert.That(slots.CanEject(synth, Slot("output"), null), Is.False, "a busy machine keeps a partial output");
            AddTo(BeakerSolution(output), PF, 55);
            Assert.That(slots.CanEject(synth, Slot("output"), null), Is.True, "a full output may leave");
            comp.Stage = CMUChemSynthesizerStage.Idle;

            Assert.That(slots.CanEject(synth, Slot("sample"), null), Is.False, "the sample never leaves by automation");
            var user = SEntMan.SpawnEntity(null, map.GridCoords);
            Assert.That(slots.CanEject(synth, Slot("sample"), user), Is.True, "players are not gated");
        });
    }

    // E4 end to end with a real arm: empty beakers go to output then waste (they are in the stack slots after the
    // inputs), a third empty one is refused, and a full one is loaded into an input.
    [Test]
    public async Task InserterRoutesBeakersBySlot()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = 0; x <= 2; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);

            var arm = SSpawnAtPosition("CMUInserterArm", map.GridCoords.Offset(new Vector2(1, 0)));
            var synth = SSpawnAtPosition(Synthesizer, map.GridCoords.Offset(new Vector2(2, 0)));
            Server.System<SharedTransformSystem>().SetLocalRotation(arm, Angle.FromDegrees(90));
            var arms = Server.System<CMUInserterSystem>();
            var slots = Server.System<ItemSlotsSystem>();
            var claw = SComp<CMUInserterComponent>(arm).Claw;

            void RunArm(Func<bool> done)
            {
                for (var i = 0; i < 12 && !done(); i++)
                    arms.Update(1);
            }

            var emptyOutput = Beaker(map);
            RunArm(() => slots.GetItemOrNull(synth, "output") == emptyOutput);
            Assert.That(slots.GetItemOrNull(synth, "output"), Is.EqualTo(emptyOutput), "an empty beaker goes to the output");

            var emptyWaste = Beaker(map);
            RunArm(() => slots.GetItemOrNull(synth, "waste") == emptyWaste);
            Assert.That(slots.GetItemOrNull(synth, "waste"), Is.EqualTo(emptyWaste), "the next empty beaker goes to waste");

            var spare = Beaker(map);
            for (var i = 0; i < 6; i++)
            {
                arms.Update(1);
                Assert.That(claw.ContainedEntity, Is.Null, "an empty beaker with nowhere to go must not be picked up");
            }

            SDeleteNow(spare);

            var full = Beaker(map, (Phenol, 5));
            RunArm(() => slots.GetItemOrNull(synth, "input1") == full);
            Assert.Multiple(() =>
            {
                Assert.That(slots.GetItemOrNull(synth, "input1"), Is.EqualTo(full), "a full beaker goes to an input");
                Assert.That(slots.GetItemOrNull(synth, "output"), Is.EqualTo(emptyOutput));
                Assert.That(slots.GetItemOrNull(synth, "waste"), Is.EqualTo(emptyWaste));
            });
        });
    }

    // M3: the window opens without power (it is the only way to the slots), and a start without power only pauses.
    [Test]
    public async Task UnpoweredMachineKeepsSlotsAndPauses()
    {
        var map = await Pair.CreateTestMap();
        EntityUid synth = default;
        EntityUid phenol = default;

        await Server.WaitAssertion(() =>
        {
            // Powered is set by hand (no power net on the test map), as in PowerLossPausesWithoutDuplication
            synth = SpawnSynthesizer(map);
            var receiver = SComp<ApcPowerReceiverComponent>(synth);
            receiver.Powered = false;
            SEntMan.Dirty(synth, receiver);
            Assert.That(SEntMan.HasComponent<ActivatableUIRequiresPowerComponent>(synth), Is.False,
                "the window must open during a power loss so beakers, report and sample can be taken out");

            phenol = Beaker(map, (Phenol, 5));
            Insert(synth, "input1", Beaker(map, (Formaldehyde, 10)));
            Insert(synth, "input2", phenol);
            var system = Server.System<CMUChemSynthesizerSystem>();
            Assert.That(system.SetProgram(synth, PF), Is.True);
            Assert.That(system.TryStart(synth), Is.True);
        });

        await RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUChemSynthesizerComponent>(synth);
            Assert.That(Server.System<PowerReceiverSystem>().IsPowered(synth), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Metering), "an unpowered machine must not advance");
                Assert.That(Quantity(phenol, Phenol), Is.EqualTo(FixedPoint2.New(5)), "an unpowered machine pulls nothing");
                Assert.That(Chamber(synth).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
            });

            var user = SEntMan.SpawnEntity(null, map.GridCoords);
            var slots = Server.System<ItemSlotsSystem>();
            Assert.That(slots.TryGetSlot(synth, "input2", out var slot), Is.True);
            Assert.That(slots.CanEject(synth, slot!, user), Is.True, "a player can still take a beaker out");
        });
    }

    // M3: batches are locked while running (the reservation reads them), and Stop releases the reservation.
    [Test]
    public async Task StopReleasesReservationAndBatchesLockWhileRunning()
    {
        var map = await Pair.CreateTestMap();
        (EntityUid Synth, EntityUid Formaldehyde, EntityUid Phenol, EntityUid Output, EntityUid Waste) rig = default;
        const float capacity = 360f;

        await Server.WaitAssertion(() =>
        {
            rig = Setup(map, 10, 5, 5, 10f);
            Port(rig.Synth, "product_out").Links.Add(SSpawnAtPosition("RUMCSteelPipe", map.GridCoords));
            Assert.That(Server.System<CMUChemSynthesizerSystem>().TryStart(rig.Synth), Is.True);
        });

        await RunSeconds(2.5f);

        await Server.WaitAssertion(() =>
        {
            var fluids = Server.System<FluidSystem>();
            var system = Server.System<CMUChemSynthesizerSystem>();
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            var tank = (rig.Synth, SComp<FluidTankComponent>(rig.Synth));
            Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Conditioning));
            Assert.That(fluids.Room(tank, FluidKinds.Water), Is.EqualTo(capacity - 5f).Within(0.001f));

            Assert.That(system.SetBatches(rig.Synth, 2), Is.False, "batches must not change mid-cycle");
            Assert.That(comp.Batches, Is.EqualTo(5));
            Assert.That(system.GetReservedFluid(comp), Is.EqualTo(5f).Within(0.001f));

            system.Stop(rig.Synth);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Idle));
                Assert.That(system.GetReservedFluid(comp), Is.Zero, "a stopped machine reserves nothing");
                Assert.That(fluids.Room(tank, FluidKinds.Water), Is.EqualTo(capacity).Within(0.001f));
                Assert.That(Chamber(rig.Synth).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(BeakerSolution(rig.Waste).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(15)),
                    "the aborted chamber goes to waste");
            });

            Assert.That(system.SetBatches(rig.Synth, 2), Is.True);
            Assert.That(comp.Batches, Is.EqualTo(2));
        });
    }

    // E4 end to end: the finished output is pulled, a busy machine's partial output and a loaded input are not.
    [Test]
    public async Task InserterPullsFinishedOutputOnly()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = 0; x <= 2; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);

            var synth = SSpawnAtPosition(Synthesizer, map.GridCoords);
            var arm = SSpawnAtPosition("CMUInserterArm", map.GridCoords.Offset(new Vector2(1, 0)));
            var belt = SSpawnAtPosition("RUMCConveyorBeltTile", map.GridCoords.Offset(new Vector2(2, 0)));
            Server.System<SharedTransformSystem>().SetLocalRotation(arm, Angle.FromDegrees(90));
            var arms = Server.System<CMUInserterSystem>();
            var slots = Server.System<ItemSlotsSystem>();

            var input = Beaker(map, (Phenol, 5));
            var output = Beaker(map);
            Insert(synth, "input1", input);
            Insert(synth, "output", output);
            AddTo(BeakerSolution(output), PF, 5);
            var comp = SComp<CMUChemSynthesizerComponent>(synth);

            comp.Stage = CMUChemSynthesizerStage.Reacting;
            arms.Update(1);
            Assert.That(SComp<CMUInserterComponent>(arm).Claw.ContainedEntity, Is.Null,
                "neither a busy machine's partial output nor a loaded input may be pulled");

            comp.Stage = CMUChemSynthesizerStage.Idle;
            arms.Update(1);
            Assert.That(SComp<CMUInserterComponent>(arm).Claw.ContainedEntity, Is.EqualTo(output));
            arms.Update(1);
            Assert.That(slots.GetItemOrNull(synth, "output"), Is.Null);
            Assert.That(slots.GetItemOrNull(synth, "input1"), Is.EqualTo(input));
            Assert.That(SComp<TransformComponent>(output).Coordinates, Is.EqualTo(SComp<TransformComponent>(belt).Coordinates));
        });
    }

    // ARCH-07 (E5): a stock reagent sample programs the machine with no research reward, and the sample stays.
    [Test]
    public async Task SampleProgramsStockReagent()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var research = Server.System<ServerResearchDataTerminalSystem>();
            var generator = Server.System<ServerReagentGeneratorSystem>();
            var credits = research.GetCredits("corporate");
            var sampleData = research.GetSampleData("corporate");
            var completed = research.GetResearch("corporate").CompletedChemicals.Count;
            var identified = generator.IdentifiedChemicals.Count;

            var synth = SpawnSynthesizer(map);
            var system = Server.System<CMUChemSynthesizerSystem>();
            var comp = SComp<CMUChemSynthesizerComponent>(synth);
            Assert.That(system.TryProgramFromSample(synth), Is.False);
            Assert.That(comp.Fault, Is.EqualTo(CMUChemSynthesizerSystem.FaultNoSample));

            var vial = Vial(map, PF, 20);
            InsertAsPlayer(map, synth, "sample", vial);
            Assert.That(system.TryProgramFromSample(synth), Is.False);
            Assert.That(comp.Fault, Is.EqualTo(CMUChemSynthesizerSystem.FaultSampleInsufficient), "the XRF scanner needs 30u");

            AddTo(VialSolution(vial), Phenol, 10);
            Assert.That(system.TryProgramFromSample(synth), Is.False);
            Assert.That(comp.Fault, Is.EqualTo(CMUChemSynthesizerSystem.FaultSampleContaminated));

            Server.System<SharedSolutionContainerSystem>().RemoveAllSolution(VialSolution(vial));
            AddTo(VialSolution(vial), PF, 30);
            Assert.That(system.TryProgramFromSample(synth), Is.True, comp.Fault);

            Assert.Multiple(() =>
            {
                Assert.That(comp.Program, Is.EqualTo(PF));
                Assert.That(comp.ProgramProduct, Is.EqualTo(PF));
                Assert.That(comp.Fault, Is.Null);
                Assert.That(Port(synth, "product_out").Kind, Is.EqualTo(PF));
                Assert.That(Server.System<ItemSlotsSystem>().GetItemOrNull(synth, "sample"), Is.EqualTo(vial), "the sample is not consumed");
                Assert.That(VialSolution(vial).Comp.Solution.GetTotalPrototypeQuantity(PF), Is.EqualTo(FixedPoint2.New(30)));
                Assert.That(research.GetCredits("corporate"), Is.EqualTo(credits), "a sample pays no credits");
                Assert.That(research.GetSampleData("corporate"), Is.EqualTo(sampleData), "a sample pays no sample data");
                Assert.That(research.GetResearch("corporate").CompletedChemicals.Count, Is.EqualTo(completed), "a sample completes nothing");
                Assert.That(generator.IdentifiedChemicals.Count, Is.EqualTo(identified), "a sample identifies nothing");
            });
        });
    }
}

/// <summary>An ignitable generated product is made under its ignition point, and an overheated chamber vents.</summary>
[TestFixture]
public sealed class CMUChemSynthesizerProcgenTest : CMUChemSynthesizerTestBase
{
    // Generated prototypes survive entity cleanup and would change later tests' reactions.
    public override PoolSettings PoolSettings => new() { Connected = false, Destructive = true };

    /// <summary>Ingredients known not to collide with a hand-authored reaction (see CMUProcgenIgnitionHeatTest).</summary>
    private static readonly string[] Ingredients = ["RMCGold", "RMCPlatinum", "CMUTearGas"];

    private const string Fuel = "CMUSynthesizerTest-Fuel";

    [Test]
    public async Task IgnitableProductNeverIgnitesAndOverheatVents()
    {
        var map = await Pair.CreateTestMap();
        EntityUid synth = default;
        var inputs = new EntityUid[Ingredients.Length];
        EntityUid output = default;
        EntityUid waste = default;
        const string residue = SharedReagentGeneratorSystem.IgnitionResidueReagent;

        await Server.WaitAssertion(() =>
        {
            // Fueling 6 ignites at the 410 K floor; heat-gated from 330 K, so the band is 330-399 K.
            var data = new GeneratedReagentData
            {
                ID = Fuel,
                Name = Fuel,
                GenTier = 2,
                Class = ReagentClass.Ultra,
                Effects = [],
                Recipe = [],
                RequiredMinTemp = 330f,
            };
            data.Effects["Fueling"] = 6;
            foreach (var ingredient in Ingredients)
                data.Recipe[ingredient] = (1, false);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new GenerateReagentEvent(data));

            synth = SpawnSynthesizer(map);
            for (var i = 0; i < Ingredients.Length; i++)
            {
                inputs[i] = Beaker(map, (Ingredients[i], 4));
                Insert(synth, $"input{i + 1}", inputs[i]);
            }

            output = Beaker(map);
            waste = Beaker(map);
            Insert(synth, "output", output);
            Insert(synth, "waste", waste);

            var system = Server.System<CMUChemSynthesizerSystem>();
            Assert.That(system.GetVentLimit([Fuel]),
                Is.EqualTo(SharedReagentGeneratorSystem.IgnitionTemperature(6) - SharedReagentGeneratorSystem.IgnitionGuardMargin).Within(0.01f));
            Assert.That(system.SetProgram(synth, Fuel), Is.True);
            system.SetBatches(synth, 4);
            SComp<CMUChemSynthesizerComponent>(synth).EnergyPerSecond = 3000f;
            Assert.That(system.TryStart(synth), Is.True);
        });

        await RunSeconds(6);

        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUChemSynthesizerComponent>(synth);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Idle), comp.Fault);
                Assert.That(Quantity(output, Fuel), Is.EqualTo(FixedPoint2.New(4)), "the whole batch should survive as fuel");
                Assert.That(Quantity(output, residue), Is.EqualTo(FixedPoint2.Zero), "the product ignited");
                Assert.That(Quantity(waste, residue), Is.EqualTo(FixedPoint2.Zero), "the product ignited");
                Assert.That(BeakerSolution(output).Comp.Solution.Temperature, Is.LessThan(SharedReagentGeneratorSystem.IgnitionTemperature(6)));
                Assert.That(Chamber(synth).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
            });

            // second run, held almost still so the test can overheat the chamber by hand
            var solutions = Server.System<SharedSolutionContainerSystem>();
            for (var i = 0; i < Ingredients.Length; i++)
            {
                Assert.That(solutions.TryAddReagent(BeakerSolution(inputs[i]), Ingredients[i], FixedPoint2.New(4)), Is.True);
            }

            comp.EnergyPerSecond = 0.01f;
            Assert.That(Server.System<CMUChemSynthesizerSystem>().TryStart(synth), Is.True);
        });

        // metering at 1 s
        await RunSeconds(1.5f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<CMUChemSynthesizerComponent>(synth).Stage, Is.EqualTo(CMUChemSynthesizerStage.Conditioning));
            var chamber = Chamber(synth);
            Assert.That(chamber.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(12)));
            // the chamber never reacts outside the Reacting stage, so this makes no fuel and burns nothing
            Server.System<SharedSolutionContainerSystem>().SetTemperature(chamber, 405f);
            Assert.That(chamber.Comp.Solution.GetTotalPrototypeQuantity(Fuel), Is.EqualTo(FixedPoint2.Zero));
        });

        await RunSeconds(1.2f);

        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUChemSynthesizerComponent>(synth);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Fault));
                Assert.That(comp.Fault, Is.EqualTo(CMUChemSynthesizerSystem.FaultOverheat));
                Assert.That(Chamber(synth).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero), "the chamber should be vented");
                Assert.That(BeakerSolution(waste).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(12)), "the vented chamber goes to waste");
                Assert.That(Quantity(output, Fuel), Is.EqualTo(FixedPoint2.New(4)), "no product from the vented run");
                Assert.That(Quantity(waste, residue), Is.EqualTo(FixedPoint2.Zero));
            });
        });
    }

    // GenTier >= 3 needs the faction to have completed the chemical; a uranium recipe programs its own reaction,
    // not the CMUGeneratedUraniumAlternative{ID} that sorts first.
    [Test]
    public async Task ReportProgrammingGatesAndRecipeChoice()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            // Hydro reactants are never hidden by a report, so only the tier gate can refuse these.
            const string hard = "CMUSynthesizerTest-Hard";
            Generate(hard, 3, ["CMUPhenol", "CMUFormaldehyde"], 0f, ("Toxic", 1)); // a property-less chem has a null effects list

            var refused = SpawnSynthesizer(map);
            Insert(refused, "program", Report(map, hard, valid: true, genTier: 3));
            var refusedComp = SComp<CMUChemSynthesizerComponent>(refused);
            Assert.That(refusedComp.Program, Is.Null);
            Assert.That(refusedComp.Fault, Is.EqualTo(CMUChemSynthesizerSystem.FaultUnresearched));

            // no research terminal on the test map, so the machine belongs to the corporate faction
            Server.System<ServerResearchDataTerminalSystem>().GetResearch("corporate").CompletedChemicals.Add(hard);
            var allowed = SpawnSynthesizer(map);
            Insert(allowed, "program", Report(map, hard, valid: true, genTier: 3));
            var allowedComp = SComp<CMUChemSynthesizerComponent>(allowed);
            Assert.That(allowedComp.Program, Is.EqualTo(hard), allowedComp.Fault);

            const string uranium = "CMUSynthesizerTest-U";
            Generate(uranium, 1, ["RMCUranium", "CMUPhenol"], 0f, ("Toxic", 1));
            var alternative = $"CMUGeneratedUraniumAlternative{uranium}";
            Assert.That(Server.System<CMUReactionProductIndexSystem>().TryGetRecipes(uranium, out var recipes), Is.True);
            Assert.That(recipes!.Select(r => r.ID), Is.SupersetOf(new[] { uranium, alternative }));
            Assert.That(CMUChemSynthesizerSystem.SelectRecipe(recipes!, uranium).ID, Is.EqualTo(uranium));

            var programmed = SpawnSynthesizer(map);
            Insert(programmed, "program", Report(map, uranium, valid: true));
            Assert.That(SComp<CMUChemSynthesizerComponent>(programmed).Program, Is.EqualTo(uranium));
        });
    }

    // ARCH-07 (E5): a GenTier 3 sample is refused below its clearance (CreateReport's clearance part) and, with the
    // clearance, until the faction has completed it; the sample path itself never completes it or pays anything.
    [Test]
    public async Task SampleRefusesUnresearchedGenTier3()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            const string hard = "CMUSynthesizerTest-SampleTier3";
            Generate(hard, 3, ["CMUPhenol", "CMUFormaldehyde"], 0f, ("Toxic", 1));

            var research = Server.System<ServerResearchDataTerminalSystem>();
            var credits = research.GetCredits("corporate");
            var sampleData = research.GetSampleData("corporate");

            var synth = SpawnSynthesizer(map);
            var vial = Vial(map, hard, 30);
            InsertAsPlayer(map, synth, "sample", vial);
            var system = Server.System<CMUChemSynthesizerSystem>();
            var comp = SComp<CMUChemSynthesizerComponent>(synth);

            // no research terminal on the test map: the corporate faction, clearance 1
            Assert.That(research.GetClearance("corporate"), Is.LessThan(3));
            Assert.That(system.TryProgramFromSample(synth), Is.False);
            Assert.That(comp.Fault, Is.EqualTo(CMUChemSynthesizerSystem.FaultSampleClearance));

            research.Clearance = 3;
            Assert.That(system.TryProgramFromSample(synth), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Program, Is.Null);
                Assert.That(comp.Fault, Is.EqualTo(CMUChemSynthesizerSystem.FaultUnresearched));
                Assert.That(research.GetResearch("corporate").CompletedChemicals, Does.Not.Contain(hard),
                    "programming from a sample must not complete the chemical");
                Assert.That(research.GetCredits("corporate"), Is.EqualTo(credits));
                Assert.That(research.GetSampleData("corporate"), Is.EqualTo(sampleData));
            });

            research.GetResearch("corporate").CompletedChemicals.Add(hard);
            Assert.That(system.TryProgramFromSample(synth), Is.True, comp.Fault);
            Assert.That(comp.Program, Is.EqualTo(hard));
            Assert.That(Port(synth, "product_out").Kind, Is.EqualTo(hard));
            Assert.That(Server.System<ItemSlotsSystem>().GetItemOrNull(synth, "sample"), Is.EqualTo(vial));
        });
    }

    // Hyperthermic synthesis heats to its 400 K clamp, which is the guarded limit of a Fueling 6 product (410 K
    // ignition); the finished product must not be vented for that.
    [Test]
    public async Task HyperthermicIgnitableProductIsNotVented()
    {
        var map = await Pair.CreateTestMap();
        EntityUid synth = default;
        EntityUid output = default;
        EntityUid waste = default;
        const string fuel = "CMUSynthesizerTest-HotFuel";

        await Server.WaitAssertion(() =>
        {
            Generate(fuel, 2, Ingredients, 330f, ("Fueling", 6), ("Hyperthermic", 6));

            synth = SpawnSynthesizer(map);
            for (var i = 0; i < Ingredients.Length; i++)
            {
                Insert(synth, $"input{i + 1}", Beaker(map, (Ingredients[i], 4)));
            }

            output = Beaker(map);
            waste = Beaker(map);
            Insert(synth, "output", output);
            Insert(synth, "waste", waste);

            var system = Server.System<CMUChemSynthesizerSystem>();
            Assert.That(system.SetProgram(synth, fuel), Is.True);
            system.SetBatches(synth, 4);
            SComp<CMUChemSynthesizerComponent>(synth).EnergyPerSecond = 3000f;
            Assert.That(system.TryStart(synth), Is.True);
        });

        await RunSeconds(6);

        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUChemSynthesizerComponent>(synth);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Idle), comp.Fault);
                Assert.That(Quantity(output, fuel), Is.EqualTo(FixedPoint2.New(4)));
                Assert.That(Quantity(output, SharedReagentGeneratorSystem.IgnitionResidueReagent), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(BeakerSolution(waste).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero), "the product was vented");
            });
        });
    }
}

/// <summary>
/// M4 (ARCH-04, FLOW-14): the chain planner, on test reactions A+B→I1, I1+C→I2, I2+B→T (priority 0, no temperature
/// gate) with a priority −1 decoy for I1 named after it, and a cycle X+Z→Y, Y+W→X (a second co-reagent, so the
/// reverse reaction is not also a side reaction and the cycle guard is what refuses it). Also the M3 report/sample order.
/// </summary>
[TestFixture]
public sealed class CMUChemSynthesizerChainTest : CMUChemSynthesizerTestBase
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private const string A = "CMUSynthChainTest-A";
    private const string B = "CMUSynthChainTest-B";
    private const string C = "CMUSynthChainTest-C";
    private const string I1 = "CMUSynthChainTest-I1";
    private const string I2 = "CMUSynthChainTest-I2";
    private const string T = "CMUSynthChainTest-T";
    private const string Decoy = "CMUSynthChainTest-Decoy";
    private const string X = "CMUSynthChainTest-X";
    private const string Y = "CMUSynthChainTest-Y";
    private const string Z = "CMUSynthChainTest-Z";
    private const string W = "CMUSynthChainTest-W";
    private const string D = "CMUSynthChainTest-D";
    private const string U = "CMUSynthChainTest-U";
    private const string T2 = "CMUSynthChainTest-T2";
    private const string E = "CMUSynthChainTest-E";
    private const string F = "CMUSynthChainTest-F";
    private const string G = "CMUSynthChainTest-G";
    private const string H = "CMUSynthChainTest-H";
    private const string I3 = "CMUSynthChainTest-I3";
    private const string I4 = "CMUSynthChainTest-I4";
    private const string V = "CMUSynthChainTest-V";
    private const string P = "CMUSynthChainTest-P";
    private const string Q = "CMUSynthChainTest-Q";
    private const string R = "CMUSynthChainTest-R";
    private const string S = "CMUSynthChainTest-S";
    private const string K = "CMUSynthChainTest-K";
    private const string L = "CMUSynthChainTest-L";

    private const string MakeI1 = "CMUSynthChainTest-MakeI1";
    private const string MakeI2 = "CMUSynthChainTest-MakeI2";
    private const string MakeT = "CMUSynthChainTest-MakeT";
    private const string MakeX = "CMUSynthChainTest-MakeX";
    private const string MakeY = "CMUSynthChainTest-MakeY";
    private const string MakeU = "CMUSynthChainTest-MakeU";
    private const string MakeT2 = "CMUSynthChainTest-MakeT2";
    private const string MakeI3 = "CMUSynthChainTest-MakeI3";
    private const string MakeI4 = "CMUSynthChainTest-MakeI4";
    private const string MakeV = "CMUSynthChainTest-MakeV";
    private const string MakeR = "CMUSynthChainTest-MakeR";
    private const string MakeP1 = "CMUSynthChainTest-MakeP1";
    private const string MakeP2 = "CMUSynthChainTest-MakeP2";

    /// <summary>Big per-batch yield: two parked intermediates of this overflow the 120u buffer.</summary>
    private const int BigYield = 70;

    private static string ReagentYaml(string id) => $"""
        - type: reagent
          id: {id}
          name: reagent-name-nothing
          desc: reagent-desc-nothing
          physicalDesc: reagent-physical-desc-nothing
          metabolisms:
            Bloodstream:
              metabolismRate: 0.1
              effects:
              - !type:Repairing
                potency: 1

        """;

    private static string ReactionYaml(string id, int priority, string first, string second, string product,
        int made = 1, string mixer = "") => $"""
        - type: reaction
          id: {id}
          priority: {priority}
          {(mixer == "" ? "" : $"requiredMixerCategories: [ {mixer} ]")}
          reactants:
            {first}:
              amount: 1
            {second}:
              amount: 1
          products:
            {product}: {made}

        """;

    [TestPrototypes]
    private static readonly string Prototypes =
        string.Concat(new[] { A, B, C, I1, I2, T, Decoy, X, Y, Z, W, D, U, T2, E, F, G, H, I3, I4, V, P, Q, R, S, K, L }
            .Select(ReagentYaml)) +
        ReactionYaml(MakeI1, 0, A, B, I1) +
        // decoy: named after the reagent (the single-step choice) but a procgen-style priority; the planner must
        // prefer MakeI1 even when Decoy is available
        ReactionYaml(I1, -1, Decoy, A, I1) +
        ReactionYaml(MakeI2, 0, I1, C, I2) +
        ReactionYaml(MakeT, 0, I2, B, T) +
        ReactionYaml(MakeY, 0, X, Z, Y) +
        ReactionYaml(MakeX, 0, Y, W, X) +
        // a fourth step on top of the chain: too deep
        ReactionYaml(MakeU, 0, T, D, U) +
        // a target the synthesizer's mixer cannot run
        ReactionYaml(MakeT2, 0, I2, B, T2, mixer: "DummyGrind") +
        // two big-yield intermediates that cannot both wait in the buffer
        ReactionYaml(MakeI3, 0, E, F, I3, BigYield) +
        ReactionYaml(MakeI4, 0, G, H, I4, BigYield) +
        ReactionYaml(MakeV, 0, I3, I4, V) +
        // P's first recipe fails deep (K is never supplied), its second succeeds; Q is the real, shallower block
        ReactionYaml(MakeR, 0, P, Q, R) +
        ReactionYaml(MakeP1, 0, S, K, P) +
        ReactionYaml(MakeP2, -1, S, L, P);

    private Entity<SolutionComponent> Buffer(EntityUid synth)
    {
        Assert.That(Server.System<SharedSolutionContainerSystem>().TryGetSolution(synth, "buffer", out var soln, out _), Is.True);
        return soln!.Value;
    }

    private (EntityUid Synth, EntityUid A, EntityUid B, EntityUid C, EntityUid Output, EntityUid Waste) ChainRig(
        Robust.UnitTesting.Pool.TestMapData map, int c, bool unlocked, int b = 10)
    {
        var synth = SpawnSynthesizer(map);
        var inA = Beaker(map, (A, 5));
        var inB = Beaker(map, (B, b));
        var inC = c > 0 ? Beaker(map, (C, c)) : Beaker(map);
        var output = Beaker(map);
        var waste = Beaker(map);
        Insert(synth, "input1", inA);
        Insert(synth, "input2", inB);
        if (c > 0)
            Insert(synth, "input3", inC);
        Insert(synth, "output", output);
        Insert(synth, "waste", waste);

        var system = Server.System<CMUChemSynthesizerSystem>();
        var comp = SComp<CMUChemSynthesizerComponent>(synth);
        comp.ChainPlannerUnlocked = unlocked;
        comp.EnergyPerSecond = 3000f;
        system.SetChainEnabled(synth, true);
        Assert.That(system.SetProgram(synth, MakeT), Is.True);
        Assert.That(system.SetBatches(synth, 5), Is.True);
        return (synth, inA, inB, inC, output, waste);
    }

    [Test]
    public async Task ThreeStepChainCompletes()
    {
        var map = await Pair.CreateTestMap();
        (EntityUid Synth, EntityUid A, EntityUid B, EntityUid C, EntityUid Output, EntityUid Waste) rig = default;

        await Server.WaitAssertion(() =>
        {
            rig = ChainRig(map, 5, unlocked: true);
            // the decoy is genuinely usable (Decoy in the tank, which never reacts; A in a beaker)
            Assert.That(Server.System<FluidSystem>().Add(rig.Synth, Decoy, 5f), Is.EqualTo(5f).Within(0.0001f));
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            Assert.That(Server.System<CMUChemSynthesizerSystem>().TryStart(rig.Synth), Is.True, comp.Fault);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Fault, Is.Null);
                Assert.That(comp.Plan.Select(s => s.Reaction), Is.EqualTo(new[] { MakeI1, MakeI2, MakeT }),
                    "post-order steps, the priority-0 recipe before the usable decoy named after I1");
                Assert.That(comp.Plan.Select(s => s.Batches), Is.All.EqualTo(5));
                Assert.That(comp.PlanIndex, Is.Zero);
                Assert.That(Quantity(rig.A, A), Is.EqualTo(FixedPoint2.New(5)), "planning must not pull anything");
                Assert.That(Chamber(rig.Synth).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
            });
        });

        // step 1 meters at 1 s; heat its chamber so its intermediate would be parked hot without FLOW-14
        await RunSeconds(1.5f);

        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Conditioning));
            Assert.That(comp.PlanIndex, Is.Zero);
            Server.System<SharedSolutionContainerSystem>().SetTemperature(Chamber(rig.Synth), 350f);
        });

        // step 1 reacts and parks I1 at 2 s; step 2 meters at 3 s
        await RunSeconds(1f);

        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            var buffer = Buffer(rig.Synth).Comp.Solution;
            Assert.Multiple(() =>
            {
                Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Metering), comp.Fault);
                Assert.That(comp.PlanIndex, Is.EqualTo(1));
                Assert.That(buffer.GetTotalPrototypeQuantity(I1), Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(buffer.Temperature, Is.EqualTo(CMUChemSynthesizerSystem.RoomTemperature).Within(0.01f),
                    "FLOW-14: a parked intermediate is reset to room temperature");
                Assert.That(Chamber(rig.Synth).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
            });
        });

        await RunSeconds(6);

        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Idle), comp.Fault);
                Assert.That(comp.Fault, Is.Null);
                Assert.That(Quantity(rig.Output, T), Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(BeakerSolution(rig.Output).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(Quantity(rig.Waste, I1), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(Quantity(rig.Waste, I2), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(BeakerSolution(rig.Waste).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero), "exact amounts leave no waste");
                Assert.That(Buffer(rig.Synth).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero), "the buffer ends empty");
                Assert.That(Chamber(rig.Synth).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(Quantity(rig.A, A), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(Quantity(rig.B, B), Is.EqualTo(FixedPoint2.Zero), "B is shared by steps 1 and 3");
                Assert.That(Quantity(rig.C, C), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(Server.System<FluidSystem>().GetAmount(rig.Synth, Decoy), Is.EqualTo(5f).Within(0.0001f),
                    "the decoy recipe was not used");
                Assert.That(comp.Plan, Is.Empty);
            });
        });
    }

    [Test]
    public async Task MissingInputBlocksPlan()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var rig = ChainRig(map, 0, unlocked: true);
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            Assert.That(Server.System<CMUChemSynthesizerSystem>().TryStart(rig.Synth), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Fault));
                Assert.That(comp.Fault, Is.EqualTo(CMUChemSynthesizerSystem.FaultPlanMissingInput));
                Assert.That(comp.PlanBlockReagent, Is.EqualTo(C));
                Assert.That(comp.Plan, Is.Empty);
                Assert.That(Quantity(rig.A, A), Is.EqualTo(FixedPoint2.New(5)), "a refused plan pulls nothing");
                Assert.That(Quantity(rig.B, B), Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(Chamber(rig.Synth).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(Buffer(rig.Synth).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
            });
        });
    }

    // The ledger: steps 1 and 3 each need 5 B, so 5 B is short even though either step alone would fit.
    [Test]
    public async Task LedgerCountsSharedInputOnce()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var rig = ChainRig(map, 5, unlocked: true, b: 5);
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            Assert.That(Server.System<CMUChemSynthesizerSystem>().TryStart(rig.Synth), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Fault, Is.EqualTo(CMUChemSynthesizerSystem.FaultPlanMissingInput));
                Assert.That(comp.PlanBlockReagent, Is.EqualTo(B));
                Assert.That(comp.Plan, Is.Empty);
                Assert.That(Quantity(rig.A, A), Is.EqualTo(FixedPoint2.New(5)), "a refused plan pulls nothing");
                Assert.That(Quantity(rig.B, B), Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(Quantity(rig.C, C), Is.EqualTo(FixedPoint2.New(5)));
            });
        });
    }

    // U ← T ← I2 ← I1 is four steps on one branch.
    [Test]
    public async Task FourStepChainIsTooDeep()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var rig = ChainRig(map, 5, unlocked: true);
            Server.System<FluidSystem>().Add(rig.Synth, D, 5f);
            var system = Server.System<CMUChemSynthesizerSystem>();
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            Assert.That(system.SetProgram(rig.Synth, MakeU), Is.True);
            Assert.That(system.TryStart(rig.Synth), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Fault, Is.EqualTo(CMUChemSynthesizerSystem.FaultPlanTooDeep));
                Assert.That(comp.PlanBlockReagent, Is.EqualTo(I1));
                Assert.That(comp.Plan, Is.Empty);
                Assert.That(Quantity(rig.A, A), Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(Server.System<FluidSystem>().GetAmount(rig.Synth, D), Is.EqualTo(5f).Within(0.0001f));
            });
        });
    }

    // The final step gets the single-step gates before planning: a target the mixer cannot run is refused before any
    // intermediate step uses the inputs up.
    [Test]
    public async Task TargetCheckedBeforePlanning()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var rig = ChainRig(map, 5, unlocked: true);
            var system = Server.System<CMUChemSynthesizerSystem>();
            var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);
            Assert.That(system.SetProgram(rig.Synth, MakeT2), Is.True);
            Assert.That(system.TryStart(rig.Synth), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Fault, Is.EqualTo(CMUChemSynthesizerSystem.FaultUnsupportedMixing));
                Assert.That(comp.Plan, Is.Empty);
                Assert.That(Quantity(rig.A, A), Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(Quantity(rig.B, B), Is.EqualTo(FixedPoint2.New(10)));
            });
        });
    }

    // Two parked 70u intermediates would overflow the 120u buffer at step 2; the plan is refused before anything moves.
    [Test]
    public async Task BufferOverflowRefusedUpFront()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var synth = SpawnSynthesizer(map);
            Insert(synth, "output", Beaker(map));
            Insert(synth, "waste", Beaker(map));
            var fluids = Server.System<FluidSystem>();
            foreach (var reagent in new[] { E, F, G, H })
            {
                fluids.Add(synth, reagent, 1f);
            }

            var system = Server.System<CMUChemSynthesizerSystem>();
            var comp = SComp<CMUChemSynthesizerComponent>(synth);
            comp.ChainPlannerUnlocked = true;
            system.SetChainEnabled(synth, true);
            Assert.That(system.SetProgram(synth, MakeV), Is.True);
            Assert.That(system.TryStart(synth), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Fault, Is.EqualTo(CMUChemSynthesizerSystem.FaultPlanBufferFull));
                Assert.That(comp.PlanBlockReagent, Is.EqualTo(I4));
                Assert.That(comp.Plan, Is.Empty);
                Assert.That(Buffer(synth).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero));
                foreach (var reagent in new[] { E, F, G, H })
                {
                    Assert.That(fluids.GetAmount(synth, reagent), Is.EqualTo(1f).Within(0.0001f), reagent);
                }
            });
        });
    }

    // The buffer never outlives its plan: a final Output and a new program both empty it to waste.
    [Test]
    public async Task BufferNeverOutlivesItsProgram()
    {
        var map = await Pair.CreateTestMap();
        EntityUid synth = default;
        EntityUid output = default;
        EntityUid waste = default;

        await Server.WaitAssertion(() =>
        {
            synth = SpawnSynthesizer(map);
            output = Beaker(map);
            waste = Beaker(map);
            Insert(synth, "input1", Beaker(map, (A, 5)));
            Insert(synth, "input2", Beaker(map, (B, 5)));
            Insert(synth, "output", output);
            Insert(synth, "waste", waste);

            var system = Server.System<CMUChemSynthesizerSystem>();
            Assert.That(system.SetProgram(synth, MakeI1), Is.True);
            Assert.That(system.SetBatches(synth, 5), Is.True);
            // a leftover intermediate, as after a faulted chain that was restarted as a single step
            AddTo(Buffer(synth), C, 3);
            Assert.That(system.TryStart(synth), Is.True, SComp<CMUChemSynthesizerComponent>(synth).Fault);
        });

        await RunSeconds(4);

        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUChemSynthesizerComponent>(synth);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Idle), comp.Fault);
                Assert.That(Quantity(output, I1), Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(Buffer(synth).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero), "a final Output empties the buffer");
                Assert.That(Quantity(waste, C), Is.EqualTo(FixedPoint2.New(3)));
            });

            AddTo(Buffer(synth), C, 2);
            Assert.That(Server.System<CMUChemSynthesizerSystem>().SetProgram(synth, MakeI2), Is.True);
            Assert.That(Buffer(synth).Comp.Solution.Volume, Is.EqualTo(FixedPoint2.Zero), "a new program empties the buffer");
            Assert.That(Quantity(waste, C), Is.EqualTo(FixedPoint2.New(5)));
        });
    }

    // A failure inside a recipe the planner abandoned (P via S+K, K missing, depth 3) must not mask the real block
    // (Q, depth 2) once a sibling recipe (P via S+L) succeeded.
    [Test]
    public async Task AbandonedRecipeFailureDoesNotMaskRealBlock()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var synth = SpawnSynthesizer(map);
            var fluids = Server.System<FluidSystem>();
            fluids.Add(synth, S, 1f);
            fluids.Add(synth, L, 1f);
            var system = Server.System<CMUChemSynthesizerSystem>();
            var comp = SComp<CMUChemSynthesizerComponent>(synth);
            comp.ChainPlannerUnlocked = true;
            system.SetChainEnabled(synth, true);
            Assert.That(system.SetProgram(synth, MakeR), Is.True);
            Assert.That(system.TryStart(synth), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Fault, Is.EqualTo(CMUChemSynthesizerSystem.FaultPlanMissingInput));
                Assert.That(comp.PlanBlockReagent, Is.EqualTo(Q), "not K, from the abandoned recipe");
                Assert.That(fluids.GetAmount(synth, S), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(fluids.GetAmount(synth, L), Is.EqualTo(1f).Within(0.0001f));
            });
        });
    }

    [Test]
    public async Task CycleRefusedWithoutHanging()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var synth = SpawnSynthesizer(map);
            var inZ = Beaker(map, (Z, 10));
            Insert(synth, "input1", inZ);
            Insert(synth, "input2", Beaker(map, (W, 10)));
            var system = Server.System<CMUChemSynthesizerSystem>();
            var comp = SComp<CMUChemSynthesizerComponent>(synth);
            comp.ChainPlannerUnlocked = true;
            system.SetChainEnabled(synth, true);
            Assert.That(system.SetProgram(synth, MakeY), Is.True);

            // Y needs X, X is only made from Y
            Assert.That(system.TryStart(synth), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Stage, Is.EqualTo(CMUChemSynthesizerStage.Fault));
                Assert.That(comp.Fault, Is.EqualTo(CMUChemSynthesizerSystem.FaultPlanCycle));
                Assert.That(comp.PlanBlockReagent, Is.EqualTo(Y));
                Assert.That(comp.Plan, Is.Empty);
                Assert.That(Quantity(inZ, Z), Is.EqualTo(FixedPoint2.New(10)));
            });
        });
    }

    [Test]
    public async Task LockedPlannerRefused()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var research = Server.System<ServerResearchDataTerminalSystem>();
            var clearance = research.Clearance;
            try
            {
                var rig = ChainRig(map, 5, unlocked: false);
                var system = Server.System<CMUChemSynthesizerSystem>();
                var comp = SComp<CMUChemSynthesizerComponent>(rig.Synth);

                // no research terminal on the test map: the corporate faction
                research.Clearance = 1;
                Assert.That(research.GetClearance(research.GetFaction(rig.Synth)), Is.LessThan(comp.ChainPlannerClearance));
                Assert.That(system.TryStart(rig.Synth), Is.False);
                Assert.Multiple(() =>
                {
                    Assert.That(comp.Fault, Is.EqualTo(CMUChemSynthesizerSystem.FaultChainLocked));
                    Assert.That(comp.Plan, Is.Empty);
                    Assert.That(Quantity(rig.A, A), Is.EqualTo(FixedPoint2.New(5)));
                });

                // E6: clearance 3 unlocks it
                research.Clearance = comp.ChainPlannerClearance;
                Assert.That(system.TryStart(rig.Synth), Is.True, comp.Fault);
                Assert.That(comp.Plan, Has.Count.EqualTo(3));
                system.Stop(rig.Synth);
                Assert.That(comp.Plan, Is.Empty);
            }
            finally
            {
                research.Clearance = clearance;
            }
        });
    }

    // M3: a report reprograms only when it was inserted after the current program; a sample program is not
    // overridden by a report left in its slot.
    [Test]
    public async Task ReportOnlyOverridesWhenNewer()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            // unpowered, so a started machine stays in Metering
            var synth = SSpawnAtPosition(Synthesizer, map.GridCoords);
            var system = Server.System<CMUChemSynthesizerSystem>();
            var comp = SComp<CMUChemSynthesizerComponent>(synth);

            var oldReport = Report(map, T, valid: true);
            Insert(synth, "program", oldReport);
            Assert.That(comp.Program, Is.EqualTo(MakeT), comp.Fault);

            InsertAsPlayer(map, synth, "sample", Vial(map, I2, 30));
            Assert.That(system.TryProgramFromSample(synth), Is.True, comp.Fault);
            Assert.That(comp.Program, Is.EqualTo(MakeI2));

            Assert.That(system.TryStart(synth), Is.True, comp.Fault);
            Assert.That(comp.Program, Is.EqualTo(MakeI2), "the older report must not override the sample program");
            system.Stop(synth);

            // a report inserted mid-cycle is read by the next start
            Assert.That(system.TryStart(synth), Is.True, comp.Fault);
            SEntMan.DeleteEntity(oldReport);
            Insert(synth, "program", Report(map, T, valid: true));
            Assert.That(comp.Program, Is.EqualTo(MakeI2), "a running machine is not reprogrammed");
            system.Stop(synth);

            Assert.That(system.TryStart(synth), Is.True, comp.Fault);
            Assert.That(comp.Program, Is.EqualTo(MakeT), "the newer report wins");
            system.Stop(synth);
        });
    }
}
