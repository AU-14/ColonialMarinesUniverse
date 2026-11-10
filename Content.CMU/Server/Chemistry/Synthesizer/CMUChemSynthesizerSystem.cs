using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.CMU14.Chemistry.Reagents;
using Content.Server.CMU14.Chemistry.Research;
using Content.Server.CMU14.Chemistry.Temperature;
using Content.Server.CMU14.Fluids;
using Content.Server.Power.EntitySystems;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.CMU14.Chemistry.Research;
using Content.Shared.CMU14.Chemistry.Synthesizer;
using Content.Shared.CMU14.Chemistry.Temperature;
using Content.Shared.CMU14.Fluids;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Chemistry.Synthesizer;

/// <summary>
/// N10 (GraphAudit-ChemBotanyPlan, variant B): runs <see cref="CMUChemSynthesizerComponent"/>.
/// <para>
/// One cycle, on a <see cref="CMUChemSynthesizerComponent.StepInterval"/> accumulator that only advances while
/// powered: <b>Metering</b> pulls exactly <c>amount × batches</c> of each reactant (catalysts once) out of the input
/// beakers into the chamber, or nothing at all if anything is short. <b>Conditioning</b> drives the chamber towards the
/// reaction's band with the thermostat helper. <b>Reacting</b> reacts the chamber once with the machine's own mixer.
/// <b>Output</b> sends the product to the output beaker (waiting while it is full) and everything else to the waste
/// beaker, spilling what does not fit.
/// </para>
/// <para>
/// The chamber solution has <c>canReact: false</c>, so adding reagents and heating it never reacts it; reactions only
/// run in the single <see cref="SharedSolutionContainerSystem.UpdateChemicals"/> call of the Reacting stage, at a
/// temperature the machine picked. Off-recipe reactions are guarded by a documented heuristic: any other reaction
/// whose reactants are all among the chamber's reagents (plus the product) and that is eligible at the reacting
/// temperature with this mixer faults the run as a side reaction.
/// </para>
/// <para>
/// Ignition guard (FLOW-12): the band is capped under the ignition point of every generated chemical involved, and the
/// chamber is vented to waste and faulted if it reaches <c>ignition − IgnitionGuardMargin</c> before reacting, or the
/// ignition point itself after reacting (a Hyperthermic synthesis may legitimately heat up to its 400 K clamp).
/// </para>
/// <para>
/// M3 (GraphAudit-DeferredFollowups): reactants are metered from the input beakers and then the fluid tank (fed by the
/// <c>reagent_in_*</c> ports), truncated to <see cref="FixedPoint2"/> and added at room temperature. The product port's
/// kind is the programmed product (FLOW-13); while it is linked the product goes into the tank, whose room for it is
/// reserved for the whole cycle. Inserters (a null user) are gated by slot. A vial in the sample slot can program the
/// machine without a report and without any research reward (ARCH-07). The reservation applies only while the product
/// port is linked; batches cannot change mid-cycle; a report reprograms only when inserted after the current program.
/// </para>
/// <para>
/// M4 (ARCH-04, FLOW-14): with the chain toggle on and the program's reactants missing, <see cref="TryStart"/> plans a
/// chain (unlocked by clearance or <see cref="CMUChemSynthesizerComponent.ChainPlannerUnlocked"/>). Each non-final step
/// parks its product in the <c>buffer</c> solution at room temperature; the meter reads the buffer first; the final step
/// outputs normally. The target step is validated before planning, and a plan whose intermediates would overflow the
/// buffer is refused up front. The buffer never outlives its plan: a new program, the end of any final Output, Stop,
/// dumping faults, termination and reloads (running or not) empty or spill it.
/// </para>
/// <para>
/// CMU14 FLOW-02: a right-click Start/Stop verb, and continuous mode. Each run's batch count is picked once at its start
/// (<see cref="CMUChemSynthesizerComponent.RunBatches"/>): <c>Batches</c> as before, or, when continuous, the most
/// (up to <c>Batches</c>) the inputs and the chamber allow. A continuous machine starts the next run after a finished
/// Output; when no batch is possible it waits (idle, no fault) and retries every
/// <see cref="CMUChemSynthesizerComponent.WaitCheckInterval"/> of powered time. Real faults and Stop end the waiting.
/// </para>
/// </summary>
public sealed class CMUChemSynthesizerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly RMCReagentSystem _reagent = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedPuddleSystem _puddle = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPowerStateSystem _powerState = default!;
    [Dependency] private readonly CMUThermostatPlateSystem _thermostat = default!;
    [Dependency] private readonly CMUReactionProductIndexSystem _index = default!;
    [Dependency] private readonly ServerReagentGeneratorSystem _generator = default!;
    [Dependency] private readonly ServerResearchDataTerminalSystem _research = default!;
    [Dependency] private readonly FluidSystem _fluids = default!;

    /// <summary>The machine pushing its own product into its tank: its reservation does not apply to that push.</summary>
    private EntityUid? _pushingProduct;

    public const float RoomTemperature = 293.15f;

    /// <summary>Target offset past a one-sided band's bound (minTemp only, or maxTemp only).</summary>
    public const float OneSidedBandOffset = 5f;

    /// <summary>The band's top stays this far under the vent limit, so reaching the band never vents.</summary>
    public const float VentBandGap = 1f;

    /// <summary>Report entity that hides data behind clearance (ServerXRFScannerSystem / CreateReport).</summary>
    public const string PartialReportPrototype = "CMUResearchReportPartial";

    public const string FaultNoProgram = "cmu-chem-synth-fault-no-program";
    public const string FaultNoRecipe = "cmu-chem-synth-fault-no-recipe";
    public const string FaultBadReport = "cmu-chem-synth-fault-bad-report";
    public const string FaultPartialReport = "cmu-chem-synth-fault-partial-report";
    public const string FaultHiddenIngredient = "cmu-chem-synth-fault-hidden-ingredient";
    public const string FaultNonReagent = "cmu-chem-synth-fault-non-reagent";
    public const string FaultUnresearched = "cmu-chem-synth-fault-unresearched";
    public const string FaultMissingInput = "cmu-chem-synth-fault-missing-input";
    public const string FaultTooLarge = "cmu-chem-synth-fault-too-large";
    public const string FaultSideReaction = "cmu-chem-synth-fault-side-reaction";
    public const string FaultNoBand = "cmu-chem-synth-fault-no-band";
    public const string FaultOverheat = "cmu-chem-synth-fault-overheat";
    public const string FaultReactionFailed = "cmu-chem-synth-fault-reaction-failed";
    public const string FaultOutputBlocked = "cmu-chem-synth-fault-output-blocked";
    public const string FaultNoChamber = "cmu-chem-synth-fault-no-chamber";
    public const string FaultUnsupportedMixing = "cmu-chem-synth-fault-unsupported-mixing";
    public const string FaultConditioningTimeout = "cmu-chem-synth-fault-conditioning-timeout";
    public const string FaultInvalidReport = "cmu-chem-synth-fault-invalid-report";
    public const string FaultNoSample = "cmu-chem-synth-fault-no-sample";
    public const string FaultSampleInvalid = "cmu-chem-synth-fault-sample-invalid";
    public const string FaultSampleInsufficient = "cmu-chem-synth-fault-sample-insufficient";
    public const string FaultSampleContaminated = "cmu-chem-synth-fault-sample-contaminated";
    public const string FaultSampleClearance = "cmu-chem-synth-fault-sample-clearance";
    public const string FaultResearchLocked = "cmu-chem-synth-fault-research-locked";
    public const string FaultPlanMissingInput = "cmu-chem-synth-fault-plan-missing-input";
    public const string FaultPlanTooDeep = "cmu-chem-synth-fault-plan-too-deep";
    public const string FaultPlanCycle = "cmu-chem-synth-fault-plan-cycle";
    public const string FaultPlanInvalid = "cmu-chem-synth-fault-plan-invalid";
    public const string FaultChainLocked = "cmu-chem-synth-fault-chain-locked";
    public const string FaultPlanBufferFull = "cmu-chem-synth-fault-plan-buffer-full";

    /// <summary>Within this much of a whole FixedPoint2 a tank amount counts as that amount (as <c>FluidSystem.TryUse</c>).</summary>
    public const float TankTolerance = 0.0001f;

    /// <summary>Clearance a Special-class chemical needs (<c>ServerReagentGeneratorSystem.CreateReport</c>).</summary>
    public const int SpecialClearance = 6;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUChemSynthesizerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUChemSynthesizerComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<CMUChemSynthesizerComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<CMUChemSynthesizerComponent, CMUChemSynthesizerStartMessage>(OnStartMessage);
        SubscribeLocalEvent<CMUChemSynthesizerComponent, CMUChemSynthesizerStopMessage>(OnStopMessage);
        SubscribeLocalEvent<CMUChemSynthesizerComponent, CMUChemSynthesizerSetBatchesMessage>(OnSetBatchesMessage);
        SubscribeLocalEvent<CMUChemSynthesizerComponent, CMUChemSynthesizerProgramFromSampleMessage>(OnProgramFromSampleMessage);
        SubscribeLocalEvent<CMUChemSynthesizerComponent, CMUChemSynthesizerSetChainMessage>(OnSetChainMessage);
        // CMU14 FLOW-02: the only directed subscriptions of these pairs (one per server)
        SubscribeLocalEvent<CMUChemSynthesizerComponent, CMUChemSynthesizerSetContinuousMessage>(OnSetContinuousMessage);
        SubscribeLocalEvent<CMUChemSynthesizerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<CMUChemSynthesizerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUChemSynthesizerComponent, CMUFluidTankRoomEvent>(OnTankRoom);
        SubscribeLocalEvent<CMUChemSynthesizerComponent, ItemSlotInsertAttemptEvent>(OnSlotInsertAttempt);
        SubscribeLocalEvent<CMUChemSynthesizerComponent, ItemSlotEjectAttemptEvent>(OnSlotEjectAttempt);
        // after the reagent cache is rebuilt, so a removed reagent no longer resolves
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded, after: new[] { typeof(RMCReagentSystem) });
    }

    #region Public API

    /// <summary>
    /// Programs the machine with a reaction directly, bypassing the research report checks (tests, admin tools).
    /// The product is <paramref name="productId"/> or, when null, the reaction's first product.
    /// </summary>
    public bool SetProgram(EntityUid uid, string reactionId, string? productId = null, CMUChemSynthesizerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (comp.Stage is not (CMUChemSynthesizerStage.Idle or CMUChemSynthesizerStage.Fault))
            return false;

        if (!_proto.TryIndex<ReactionPrototype>(reactionId, out var reaction) || reaction.Products.Count == 0)
            return false;

        if (productId == null || !reaction.Products.ContainsKey(productId))
            productId = reaction.Products.Keys.First().Id;

        comp.Program = reaction.ID;
        comp.ProgramProduct = productId;
        // the newest program wins: a report already in its slot no longer reprograms the next start
        comp.ReportPending = false;
        ClearPlan(comp);
        // intermediates belong to the plan of the old program; never meter them into a new one
        DumpBuffer((uid, comp));
        comp.Fault = null;
        comp.Stage = CMUChemSynthesizerStage.Idle;
        Dirty(uid, comp);
        SyncProductPort((uid, comp));
        return true;
    }

    /// <summary>
    /// Programs the machine from the vial in its sample slot (ARCH-07, E5): at least
    /// <see cref="CMUChemSynthesizerComponent.SampleMinVolume"/> of exactly one reagent (XRF scanner validity), the
    /// clearance part of <c>CreateReport</c>, then the recipe, hidden-ingredient and research gates of
    /// <see cref="TryProgramFromReagent"/>. Gives no reward (no completion, credits, properties or report) and leaves the
    /// sample in its slot.
    /// </summary>
    public bool TryProgramFromSample(EntityUid uid, CMUChemSynthesizerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (comp.Stage is not (CMUChemSynthesizerStage.Idle or CMUChemSynthesizerStage.Fault))
            return false;

        var ent = new Entity<CMUChemSynthesizerComponent>(uid, comp);
        if (_itemSlots.GetItemOrNull(uid, comp.SampleSlot) is not { } vial)
        {
            SetFault(ent, FaultNoSample);
            return false;
        }

        if (!_solutions.TryGetSolution(vial, comp.SampleSolution, out _, out var sample))
        {
            SetFault(ent, FaultSampleInvalid);
            return false;
        }

        if (sample.Volume < comp.SampleMinVolume)
        {
            SetFault(ent, FaultSampleInsufficient);
            return false;
        }

        if (sample.Contents.Count != 1)
        {
            SetFault(ent, FaultSampleContaminated);
            return false;
        }

        var reagentId = sample.Contents[0].Reagent.Prototype;
        if (!_reagent.TryIndex(reagentId, out var reagent))
        {
            SetFault(ent, FaultSampleInvalid);
            return false;
        }

        // FLOW-24 parity with the XRF scanner: a locked faction gets nothing from a sample.
        var faction = _research.GetFaction(uid);
        if (_research.IsResearchBlocked(faction))
        {
            SetFault(ent, FaultResearchLocked);
            return false;
        }

        if (!ClearanceAllows(reagent, _research.GetClearance(faction)))
        {
            SetFault(ent, FaultSampleClearance);
            return false;
        }

        return TryProgramFromReagent(ent, reagentId);
    }

    /// <summary>
    /// The clearance part of <c>ServerReagentGeneratorSystem.CreateReport</c>: an Ultra+ chemical needs clearance of at
    /// least its GenTier, a Special one <see cref="SpecialClearance"/>.
    /// </summary>
    public static bool ClearanceAllows(ReagentPrototype reagent, int clearance)
    {
        if (reagent.Class >= ReagentClass.Ultra)
            return clearance >= reagent.GenTier;

        if (reagent.Class == ReagentClass.Special)
            return clearance >= SpecialClearance;

        return true;
    }

    /// <summary>
    /// Tank room held back for the running cycle's products (Σ products × batches), so pipe inflows cannot fill the tank
    /// the product port pushes from. Zero while idle or faulted.
    /// </summary>
    public float GetReservedFluid(CMUChemSynthesizerComponent comp)
    {
        if (comp.Stage is CMUChemSynthesizerStage.Idle or CMUChemSynthesizerStage.Fault || !TryGetProgram(comp, out var reaction))
            return 0f;

        var total = FixedPoint2.Zero;
        var batches = GetRunBatches(comp);
        foreach (var amount in reaction.Products.Values)
        {
            total += amount * batches;
        }

        return total.Float();
    }

    /// <summary>The current run's batch count (FLOW-02); <see cref="CMUChemSynthesizerComponent.Batches"/> when unset.</summary>
    public static int GetRunBatches(CMUChemSynthesizerComponent comp)
    {
        return comp.RunBatches > 0 ? comp.RunBatches : comp.Batches;
    }

    /// <summary>
    /// Starts a cycle. A report inserted since the program was set (<see cref="CMUChemSynthesizerComponent.ReportPending"/>)
    /// reprograms the machine first. With the chain planner on and the program's reactants missing, plans a chain
    /// (M4); planning never moves anything. FLOW-02: in continuous mode the run's batch count is the most the inputs
    /// allow (<see cref="TryPickContinuousRun"/>), and inputs too short for even one batch make it wait instead of
    /// faulting (it returns false then, with <see cref="CMUChemSynthesizerComponent.Waiting"/> set).
    /// </summary>
    public bool TryStart(EntityUid uid, CMUChemSynthesizerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (comp.Stage is not (CMUChemSynthesizerStage.Idle or CMUChemSynthesizerStage.Fault))
            return false;

        var ent = new Entity<CMUChemSynthesizerComponent>(uid, comp);
        if (comp.ReportPending)
        {
            // a failed report keeps the flag, so the next start refuses it again
            if (_itemSlots.GetItemOrNull(uid, comp.ProgramSlot) is { } report && !TryProgramFromReport(ent, report))
                return false;

            comp.ReportPending = false;
        }

        if (comp.Program == null)
        {
            SetFault(ent, FaultNoProgram);
            return false;
        }

        // FLOW-02: a waiting machine keeps naming what it waits for until it starts or faults
        var waitingFor = comp.Waiting ? comp.PlanBlockReagent : null;
        ClearPlan(comp);
        comp.PlanBlockReagent = waitingFor;
        if (comp.Continuous)
        {
            if (!TryPickContinuousRun(ent))
                return false;
        }
        else
        {
            // unchanged single-shot behaviour: exactly Batches, all or nothing
            comp.RunBatches = comp.Batches;
            if (comp.ChainEnabled && !TryPlan(ent, comp.RunBatches))
                return false;
        }

        comp.Waiting = false;
        comp.PlanBlockReagent = null;
        comp.Fault = null;
        comp.Stage = CMUChemSynthesizerStage.Metering;
        comp.Accumulator = 0f;
        Dirty(ent);
        return true;
    }

    /// <summary>Aborts the cycle (or the waiting of continuous mode); whatever is in the chamber and the buffer goes to waste.</summary>
    public void Stop(EntityUid uid, CMUChemSynthesizerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        var ent = new Entity<CMUChemSynthesizerComponent>(uid, comp);
        if (TryGetChamber(ent, out var chamber))
            DumpChamber(ent, chamber.Value);

        DumpBuffer(ent);
        ClearPlan(comp);
        comp.Waiting = false;
        GoIdle(ent);
    }

    /// <summary>
    /// FLOW-02: turns continuous mode on or off; allowed in any stage. Turning it off while waiting leaves the machine
    /// idle; a running cycle finishes and then stays idle.
    /// </summary>
    public void SetContinuous(EntityUid uid, bool enabled, CMUChemSynthesizerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp) || comp.Continuous == enabled)
            return;

        comp.Continuous = enabled;
        if (!enabled && comp.Waiting)
        {
            comp.Waiting = false;
            comp.PlanBlockReagent = null;
            GoIdle((uid, comp));
            return;
        }

        Dirty(uid, comp);
    }

    /// <summary>Sets the batch count; refused while a cycle runs, since the tank reservation and the meter read it.</summary>
    public bool SetBatches(EntityUid uid, int batches, CMUChemSynthesizerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (comp.Stage is not (CMUChemSynthesizerStage.Idle or CMUChemSynthesizerStage.Fault))
            return false;

        comp.Batches = Math.Clamp(batches, 1, Math.Max(1, comp.MaxBatches));
        Dirty(uid, comp);
        return true;
    }

    /// <summary>Turns the chain planner toggle on or off; it applies from the next start.</summary>
    public void SetChainEnabled(EntityUid uid, bool enabled, CMUChemSynthesizerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp) || comp.ChainEnabled == enabled)
            return;

        comp.ChainEnabled = enabled;
        Dirty(uid, comp);
    }

    /// <summary>E6: the planner needs the override or the machine's faction at <see cref="CMUChemSynthesizerComponent.ChainPlannerClearance"/>.</summary>
    public bool IsChainPlannerUnlocked(Entity<CMUChemSynthesizerComponent> ent)
    {
        return ent.Comp.ChainPlannerUnlocked ||
               _research.GetClearance(_research.GetFaction(ent.Owner)) >= ent.Comp.ChainPlannerClearance;
    }

    #endregion

    #region Events

    // FluidPort.Scale is not saved and defaults to 1, so a spawned or loaded machine must have its product port set
    // before the fluid system steps it, or an unprogrammed port would pump the tank's largest reactant out (FLOW-13).
    private void OnStartup(Entity<CMUChemSynthesizerComponent> ent, ref ComponentStartup args)
    {
        SyncProductPort(ent, notify: false);
    }

    // A destroyed or deconstructed machine spills its chamber and buffer instead of deleting the reagents.
    private void OnTerminating(Entity<CMUChemSynthesizerComponent> ent, ref EntityTerminatingEvent args)
    {
        // nothing to spill onto when the whole grid or map is going away (round restart, test cleanup)
        var xform = Transform(ent.Owner);
        if (xform.MapUid is not { } map || TerminatingOrDeleted(map) ||
            (xform.GridUid is { } grid && TerminatingOrDeleted(grid)))
        {
            return;
        }

        if (TryGetChamber(ent, out var chamber))
            SpillAll(xform, chamber.Value.Comp.Solution);

        if (TryGetBuffer(ent, out var buffer))
            SpillAll(xform, buffer.Value.Comp.Solution);
    }

    private void SpillAll(TransformComponent xform, Solution solution)
    {
        if (solution.Volume <= FixedPoint2.Zero)
            return;

        var all = solution.SplitSolution(solution.Volume);
        _puddle.TrySpillAt(xform.Coordinates, all, out _);
    }

    private void OnInserted(Entity<CMUChemSynthesizerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ProgramSlot)
            return;

        // a report inserted mid-cycle reprograms the next start; a resting machine reads it now (and on success
        // SetProgram clears the flag again)
        ent.Comp.ReportPending = true;
        if (ent.Comp.Stage is not (CMUChemSynthesizerStage.Idle or CMUChemSynthesizerStage.Fault))
            return;

        TryProgramFromReport(ent, args.Entity);
    }

    private void OnStartMessage(Entity<CMUChemSynthesizerComponent> ent, ref CMUChemSynthesizerStartMessage args)
    {
        TryStart(ent.Owner, ent.Comp);
    }

    private void OnStopMessage(Entity<CMUChemSynthesizerComponent> ent, ref CMUChemSynthesizerStopMessage args)
    {
        Stop(ent.Owner, ent.Comp);
    }

    private void OnSetBatchesMessage(Entity<CMUChemSynthesizerComponent> ent, ref CMUChemSynthesizerSetBatchesMessage args)
    {
        SetBatches(ent.Owner, args.Batches, ent.Comp);
    }

    private void OnProgramFromSampleMessage(Entity<CMUChemSynthesizerComponent> ent, ref CMUChemSynthesizerProgramFromSampleMessage args)
    {
        TryProgramFromSample(ent.Owner, ent.Comp);
    }

    private void OnSetChainMessage(Entity<CMUChemSynthesizerComponent> ent, ref CMUChemSynthesizerSetChainMessage args)
    {
        SetChainEnabled(ent.Owner, args.Enabled, ent.Comp);
    }

    private void OnSetContinuousMessage(Entity<CMUChemSynthesizerComponent> ent, ref CMUChemSynthesizerSetContinuousMessage args)
    {
        SetContinuous(ent.Owner, args.Enabled, ent.Comp);
    }

    // FLOW-02: Start while resting (a waiting machine just tries again), Stop while running or waiting.
    private void OnGetAlternativeVerbs(Entity<CMUChemSynthesizerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var resting = ent.Comp.Stage is CMUChemSynthesizerStage.Idle or CMUChemSynthesizerStage.Fault;
        if (resting && this.IsPowered(ent.Owner, EntityManager))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cmu-chem-synth-verb-start"),
                Act = () => TryStart(ent.Owner, ent.Comp),
                // Below the item slots' eject verbs (priority 0), so alt-click still takes a beaker out, never starts a run.
                Priority = -1,
            });
        }

        if (!resting || ent.Comp.Waiting)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cmu-chem-synth-verb-stop"),
                Act = () => Stop(ent.Owner, ent.Comp),
                // Alt-click must never stop a run and dump the chamber.
                Priority = -2,
            });
        }
    }

    private void OnExamined(Entity<CMUChemSynthesizerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.Waiting)
            args.PushMarkup(Loc.GetString("cmu-chem-synth-examine-waiting"));
        else if (ent.Comp.Continuous)
            args.PushMarkup(Loc.GetString("cmu-chem-synth-examine-continuous"));
    }

    // The reservation keeps room for the port's product; with nothing linked the product goes to the beaker instead.
    private void OnTankRoom(Entity<CMUChemSynthesizerComponent> ent, ref CMUFluidTankRoomEvent args)
    {
        if (_pushingProduct == ent.Owner || !TryGetProductPort(ent, out var port) || port.Links.Count == 0)
            return;

        args.Available -= GetReservedFluid(ent.Comp);
    }

    // E4 inserter gates: a null user is automation (CMUInserterSystem); players are never gated here.
    private void OnSlotInsertAttempt(Entity<CMUChemSynthesizerComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled || args.User != null || args.Slot.ID is not { } slot)
            return;

        if (!AutomationMayInsert(ent.Comp, slot, args.Item))
            args.Cancelled = true;
    }

    private void OnSlotEjectAttempt(Entity<CMUChemSynthesizerComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Cancelled || args.User != null || args.Slot.ID is not { } slot)
            return;

        if (!AutomationMayEject(ent.Comp, slot, args.Item))
            args.Cancelled = true;
    }

    /// <summary>Inputs take only beakers with something in them, output and waste only empty ones, the sample nothing.</summary>
    private bool AutomationMayInsert(CMUChemSynthesizerComponent comp, string slot, EntityUid item)
    {
        if (slot == comp.SampleSlot)
            return false;

        if (comp.InputSlots.Contains(slot))
            return TryGetItemSolution(item, out var input) && input.Value.Comp.Solution.Volume > FixedPoint2.Zero;

        if (slot == comp.OutputSlot || slot == comp.WasteSlot)
            return TryGetItemSolution(item, out var empty) && empty.Value.Comp.Solution.Volume == FixedPoint2.Zero;

        return true;
    }

    /// <summary>
    /// Inputs leave once drained; output and waste once full, or while the machine rests (idle or faulted) and they hold
    /// something; the sample and the program never.
    /// </summary>
    private bool AutomationMayEject(CMUChemSynthesizerComponent comp, string slot, EntityUid item)
    {
        if (slot == comp.SampleSlot || slot == comp.ProgramSlot)
            return false;

        if (comp.InputSlots.Contains(slot))
            return !TryGetItemSolution(item, out var input) || input.Value.Comp.Solution.Volume <= FixedPoint2.Zero;

        if (slot == comp.OutputSlot || slot == comp.WasteSlot)
        {
            var resting = comp.Stage is CMUChemSynthesizerStage.Idle or CMUChemSynthesizerStage.Fault;
            if (!TryGetItemSolution(item, out var held))
                return resting;

            var solution = held.Value.Comp.Solution;
            return solution.AvailableVolume <= FixedPoint2.Zero || (resting && solution.Volume > FixedPoint2.Zero);
        }

        return true;
    }

    // STATE-02: a procgen reload can remove the programmed reaction. STATE-03: and the reagents left in the tank.
    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        var reactions = args.WasModified<ReactionPrototype>();
        var reagents = args.WasModified<ReagentPrototype>();
        if (!reactions && !reagents)
            return;

        var query = EntityQueryEnumerator<CMUChemSynthesizerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = new Entity<CMUChemSynthesizerComponent>(uid, comp);
            if (reagents)
            {
                PurgeUnknownFluids(ent);
                PurgeUnknownBuffer(ent);
            }

            if (comp.Program == null)
                continue;

            var running = comp.Stage is not (CMUChemSynthesizerStage.Idle or CMUChemSynthesizerStage.Fault);
            var productGone = comp.ProgramProduct != null && !_reagent.TryIndex(comp.ProgramProduct, out _);
            if (!productGone && _proto.HasIndex<ReactionPrototype>(comp.Program))
            {
                // M4: every planned step must still resolve, or the plan (and its intermediates) is dropped
                if (comp.Plan.Count == 0 || IsPlanValid(comp))
                    continue;

                ClearPlan(comp);
                DumpRun(ent);
                if (running)
                    SetFault(ent, FaultPlanInvalid);
                else
                    Dirty(ent);

                continue;
            }

            comp.Program = null;
            comp.ProgramProduct = null;
            ClearPlan(comp);
            // idle or faulted too: the buffer holds the old program's intermediates
            DumpRun(ent);

            SyncProductPort(ent);
            SetFault(ent, FaultNoProgram);
        }
    }

    /// <summary>Every step's reaction still exists, makes its product, and the product is still a reagent.</summary>
    private bool IsPlanValid(CMUChemSynthesizerComponent comp)
    {
        foreach (var step in comp.Plan)
        {
            if (!_proto.TryIndex<ReactionPrototype>(step.Reaction, out var reaction) ||
                !reaction.Products.ContainsKey(step.Product) ||
                !_reagent.TryIndex(step.Product, out _))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>STATE-03: drops tank entries whose reagent no longer resolves (an unloaded or reloaded procgen chemical).</summary>
    public void PurgeUnknownFluids(Entity<CMUChemSynthesizerComponent> ent)
    {
        if (!TryComp<FluidTankComponent>(ent, out var tank))
            return;

        List<string>? stale = null;
        foreach (var kind in tank.Contents.Keys)
        {
            if (!_reagent.TryIndex(kind, out _))
                (stale ??= new List<string>()).Add(kind);
        }

        if (stale == null)
            return;

        foreach (var kind in stale)
        {
            _fluids.Remove(ent.Owner, kind, tank.Get(kind));
        }
    }

    /// <summary>STATE-03 for the M4 buffer: drops reagents that no longer resolve (it is dumped to waste otherwise).</summary>
    public void PurgeUnknownBuffer(Entity<CMUChemSynthesizerComponent> ent)
    {
        if (!TryGetBuffer(ent, out var buffer))
            return;

        List<ReagentQuantity>? stale = null;
        foreach (var quantity in buffer.Value.Comp.Solution.Contents)
        {
            if (!_reagent.TryIndex(quantity.Reagent.Prototype, out _))
                (stale ??= new List<ReagentQuantity>()).Add(quantity);
        }

        if (stale == null)
            return;

        // straight off the solution: UpdateChemicals would look the dead reagent up
        var solution = buffer.Value.Comp.Solution;
        foreach (var quantity in stale)
        {
            solution.RemoveReagent(quantity);
        }

        Dirty(buffer.Value.Owner, buffer.Value.Comp);
    }

    #endregion

    #region Programming

    /// <summary>
    /// Variant B programming from a research report: refused for unreadable, partial and invalid reports, then
    /// <see cref="TryProgramFromReagent"/> with the report's chemical.
    /// </summary>
    private bool TryProgramFromReport(Entity<CMUChemSynthesizerComponent> ent, EntityUid report)
    {
        if (!TryComp<ResearchReportComponent>(report, out var reportComp) ||
            reportComp.Data is not { } data ||
            string.IsNullOrEmpty(data.ID) ||
            !_reagent.TryIndex(data.ID, out _))
        {
            SetFault(ent, FaultBadReport);
            return false;
        }

        if (MetaData(report).EntityPrototype?.ID == PartialReportPrototype)
        {
            SetFault(ent, FaultPartialReport);
            return false;
        }

        // CreateReport marks a report invalid when it is partial (clearance), when it could not analyze the recipe
        // (no reaction with the reagent's own id) or when the reagent has no properties. Reprints (CMUWYPaper) and
        // simulator reports (CMUResearchReportSynthesis) copy that flag, so it is checked as well as the prototype:
        // a report the game itself could not analyze does not program the machine.
        if (!reportComp.Valid)
        {
            SetFault(ent, FaultInvalidReport);
            return false;
        }

        return TryProgramFromReagent(ent, data.ID);
    }

    /// <summary>
    /// Programs the machine for <paramref name="reagentId"/>, made by <see cref="SelectRecipe"/>. Refused for chemicals
    /// with no recipe, recipes whose ingredients a report would hide, ingredients that are not reagents, and generated
    /// chemicals above <see cref="CMUChemSynthesizerComponent.MaxGenTierUnresearched"/> that the machine's faction has
    /// not completed. Shared by the report and sample paths; the caller does its own source checks.
    /// </summary>
    public bool TryProgramFromReagent(Entity<CMUChemSynthesizerComponent> ent, string reagentId)
    {
        if (!_reagent.TryIndex(reagentId, out var product) || !_index.TryGetRecipes(reagentId, out var recipes))
        {
            SetFault(ent, FaultNoRecipe);
            return false;
        }

        var reaction = SelectRecipe(recipes, reagentId);
        if (CheckRecipe(ent, reaction, product) is { } fault)
        {
            SetFault(ent, fault);
            return false;
        }

        return SetProgram(ent.Owner, reaction.ID, product.ID, ent.Comp);
    }

    /// <summary>
    /// The recipe gates shared by programming and the chain planner: every ingredient is a reagent, none is one a
    /// report would hide, and a generated product above <see cref="CMUChemSynthesizerComponent.MaxGenTierUnresearched"/>
    /// is completed by the machine's faction. Returns the fault, or null.
    /// </summary>
    private string? CheckRecipe(Entity<CMUChemSynthesizerComponent> ent, ReactionPrototype reaction, ReagentPrototype product)
    {
        // Mirrors ServerReagentGeneratorSystem.CreateReport: unidentified Special+ ingredients are hidden
        // (Hydro is exempt for reactants, not for catalysts).
        foreach (var (reactantId, info) in reaction.Reactants)
        {
            if (!_reagent.TryIndex(reactantId, out var reactant))
                return FaultNonReagent;

            var hidden = reactant.Class >= ReagentClass.Special &&
                         !_generator.IdentifiedChemicals.ContainsKey(reactant.ID) &&
                         (info.Catalyst || reactant.Class != ReagentClass.Hydro);
            if (hidden)
                return FaultHiddenIngredient;
        }

        if (product.GenTier > ent.Comp.MaxGenTierUnresearched)
        {
            var faction = _research.GetFaction(ent.Owner);
            if (!_research.GetResearch(faction).CompletedChemicals.Contains(product.ID))
                return FaultUnresearched;
        }

        return null;
    }

    /// <summary>
    /// The run-condition gates shared by the meter and the chain planner: a band under the vent limit of the reaction's
    /// reactants and products (an ignitable intermediate included), a mixer that supports it, and no side reaction at
    /// the band's target. Returns the fault, or null, and the target temperature.
    /// </summary>
    private string? CheckConditions(Entity<CMUChemSynthesizerComponent> ent, ReactionPrototype reaction, out float target)
    {
        var involved = new HashSet<string>();
        foreach (var reagent in reaction.Reactants.Keys)
        {
            involved.Add(reagent.Id);
        }

        foreach (var product in reaction.Products.Keys)
        {
            involved.Add(product.Id);
        }

        var ventLimit = GetVentLimit(involved);
        if (!TryGetBand(reaction, ventLimit, out _, out _, out target))
            return FaultNoBand;

        TryComp<ReactionMixerComponent>(ent.Owner, out var mixer);
        if (reaction.MixingCategories != null &&
            (mixer == null || reaction.MixingCategories.Except(mixer.ReactionTypes).Any()))
        {
            return FaultUnsupportedMixing;
        }

        if (FindSideReaction(reaction, involved, target, mixer) != null)
            return FaultSideReaction;

        return null;
    }

    /// <summary>What a step of <paramref name="batches"/> occupies in the chamber: its reactants and its products.</summary>
    private static FixedPoint2 StepVolume(ReactionPrototype reaction, int batches)
    {
        var total = FixedPoint2.Zero;
        foreach (var info in reaction.Reactants.Values)
        {
            total += info.Catalyst ? info.Amount : info.Amount * batches;
        }

        // products are added before reactants are known to be gone, so leave room for them too
        foreach (var amount in reaction.Products.Values)
        {
            total += amount * batches;
        }

        return total;
    }

    /// <summary>
    /// The recipe to program for <paramref name="reagentId"/>: the reaction whose id is the reagent id (procgen
    /// chemicals; this skips alternatives such as <c>CMUGeneratedUraniumAlternative{ID}</c>), else the first in
    /// engine order.
    /// </summary>
    public static ReactionPrototype SelectRecipe(IReadOnlyList<ReactionPrototype> recipes, string reagentId)
    {
        return recipes.FirstOrDefault(r => r.ID == reagentId) ?? recipes[0];
    }

    #endregion

    #region Chain planner

    /// <summary>
    /// Recipe order for the chain planner: hand-authored recipes (priority ≥ 0; procgen ones are
    /// <see cref="SharedReagentGeneratorSystem.GeneratedRecipePriority"/>) first, then the reaction named after the
    /// reagent, then engine order.
    /// </summary>
    public static List<ReactionPrototype> OrderPlanRecipes(IReadOnlyList<ReactionPrototype> recipes, string reagentId)
    {
        return recipes
            .OrderBy(r => r.Priority >= 0 ? 0 : 1)
            .ThenBy(r => r.ID == reagentId ? 0 : 1)
            .ToList();
    }

    /// <summary>State of one planning pass: what is still unclaimed, the steps so far, the branch, the deepest failure.</summary>
    private sealed class PlanContext(List<Entity<SolutionComponent>> inputs)
    {
        public readonly List<Entity<SolutionComponent>> Inputs = inputs;

        /// <summary>Unclaimed amount per reagent, seeded lazily from the inputs (buffer, beakers, tank).</summary>
        public readonly Dictionary<string, FixedPoint2> Ledger = new();

        public readonly List<CMUSynthStep> Steps = new();

        /// <summary>Reagents being made on the current branch (cycle guard).</summary>
        public readonly HashSet<string> Path = new();

        public int FailDepth = -1;
        public string? Fault;
        public string? FaultReagent;

        public (int Depth, string? Fault, string? Reagent) SaveFailure() => (FailDepth, Fault, FaultReagent);

        /// <summary>A claim that succeeded after all forgets the failures of the recipes it abandoned.</summary>
        public void RestoreFailure((int Depth, string? Fault, string? Reagent) saved)
        {
            (FailDepth, Fault, FaultReagent) = saved;
        }

        /// <summary>Keeps the deepest failure (the first one at that depth).</summary>
        public void Fail(int depth, string fault, string reagent)
        {
            if (depth <= FailDepth)
                return;

            FailDepth = depth;
            Fault = fault;
            FaultReagent = reagent;
        }
    }

    /// <summary>
    /// M4 (ARCH-04): plans the chain for the program. When the inputs already hold the program's reactants this is a
    /// single-step run and no plan is made; otherwise the planner must be unlocked (E6) and a depth-first search over
    /// <see cref="CMUReactionProductIndexSystem.TryGetRecipes"/> (at most <see cref="CMUChemSynthesizerComponent.MaxPlanDepth"/>
    /// steps on any branch, a path set against cycles, a ledger so siblings never count the same input twice) emits the
    /// steps in post-order. Faults with the deepest reason when no plan exists. Moves nothing.
    /// </summary>
    private bool TryPlan(Entity<CMUChemSynthesizerComponent> ent, int batches)
    {
        if (ComputePlan(ent, batches, out var steps, out var block) is { } fault)
        {
            ent.Comp.PlanBlockReagent = block;
            SetFault(ent, fault);
            return false;
        }

        StorePlan(ent.Comp, steps);
        return true;
    }

    /// <summary>
    /// The planning pass of <see cref="TryPlan"/> for a final step of <paramref name="batches"/>, without touching the
    /// machine: returns the fault (and the reagent it names) or null, with the steps when a chain is needed (null for a
    /// single-step run). FLOW-02 runs it for several batch counts.
    /// </summary>
    private string? ComputePlan(Entity<CMUChemSynthesizerComponent> ent, int batches,
        out List<CMUSynthStep>? steps, out string? blockReagent)
    {
        steps = null;
        blockReagent = null;
        var comp = ent.Comp;
        if (!TryGetProgram(comp, out var target))
            return null; // the meter faults an unresolvable program as before

        var product = GetProduct(comp, target);
        var ctx = new PlanContext(CollectInputs(ent));

        var covered = true;
        foreach (var (reagent, info) in target.Reactants)
        {
            var need = info.Catalyst ? info.Amount : info.Amount * batches;
            if (AvailableInput(ent, ctx.Inputs, reagent) >= need)
                continue;

            covered = false;
            break;
        }

        if (covered)
            return null;

        if (!IsChainPlannerUnlocked(ent))
            return FaultChainLocked;

        // the final step gets the same gates as every planned one, before any intermediate uses up the inputs
        var targetFault = CheckConditions(ent, target, out _);
        if (targetFault == null && StepVolume(target, batches) > ChamberCapacity(ent))
            targetFault = FaultTooLarge;

        if (targetFault != null)
            return targetFault;

        ctx.Path.Add(product.Id);
        if (!TryPlanStep(ent, ctx, target, product.Id, batches, 1))
        {
            blockReagent = ctx.FaultReagent;
            return ctx.Fault ?? FaultPlanMissingInput;
        }

        if (ctx.Steps.Count <= 1)
            return null;

        if (FindBufferOverflow(ent, ctx.Steps) is { } overflow)
        {
            blockReagent = overflow;
            return FaultPlanBufferFull;
        }

        steps = ctx.Steps;
        return null;
    }

    private static void StorePlan(CMUChemSynthesizerComponent comp, List<CMUSynthStep>? steps)
    {
        if (steps == null || steps.Count <= 1)
            return;

        comp.Plan.AddRange(steps);
        comp.PlanIndex = 0;
    }

    /// <summary>
    /// FLOW-02: faults that only mean "a reagent is not on hand and cannot be made from what is": in continuous mode
    /// these wait for inputs instead of faulting. Too deep and cycle count, since the deepest failure of a plan whose
    /// raw inputs ran out is often one of them, and adding the named reagent clears them.
    /// </summary>
    public static bool IsShortInputFault(string fault)
    {
        return fault is FaultMissingInput or FaultPlanMissingInput or FaultPlanTooDeep or FaultPlanCycle;
    }

    /// <summary>
    /// FLOW-02: picks a continuous run's batch count, the most in 1..<see cref="CMUChemSynthesizerComponent.Batches"/>
    /// that the chamber holds and the inputs (buffer, beakers, tank) cover, directly or, with the chain planner on,
    /// through a plan (largest found by bisection; feasibility grows with fewer batches). Sets
    /// <see cref="CMUChemSynthesizerComponent.RunBatches"/> and the plan and returns true; otherwise waits (short
    /// inputs) or faults (anything else) and returns false.
    /// </summary>
    private bool TryPickContinuousRun(Entity<CMUChemSynthesizerComponent> ent)
    {
        var comp = ent.Comp;
        var max = Math.Clamp(comp.Batches, 1, Math.Max(1, comp.MaxBatches));
        if (!TryGetProgram(comp, out var reaction))
        {
            // the meter faults an unresolvable program as before
            comp.RunBatches = max;
            return true;
        }

        var capacity = ChamberCapacity(ent);
        while (max > 0 && StepVolume(reaction, max) > capacity)
        {
            max--;
        }

        if (max == 0)
        {
            comp.PlanBlockReagent = null;
            SetFault(ent, FaultTooLarge);
            return false;
        }

        var direct = MaxDirectBatches(ent, reaction, max, out var shortReagent);
        if (!comp.ChainEnabled || direct == max)
        {
            if (direct == 0)
            {
                GoWaiting(ent, shortReagent);
                return false;
            }

            comp.RunBatches = direct;
            return true;
        }

        if (ComputePlan(ent, max, out var steps, out _) == null)
        {
            comp.RunBatches = max;
            StorePlan(comp, steps);
            return true;
        }

        // the largest count known to work; up to `direct` needs no plan
        var lo = direct;
        List<CMUSynthStep>? loSteps = null;
        if (lo == 0)
        {
            if (ComputePlan(ent, 1, out loSteps, out var block) is { } fault)
            {
                if (IsShortInputFault(fault))
                {
                    GoWaiting(ent, block);
                }
                else
                {
                    comp.PlanBlockReagent = block;
                    SetFault(ent, fault);
                }

                return false;
            }

            lo = 1;
        }

        var hi = max; // known not to work
        while (hi - lo > 1)
        {
            var mid = (lo + hi) / 2;
            if (ComputePlan(ent, mid, out var midSteps, out _) == null)
            {
                lo = mid;
                loSteps = midSteps;
            }
            else
            {
                hi = mid;
            }
        }

        comp.RunBatches = lo;
        StorePlan(comp, loSteps);
        return true;
    }

    /// <summary>
    /// FLOW-02: the most batches in 1..<paramref name="max"/> the inputs cover for the program as a single step, or 0
    /// with the reagent short for even one batch.
    /// </summary>
    private int MaxDirectBatches(Entity<CMUChemSynthesizerComponent> ent, ReactionPrototype reaction, int max, out string? shortReagent)
    {
        shortReagent = null;
        var inputs = CollectInputs(ent);
        var have = new Dictionary<ProtoId<ReagentPrototype>, FixedPoint2>();
        foreach (var reagent in reaction.Reactants.Keys)
        {
            have[reagent] = AvailableInput(ent, inputs, reagent);
        }

        for (var batches = max; batches >= 1; batches--)
        {
            var covered = true;
            foreach (var (reagent, info) in reaction.Reactants)
            {
                var need = info.Catalyst ? info.Amount : info.Amount * batches;
                if (have[reagent] >= need)
                    continue;

                covered = false;
                if (batches == 1)
                    shortReagent = reagent.Id;

                break;
            }

            if (covered)
                return batches;
        }

        return 0;
    }

    /// <summary>
    /// Replays the plan against the buffer (its current contents, what each step meters out of it first, what each
    /// non-final step parks in it) and returns the first intermediate that would not fit, or null.
    /// </summary>
    private string? FindBufferOverflow(Entity<CMUChemSynthesizerComponent> ent, List<CMUSynthStep> steps)
    {
        if (!TryGetBuffer(ent, out var bufferEnt))
            return steps.Count > 1 ? steps[0].Product : null;

        var buffer = bufferEnt.Value.Comp.Solution;
        var held = new Dictionary<string, FixedPoint2>();
        var total = FixedPoint2.Zero;
        foreach (var quantity in buffer.Contents)
        {
            held[quantity.Reagent.Prototype] = held.GetValueOrDefault(quantity.Reagent.Prototype) + quantity.Quantity;
            total += quantity.Quantity;
        }

        for (var i = 0; i < steps.Count - 1; i++)
        {
            var step = steps[i];
            if (!_proto.TryIndex<ReactionPrototype>(step.Reaction, out var reaction) ||
                !reaction.Products.TryGetValue(step.Product, out var perBatch))
            {
                return step.Product;
            }

            // the meter reads the buffer first
            foreach (var (reactant, info) in reaction.Reactants)
            {
                var need = info.Catalyst ? info.Amount : info.Amount * step.Batches;
                var have = held.GetValueOrDefault(reactant.Id);
                var take = FixedPoint2.Min(have, need);
                held[reactant.Id] = have - take;
                total -= take;
            }

            var made = perBatch * step.Batches;
            held[step.Product] = held.GetValueOrDefault(step.Product) + made;
            total += made;
            if (total > buffer.MaxVolume)
                return step.Product;
        }

        return null;
    }

    /// <summary>Claims every reactant of one step (making what is missing first) and then emits the step.</summary>
    private bool TryPlanStep(Entity<CMUChemSynthesizerComponent> ent, PlanContext ctx, ReactionPrototype reaction,
        string product, int batches, int depth)
    {
        foreach (var (reactant, info) in reaction.Reactants)
        {
            // a catalyst is metered once and dumped with the leftovers, so it is used up too
            var need = info.Catalyst ? info.Amount : info.Amount * batches;
            if (!TryClaim(ent, ctx, reactant.Id, need, depth))
                return false;
        }

        ctx.Steps.Add(new CMUSynthStep(reaction.ID, product, batches));
        return true;
    }

    /// <summary>
    /// Takes <paramref name="need"/> of <paramref name="reagent"/> from the ledger and plans a step (at
    /// <paramref name="depth"/> + 1) for whatever is missing, trying the recipes in <see cref="OrderPlanRecipes"/> order
    /// with the same gates as a single step. A failed recipe's claims and steps are rolled back.
    /// </summary>
    private bool TryClaim(Entity<CMUChemSynthesizerComponent> ent, PlanContext ctx, string reagent, FixedPoint2 need, int depth)
    {
        var failure = ctx.SaveFailure();
        if (!ctx.Ledger.TryGetValue(reagent, out var have))
            have = AvailableInput(ent, ctx.Inputs, reagent);

        var take = FixedPoint2.Min(have, need);
        ctx.Ledger[reagent] = have - take;
        var missing = need - take;
        if (missing <= FixedPoint2.Zero)
            return true;

        var stepDepth = depth + 1;
        if (!_index.TryGetRecipes(reagent, out var recipes) || !_reagent.TryIndex(reagent, out var proto))
        {
            ctx.Fail(stepDepth, FaultPlanMissingInput, reagent);
            return false;
        }

        if (ctx.Path.Contains(reagent))
        {
            ctx.Fail(stepDepth, FaultPlanCycle, reagent);
            return false;
        }

        if (stepDepth > ent.Comp.MaxPlanDepth)
        {
            ctx.Fail(stepDepth, FaultPlanTooDeep, reagent);
            return false;
        }

        var capacity = ChamberCapacity(ent);
        ctx.Path.Add(reagent);
        try
        {
            foreach (var recipe in OrderPlanRecipes(recipes, reagent))
            {
                if (!recipe.Products.TryGetValue(reagent, out var perBatch) || perBatch <= FixedPoint2.Zero)
                    continue;

                if ((CheckRecipe(ent, recipe, proto) ?? CheckConditions(ent, recipe, out _)) is { } fault)
                {
                    ctx.Fail(stepDepth, fault, reagent);
                    continue;
                }

                var batches = (missing.Value + perBatch.Value - 1) / perBatch.Value;
                if (batches > ent.Comp.MaxBatches || StepVolume(recipe, batches) > capacity)
                {
                    ctx.Fail(stepDepth, FaultTooLarge, reagent);
                    continue;
                }

                var ledger = new Dictionary<string, FixedPoint2>(ctx.Ledger);
                var steps = ctx.Steps.Count;
                if (TryPlanStep(ent, ctx, recipe, reagent, batches, stepDepth))
                {
                    // what the step makes beyond the need waits in the buffer for later steps
                    ctx.Ledger[reagent] = ctx.Ledger.GetValueOrDefault(reagent) + perBatch * batches - missing;
                    ctx.RestoreFailure(failure);
                    return true;
                }

                ctx.Ledger.Clear();
                foreach (var (id, amount) in ledger)
                {
                    ctx.Ledger[id] = amount;
                }

                ctx.Steps.RemoveRange(steps, ctx.Steps.Count - steps);
            }

            return false;
        }
        finally
        {
            ctx.Path.Remove(reagent);
        }
    }

    private static void ClearPlan(CMUChemSynthesizerComponent comp)
    {
        comp.Plan.Clear();
        comp.PlanIndex = 0;
        comp.PlanBlockReagent = null;
    }

    /// <summary>A plan is running and its current step is not the last one.</summary>
    private static bool IsIntermediateStep(CMUChemSynthesizerComponent comp)
    {
        return comp.Plan.Count > 0 && comp.PlanIndex < comp.Plan.Count - 1;
    }

    /// <summary>
    /// The step being run: the current plan step, or the program itself with the run's batch count
    /// (<see cref="GetRunBatches"/>, FLOW-02) when there is no plan.
    /// </summary>
    private bool TryGetStep(CMUChemSynthesizerComponent comp, [NotNullWhen(true)] out ReactionPrototype? reaction,
        out ProtoId<ReagentPrototype> product, out int batches)
    {
        product = default;
        batches = GetRunBatches(comp);
        if (comp.Plan.Count > 0 && comp.PlanIndex >= 0 && comp.PlanIndex < comp.Plan.Count)
        {
            var step = comp.Plan[comp.PlanIndex];
            batches = step.Batches;
            if (!_proto.TryIndex<ReactionPrototype>(step.Reaction, out reaction) || reaction.Products.Count == 0)
                return false;

            product = reaction.Products.ContainsKey(step.Product)
                ? new ProtoId<ReagentPrototype>(step.Product)
                : reaction.Products.Keys.First();
            return true;
        }

        if (!TryGetProgram(comp, out reaction))
            return false;

        product = GetProduct(comp, reaction);
        return true;
    }

    #endregion

    #region Cycle

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CMUChemSynthesizerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Stage is CMUChemSynthesizerStage.Idle or CMUChemSynthesizerStage.Fault)
            {
                if (comp.Waiting && comp.Stage == CMUChemSynthesizerStage.Idle)
                    UpdateWaiting(uid, comp, frameTime);

                continue;
            }

            // Power loss pauses: the accumulator does not advance and no stage runs. The inputs already sit in the
            // chamber, so nothing can be pulled twice.
            if (!this.IsPowered(uid, EntityManager))
                continue;

            var interval = (float) comp.StepInterval.TotalSeconds;
            comp.Accumulator += frameTime;
            if (comp.Accumulator < interval)
                continue;

            comp.Accumulator -= interval;
            Step(new Entity<CMUChemSynthesizerComponent>(uid, comp), interval);
        }
    }

    /// <summary>
    /// FLOW-02: a waiting machine tries to start again every <see cref="CMUChemSynthesizerComponent.WaitCheckInterval"/>
    /// of powered time; it resumes as soon as the inputs allow a batch.
    /// </summary>
    private void UpdateWaiting(EntityUid uid, CMUChemSynthesizerComponent comp, float frameTime)
    {
        if (!this.IsPowered(uid, EntityManager))
            return;

        comp.Accumulator += frameTime;
        if (comp.Accumulator < (float) comp.WaitCheckInterval.TotalSeconds)
            return;

        comp.Accumulator = 0f;
        if (!comp.Continuous)
        {
            comp.Waiting = false;
            comp.PlanBlockReagent = null;
            Dirty(uid, comp);
            return;
        }

        TryStart(uid, comp);
    }

    private void Step(Entity<CMUChemSynthesizerComponent> ent, float interval)
    {
        if (!TryGetChamber(ent, out var chamberEnt))
        {
            SetFault(ent, FaultNoChamber);
            return;
        }

        var chamber = chamberEnt.Value;
        _powerState.TrySetWorkingState(ent.Owner, true);

        switch (ent.Comp.Stage)
        {
            case CMUChemSynthesizerStage.Metering:
                Meter(ent, chamber);
                break;
            case CMUChemSynthesizerStage.Conditioning:
                Condition(ent, chamber, interval);
                break;
            case CMUChemSynthesizerStage.Reacting:
                React(ent, chamber);
                break;
            case CMUChemSynthesizerStage.Output:
                Output(ent, chamber);
                break;
        }

        ent.Comp.ChamberTemperature = chamber.Comp.Solution.Temperature;
        Dirty(ent);
    }

    private void Meter(Entity<CMUChemSynthesizerComponent> ent, Entity<SolutionComponent> chamber)
    {
        if (!TryGetStep(ent.Comp, out var reaction, out _, out var batches))
        {
            FaultAndDump(ent, chamber, ent.Comp.Plan.Count > 0 ? FaultPlanInvalid : FaultNoProgram);
            return;
        }

        // leftovers never carry into the next batch (catalyst starvation, stray by-products)
        DumpChamber(ent, chamber);

        var needs = new Dictionary<ProtoId<ReagentPrototype>, FixedPoint2>();
        foreach (var (reagent, info) in reaction.Reactants)
        {
            needs[reagent] = info.Catalyst ? info.Amount : info.Amount * batches;
        }

        if (StepVolume(reaction, batches) > ChamberCapacity(ent))
        {
            SetFault(ent, FaultTooLarge);
            return;
        }

        var inputs = CollectInputs(ent);

        // Check everything before moving anything: a short input pulls nothing.
        foreach (var (reagent, need) in needs)
        {
            if (AvailableInput(ent, inputs, reagent) < need)
            {
                // FLOW-02: inputs taken between the start and the meter; a continuous run waits and re-picks its
                // batch count. A later chain step short of inputs is still a fault (its intermediates stay buffered).
                if (ent.Comp.Continuous && ent.Comp.PlanIndex == 0)
                {
                    GoWaiting(ent, reagent.Id);
                    return;
                }

                SetFault(ent, FaultMissingInput);
                return;
            }
        }

        if (CheckConditions(ent, reaction, out var target) is { } fault)
        {
            SetFault(ent, fault);
            return;
        }

        foreach (var (reagent, need) in needs)
        {
            // Should not happen after the check above; never leave a partial batch in the chamber.
            if (PullInput(ent, inputs, chamber, reagent, need) < need)
            {
                FaultAndDump(ent, chamber, FaultMissingInput);
                return;
            }
        }

        ent.Comp.TargetTemperature = target;
        ent.Comp.ConditioningElapsed = 0f;
        ent.Comp.Stage = CMUChemSynthesizerStage.Conditioning;
    }

    private void Condition(Entity<CMUChemSynthesizerComponent> ent, Entity<SolutionComponent> chamber, float interval)
    {
        if (!TryGetStep(ent.Comp, out var reaction, out _, out _))
        {
            FaultAndDump(ent, chamber, ent.Comp.Plan.Count > 0 ? FaultPlanInvalid : FaultNoProgram);
            return;
        }

        var solution = chamber.Comp.Solution;
        var ventLimit = GetVentLimit(GetInvolved(solution, reaction));
        if (solution.Temperature >= ventLimit)
        {
            Vent(ent, chamber);
            return;
        }

        if (!TryGetBand(reaction, ventLimit, out var low, out var high, out var target))
        {
            FaultAndDump(ent, chamber, FaultNoBand);
            return;
        }

        ent.Comp.TargetTemperature = target;
        if (!InBand(solution.Temperature, low, high, target))
        {
            _thermostat.StepToward(chamber, target, ent.Comp.EnergyPerSecond * interval, CMUThermostatMode.Both);

            if (solution.Temperature >= ventLimit)
            {
                Vent(ent, chamber);
                return;
            }

            if (!InBand(solution.Temperature, low, high, target))
            {
                ent.Comp.ConditioningElapsed += interval;
                if (ent.Comp.ConditioningElapsed >= (float) ent.Comp.ConditioningTimeout.TotalSeconds)
                    FaultAndDump(ent, chamber, FaultConditioningTimeout);

                return;
            }
        }

        // reached only by tolerance (a band narrower than it): settle exactly on the in-band target
        if (solution.Temperature < low || solution.Temperature > high)
            _solutions.SetTemperature(chamber, target);

        ent.Comp.Stage = CMUChemSynthesizerStage.Reacting;
        React(ent, chamber);
    }

    private void React(Entity<CMUChemSynthesizerComponent> ent, Entity<SolutionComponent> chamber)
    {
        if (!TryGetStep(ent.Comp, out var reaction, out var product, out _))
        {
            FaultAndDump(ent, chamber, ent.Comp.Plan.Count > 0 ? FaultPlanInvalid : FaultNoProgram);
            return;
        }

        var solution = chamber.Comp.Solution;
        var involved = GetInvolved(solution, reaction);
        var ventLimit = GetVentLimit(involved);
        if (solution.Temperature >= ventLimit)
        {
            Vent(ent, chamber);
            return;
        }

        TryComp<ReactionMixerComponent>(ent.Owner, out var mixer);
        if (FindSideReaction(reaction, involved, solution.Temperature, mixer) != null)
        {
            FaultAndDump(ent, chamber, FaultSideReaction);
            return;
        }

        var before = solution.GetTotalPrototypeQuantity(product);

        solution.CanReact = true;
        try
        {
            _solutions.UpdateChemicals(chamber, true, mixer);
        }
        finally
        {
            solution.CanReact = false;
        }

        // After reacting, only the ignition point itself counts: a Hyperthermic synthesis heats up to its 400 K
        // clamp, which is the guarded limit of a level-6+ ignitable product but still 10 K under its ignition.
        if (solution.Temperature >= GetVentLimit(GetInvolved(solution, reaction), 0f))
        {
            Vent(ent, chamber);
            return;
        }

        if (solution.GetTotalPrototypeQuantity(product) <= before)
        {
            FaultAndDump(ent, chamber, FaultReactionFailed);
            return;
        }

        ent.Comp.Stage = CMUChemSynthesizerStage.Output;
        Output(ent, chamber);
    }

    private void Output(Entity<CMUChemSynthesizerComponent> ent, Entity<SolutionComponent> chamber)
    {
        var solution = chamber.Comp.Solution;
        if (IsIntermediateStep(ent.Comp))
        {
            ParkIntermediate(ent, chamber);
            return;
        }

        if (TryGetStep(ent.Comp, out _, out var product, out _))
        {
            PushProductToPort(ent, chamber, product);

            var quantity = solution.GetTotalPrototypeQuantity(product);
            if (quantity > FixedPoint2.Zero)
            {
                if (!TryGetBeakerSolution(ent, ent.Comp.OutputSlot, out var output) ||
                    output.Value.Comp.Solution.AvailableVolume <= FixedPoint2.Zero)
                {
                    // blocked: stay in Output until there is room
                    ent.Comp.Fault = FaultOutputBlocked;
                    return;
                }

                var take = FixedPoint2.Min(quantity, output.Value.Comp.Solution.AvailableVolume);
                var split = solution.SplitSolutionWithOnly(take, product);
                _solutions.UpdateChemicals(chamber, false);
                _solutions.TryAddSolution(output.Value, split);

                if (take < quantity)
                {
                    ent.Comp.Fault = FaultOutputBlocked;
                    return;
                }
            }
        }

        DumpChamber(ent, chamber);
        // a finished run leaves no surplus intermediates behind (also after a re-plan that became single-step)
        DumpBuffer(ent);
        ClearPlan(ent.Comp);
        GoIdle(ent);

        // FLOW-02: the next run starts straight away, or waits when the inputs allow no batch
        if (ent.Comp.Continuous)
            TryStart(ent.Owner, ent.Comp);
    }

    /// <summary>
    /// M4: a non-final step's product goes to the buffer at room temperature (FLOW-14: a hot intermediate would make
    /// the next step vent at Conditioning), the rest to waste, and the next step is metered.
    /// </summary>
    private void ParkIntermediate(Entity<CMUChemSynthesizerComponent> ent, Entity<SolutionComponent> chamber)
    {
        if (!TryGetStep(ent.Comp, out _, out var product, out _))
        {
            FaultAndDump(ent, chamber, FaultPlanInvalid);
            return;
        }

        if (!TryGetBuffer(ent, out var buffer))
        {
            FaultAndDump(ent, chamber, FaultNoChamber);
            return;
        }

        var solution = chamber.Comp.Solution;
        var quantity = solution.GetTotalPrototypeQuantity(product);
        if (quantity > buffer.Value.Comp.Solution.AvailableVolume)
        {
            // Planning refuses this up front (FindBufferOverflow); if it still happens, name the intermediate that did not fit.
            FaultAndDump(ent, chamber, FaultPlanBufferFull);
            ent.Comp.PlanBlockReagent = product;
            Dirty(ent);
            return;
        }

        if (quantity > FixedPoint2.Zero)
        {
            var split = solution.SplitSolutionWithOnly(quantity, product);
            _solutions.UpdateChemicals(chamber, false);
            _solutions.ForceAddSolution(buffer.Value, split);
        }

        _solutions.SetTemperature(buffer.Value, RoomTemperature);
        DumpChamber(ent, chamber);

        ent.Comp.PlanIndex++;
        ent.Comp.Fault = null;
        ent.Comp.TargetTemperature = float.NaN;
        ent.Comp.ConditioningElapsed = 0f;
        ent.Comp.Stage = CMUChemSynthesizerStage.Metering;
        Dirty(ent);
    }

    /// <summary>
    /// FLOW-13: while the product port is linked, the product goes into the tank (as much as its room allows, past the
    /// machine's own reservation) and the port carries it away. What does not fit stays for the output beaker.
    /// </summary>
    private void PushProductToPort(Entity<CMUChemSynthesizerComponent> ent, Entity<SolutionComponent> chamber, ProtoId<ReagentPrototype> product)
    {
        if (!TryGetProductPort(ent, out var port) || port.Links.Count == 0 ||
            !TryComp<FluidTankComponent>(ent, out var tank))
        {
            return;
        }

        var solution = chamber.Comp.Solution;
        var quantity = solution.GetTotalPrototypeQuantity(product);
        if (quantity <= FixedPoint2.Zero)
            return;

        _pushingProduct = ent.Owner;
        try
        {
            var room = _fluids.Room((ent.Owner, tank), product.Id);
            var push = FixedPoint2.Min(quantity, FixedPoint2.New(room));
            // FixedPoint2.New rounds by a float epsilon; never push more than the tank takes
            if (push.Float() > room)
                push -= FixedPoint2.Epsilon;

            if (push <= FixedPoint2.Zero)
                return;

            var split = solution.SplitSolutionWithOnly(push, product);
            _solutions.UpdateChemicals(chamber, false);
            _fluids.Add(ent.Owner, product.Id, split.Volume.Float());
        }
        finally
        {
            _pushingProduct = null;
        }
    }

    #endregion

    #region Inputs

    /// <summary>
    /// The solutions a Meter reads, in pull order: the intermediate buffer (M4) first, then the input beakers. The fluid
    /// tank is read after them by <see cref="AvailableInput"/> and <see cref="PullInput"/>.
    /// </summary>
    private List<Entity<SolutionComponent>> CollectInputs(Entity<CMUChemSynthesizerComponent> ent)
    {
        var inputs = new List<Entity<SolutionComponent>>();
        if (TryGetBuffer(ent, out var buffer))
            inputs.Add(buffer.Value);

        foreach (var slot in ent.Comp.InputSlots)
        {
            if (TryGetBeakerSolution(ent, slot, out var input))
                inputs.Add(input.Value);
        }

        return inputs;
    }

    /// <summary>How much of <paramref name="reagent"/> the machine can meter: the buffer and input beakers plus the tank.</summary>
    private FixedPoint2 AvailableInput(Entity<CMUChemSynthesizerComponent> ent, List<Entity<SolutionComponent>> beakers, ProtoId<ReagentPrototype> reagent)
    {
        var have = TankQuantity(ent, reagent);
        foreach (var beaker in beakers)
        {
            have += beaker.Comp.Solution.GetTotalPrototypeQuantity(reagent);
        }

        return have;
    }

    /// <summary>
    /// Moves up to <paramref name="need"/> of <paramref name="reagent"/> into the chamber, from the input beakers first
    /// and then the tank, and returns how much was moved. A tank portion is the truncated <see cref="FixedPoint2"/> amount,
    /// exactly that much leaves the tank (not FLOW-15), and it enters the chamber at room temperature.
    /// </summary>
    private FixedPoint2 PullInput(Entity<CMUChemSynthesizerComponent> ent, List<Entity<SolutionComponent>> beakers,
        Entity<SolutionComponent> chamber, ProtoId<ReagentPrototype> reagent, FixedPoint2 need)
    {
        var remaining = need;
        foreach (var beaker in beakers)
        {
            if (remaining <= FixedPoint2.Zero)
                break;

            var beakerSolution = beaker.Comp.Solution;
            var have = beakerSolution.GetTotalPrototypeQuantity(reagent);
            if (have <= FixedPoint2.Zero)
                continue;

            var split = beakerSolution.SplitSolutionWithOnly(FixedPoint2.Min(have, remaining), reagent);
            _solutions.UpdateChemicals(beaker, false);
            remaining -= split.Volume;
            _solutions.ForceAddSolution(chamber, split);
        }

        if (remaining > FixedPoint2.Zero)
        {
            var take = FixedPoint2.Min(TankQuantity(ent, reagent), remaining);
            if (take > FixedPoint2.Zero)
            {
                // The tolerance may claim up to TankTolerance more than the float tank holds; never add more than was removed.
                var removed = _fluids.Remove(ent.Owner, reagent.Id, take.Float());
                take = FixedPoint2.Min(take, FixedPoint2.New(removed + TankTolerance));
                var portion = new Solution(reagent.Id, take) { Temperature = RoomTemperature };
                _solutions.ForceAddSolution(chamber, portion);
                remaining -= take;
            }
        }

        return need - remaining;
    }

    /// <summary>
    /// The tank's amount of <paramref name="reagent"/>, truncated to <see cref="FixedPoint2"/> after allowing
    /// <see cref="TankTolerance"/> of float drift (so 9.99995 counts as 10, as <c>FluidSystem.TryUse</c> would).
    /// </summary>
    private FixedPoint2 TankQuantity(Entity<CMUChemSynthesizerComponent> ent, ProtoId<ReagentPrototype> reagent)
    {
        var amount = _fluids.GetAmount(ent.Owner, reagent.Id);
        return amount > 0f ? FixedPoint2.New(amount + TankTolerance) : FixedPoint2.Zero;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// FLOW-13: the product port carries only the programmed product. With no program it is off (scale 0), since a
    /// port with no kind would push whatever the tank has most of, i.e. the reactants.
    /// </summary>
    private void SyncProductPort(Entity<CMUChemSynthesizerComponent> ent, bool notify = true)
    {
        if (!TryGetProductPort(ent, out var port))
            return;

        var kind = ent.Comp.Program != null ? ent.Comp.ProgramProduct : null;
        var scale = kind != null ? 1f : 0f;
        if (port.Kind == kind && port.Scale.Equals(scale))
            return;

        port.Kind = kind;
        port.Scale = scale;
        if (notify)
            _fluids.ConnectionsChanged(ent.Owner);
    }

    private bool TryGetProductPort(Entity<CMUChemSynthesizerComponent> ent, [NotNullWhen(true)] out FluidPort? port)
    {
        port = null;
        if (!TryComp<FluidPortsComponent>(ent, out var ports))
            return false;

        port = ports.Ports.Find(p => p.Id == ent.Comp.ProductPort);
        return port != null;
    }

    private bool TryGetProgram(CMUChemSynthesizerComponent comp, [NotNullWhen(true)] out ReactionPrototype? reaction)
    {
        reaction = null;
        return comp.Program != null && _proto.TryIndex<ReactionPrototype>(comp.Program, out reaction);
    }

    private static ProtoId<ReagentPrototype> GetProduct(CMUChemSynthesizerComponent comp, ReactionPrototype reaction)
    {
        if (comp.ProgramProduct != null && reaction.Products.ContainsKey(comp.ProgramProduct))
            return comp.ProgramProduct;

        return reaction.Products.Keys.First();
    }

    private bool TryGetChamber(Entity<CMUChemSynthesizerComponent> ent, [NotNullWhen(true)] out Entity<SolutionComponent>? chamber)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.ChamberSolution, out chamber))
            return false;

        // the chamber only ever reacts inside React
        chamber.Value.Comp.Solution.CanReact = false;
        return true;
    }

    /// <summary>The intermediate buffer (M4); like the chamber, it never reacts.</summary>
    private bool TryGetBuffer(Entity<CMUChemSynthesizerComponent> ent, [NotNullWhen(true)] out Entity<SolutionComponent>? buffer)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.BufferSolution, out buffer))
            return false;

        buffer.Value.Comp.Solution.CanReact = false;
        return true;
    }

    /// <summary>How much one step may fill the chamber with (reactants and products).</summary>
    private FixedPoint2 ChamberCapacity(Entity<CMUChemSynthesizerComponent> ent)
    {
        return TryGetChamber(ent, out var chamber)
            ? FixedPoint2.Min(chamber.Value.Comp.Solution.MaxVolume, ent.Comp.MaxChamber)
            : ent.Comp.MaxChamber;
    }

    private bool TryGetBeakerSolution(Entity<CMUChemSynthesizerComponent> ent, string slot, [NotNullWhen(true)] out Entity<SolutionComponent>? solution)
    {
        solution = null;
        return _itemSlots.GetItemOrNull(ent.Owner, slot) is { } item && TryGetItemSolution(item, out solution);
    }

    /// <summary>The dispenser solution of a beaker-like item.</summary>
    private bool TryGetItemSolution(EntityUid item, [NotNullWhen(true)] out Entity<SolutionComponent>? solution)
    {
        solution = null;
        return TryComp<FitsInDispenserComponent>(item, out var fits) &&
               _solutions.TryGetSolution(item, fits.Solution, out solution);
    }

    private static HashSet<string> GetInvolved(Solution solution, ReactionPrototype reaction)
    {
        var involved = new HashSet<string>();
        foreach (var reagent in solution.Contents)
        {
            involved.Add(reagent.Reagent.Prototype);
        }

        foreach (var product in reaction.Products.Keys)
        {
            involved.Add(product.Id);
        }

        return involved;
    }

    /// <summary>
    /// The lowest <c>ignition − IgnitionGuardMargin</c> over the generated chemicals among <paramref name="reagents"/>,
    /// or +∞ when none can ignite. Uses the generated <c>{ID}Ignition</c> reaction (what would actually fire) and the
    /// procedural data when the server has it.
    /// </summary>
    public float GetVentLimit(IEnumerable<string> reagents, float margin = SharedReagentGeneratorSystem.IgnitionGuardMargin)
    {
        var limit = float.PositiveInfinity;
        foreach (var id in reagents)
        {
            var ignitionId = SharedReagentGeneratorSystem.IgnitionRecipeId(id);
            if (_generator.IsIgnitionRecipe(ignitionId) &&
                _proto.TryIndex<ReactionPrototype>(ignitionId, out var ignition))
            {
                limit = MathF.Min(limit, ignition.MinimumTemperature - margin);
            }

            if (_generator.ProceduralReagentData.TryGetValue(id, out var data) && data.Effects != null)
            {
                var level = SharedReagentGeneratorSystem.IgnitionLevel(data.Effects);
                if (level > 0)
                    limit = MathF.Min(limit, SharedReagentGeneratorSystem.IgnitionTemperature(level) - margin);
            }
        }

        return limit;
    }

    /// <summary>
    /// The band the reaction runs in, capped under the vent limit, and the temperature the chamber is driven to:
    /// the midpoint of a two-sided band, 5 K inside a one-sided one, room temperature when there is no gate.
    /// </summary>
    private static bool TryGetBand(ReactionPrototype reaction, float ventLimit, out float low, out float high, out float target)
    {
        low = MathF.Max(0f, reaction.MinimumTemperature);
        high = MathF.Min(reaction.MaximumTemperature, ventLimit - VentBandGap);

        var hasLow = low > 0f;
        var hasHigh = float.IsFinite(high);

        if (hasLow && hasHigh)
            target = (low + high) / 2f;
        else if (hasLow)
            target = low + OneSidedBandOffset;
        else if (hasHigh)
            target = MathF.Min(RoomTemperature, high - OneSidedBandOffset);
        else
            target = RoomTemperature;

        if (high < low)
            return false;

        target = Math.Clamp(target, low, high);
        return target > 0f;
    }

    /// <summary>
    /// Inside [low, high], or within the thermostat's tolerance of the target (a band narrower than that tolerance
    /// still counts as reached once the chamber is at its target).
    /// </summary>
    private static bool InBand(float temperature, float low, float high, float target)
    {
        return (temperature >= low && temperature <= high) ||
               MathF.Abs(temperature - target) <= CMUThermostatPlateSystem.TargetTolerance;
    }

    /// <summary>
    /// Heuristic side-reaction guard, a private mirror of the temperature and mixer checks of
    /// <c>ChemicalReactionSystem.CanReact</c>: another reaction whose reactants are all present and that is eligible
    /// at <paramref name="temperature"/> with <paramref name="mixer"/>. Reactions that only make the programmed
    /// products are not side reactions. Ratios and catalyst amounts are not considered (conservative).
    /// </summary>
    private ReactionPrototype? FindSideReaction(ReactionPrototype program, HashSet<string> present, float temperature, ReactionMixerComponent? mixer)
    {
        foreach (var other in _proto.EnumeratePrototypes<ReactionPrototype>())
        {
            if (other.ID == program.ID || other.Reactants.Count == 0)
                continue;

            if (temperature < other.MinimumTemperature || temperature > other.MaximumTemperature)
                continue;

            if (other.MixingCategories != null &&
                (mixer == null || other.MixingCategories.Except(mixer.ReactionTypes).Any()))
            {
                continue;
            }

            var allPresent = true;
            foreach (var reactant in other.Reactants.Keys)
            {
                if (present.Contains(reactant.Id))
                    continue;

                allPresent = false;
                break;
            }

            if (!allPresent)
                continue;

            var sameProducts = other.Products.Count > 0;
            foreach (var product in other.Products.Keys)
            {
                if (program.Products.ContainsKey(product))
                    continue;

                sameProducts = false;
                break;
            }

            if (sameProducts)
                continue;

            return other;
        }

        return null;
    }

    private void Vent(Entity<CMUChemSynthesizerComponent> ent, Entity<SolutionComponent> chamber)
    {
        _popup.PopupEntity(Loc.GetString(FaultOverheat), ent.Owner, PopupType.MediumCaution);
        FaultAndDump(ent, chamber, FaultOverheat);
    }

    /// <summary>Moves the whole buffer to the waste beaker and spills what does not fit.</summary>
    private void DumpBuffer(Entity<CMUChemSynthesizerComponent> ent)
    {
        if (TryGetBuffer(ent, out var buffer))
            DumpChamber(ent, buffer.Value);
    }

    /// <summary>Dumps the chamber and the buffer: everything the current run holds.</summary>
    private void DumpRun(Entity<CMUChemSynthesizerComponent> ent)
    {
        if (TryGetChamber(ent, out var chamber))
            DumpChamber(ent, chamber.Value);

        DumpBuffer(ent);
    }

    /// <summary>Moves the whole solution (the chamber, or the buffer) to the waste beaker and spills what does not fit.</summary>
    private void DumpChamber(Entity<CMUChemSynthesizerComponent> ent, Entity<SolutionComponent> chamber)
    {
        var solution = chamber.Comp.Solution;
        if (solution.Volume <= FixedPoint2.Zero)
            return;

        var all = solution.SplitSolution(solution.Volume);
        _solutions.UpdateChemicals(chamber, false);

        if (TryGetBeakerSolution(ent, ent.Comp.WasteSlot, out var waste))
        {
            var room = waste.Value.Comp.Solution.AvailableVolume;
            if (room > FixedPoint2.Zero)
            {
                var part = all.SplitSolution(FixedPoint2.Min(room, all.Volume));
                _solutions.TryAddSolution(waste.Value, part);
            }
        }

        if (all.Volume > FixedPoint2.Zero)
            _puddle.TrySpillAt(ent.Owner, all, out _);
    }

    private void FaultAndDump(Entity<CMUChemSynthesizerComponent> ent, Entity<SolutionComponent> chamber, string fault)
    {
        DumpChamber(ent, chamber);
        DumpBuffer(ent);
        SetFault(ent, fault);
    }

    private void SetFault(Entity<CMUChemSynthesizerComponent> ent, string fault)
    {
        // FLOW-02: a real fault ends continuous waiting; Continuous itself stays set
        ent.Comp.Waiting = false;
        ent.Comp.Fault = fault;
        ent.Comp.Stage = CMUChemSynthesizerStage.Fault;
        ent.Comp.TargetTemperature = float.NaN;
        ent.Comp.Accumulator = 0f;
        _powerState.TrySetWorkingState(ent.Owner, false);
        Dirty(ent);
    }

    /// <summary>
    /// FLOW-02: continuous mode with too little input for one batch: idle with no fault, naming the short reagent
    /// (<see cref="CMUChemSynthesizerComponent.PlanBlockReagent"/>) when known. The buffer is kept, as for a
    /// <see cref="FaultMissingInput"/>. Repeated checks that still find nothing do not dirty the component.
    /// </summary>
    private void GoWaiting(Entity<CMUChemSynthesizerComponent> ent, string? reagent)
    {
        var comp = ent.Comp;
        comp.Accumulator = 0f;
        if (comp.Waiting && comp.Stage == CMUChemSynthesizerStage.Idle && comp.Fault == null &&
            comp.Plan.Count == 0 && comp.PlanBlockReagent == reagent)
        {
            return;
        }

        ClearPlan(comp);
        comp.PlanBlockReagent = reagent;
        comp.Waiting = true;
        GoIdle(ent);
    }

    private void GoIdle(Entity<CMUChemSynthesizerComponent> ent)
    {
        ent.Comp.Fault = null;
        ent.Comp.Stage = CMUChemSynthesizerStage.Idle;
        ent.Comp.TargetTemperature = float.NaN;
        ent.Comp.Accumulator = 0f;
        _powerState.TrySetWorkingState(ent.Owner, false);
        Dirty(ent);
    }

    #endregion
}
