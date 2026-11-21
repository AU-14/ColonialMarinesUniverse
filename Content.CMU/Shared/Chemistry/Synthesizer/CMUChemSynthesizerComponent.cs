using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Chemistry.Synthesizer;

/// <summary>
/// N10 (GraphAudit-ChemBotanyPlan, variant B): a machine that runs one programmed reaction step in a sealed internal
/// chamber. It meters exactly <c>amount × batches</c> of each reactant out of its input beakers, holds the chamber in
/// the reaction's temperature band (never at or above a procgen ignition point), reacts it with its own mixer and
/// sends the product to the output beaker and everything else to the waste beaker.
/// Run by the server <c>CMUChemSynthesizerSystem</c>.
/// M3 (GraphAudit-DeferredFollowups): reactants may also come from the fluid tank (fed by the <c>reagent_in_*</c>
/// ports), the product leaves through <see cref="ProductPort"/> when it is linked, inserters load and unload the
/// beakers (gated by the server system), and a vial in <see cref="SampleSlot"/> can program the machine without a report.
/// M4: with <see cref="ChainEnabled"/> a start whose inputs lack the program's reactants plans a chain of up to
/// <see cref="MaxPlanDepth"/> steps (<see cref="Plan"/>), parking intermediates in <see cref="BufferSolution"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUChemSynthesizerComponent : Component
{
    /// <summary>The programmed reaction prototype id, or null when unprogrammed.</summary>
    [DataField, AutoNetworkedField]
    public string? Program;

    /// <summary>The reagent the programmed reaction is run for (what goes to the output beaker).</summary>
    [DataField, AutoNetworkedField]
    public string? ProgramProduct;

    /// <summary>How many unit reactions to run per cycle; with <see cref="Continuous"/> the most one run may make.</summary>
    [DataField, AutoNetworkedField]
    public int Batches = 1;

    /// <summary>
    /// CMU14 FLOW-02: the batch count of the current run, picked once at its start. Equal to <see cref="Batches"/> unless
    /// <see cref="Continuous"/>, where it is the most the inputs and the chamber allow (1 to <see cref="Batches"/>).
    /// 0 means not set (a machine saved before this field), which reads as <see cref="Batches"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int RunBatches;

    /// <summary>
    /// CMU14 FLOW-02: after a finished run the machine starts the next one; when the inputs allow no batch it waits
    /// (<see cref="Waiting"/>) instead of faulting.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Continuous;

    /// <summary>CMU14 FLOW-02: idle in continuous mode until the inputs allow a batch. Never set together with a fault.</summary>
    [AutoNetworkedField]
    public bool Waiting;

    /// <summary>How often a waiting machine tries to start again (powered time).</summary>
    [DataField]
    public TimeSpan WaitCheckInterval = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public CMUChemSynthesizerStage Stage = CMUChemSynthesizerStage.Idle;

    /// <summary>Locale id of the current fault or block reason, or null.</summary>
    [DataField, AutoNetworkedField]
    public string? Fault;

    /// <summary>Chamber temperature, for the window.</summary>
    [AutoNetworkedField]
    public float ChamberTemperature = 293.15f;

    /// <summary>The temperature the chamber is being driven to, for the window. NaN when none.</summary>
    [AutoNetworkedField]
    public float TargetTemperature = float.NaN;

    [DataField]
    public FixedPoint2 MaxChamber = FixedPoint2.New(120);

    [DataField]
    public int MaxBatches = 60;

    /// <summary>Thermal energy the chamber's thermostat moves per second.</summary>
    [DataField]
    public float EnergyPerSecond = 300f;

    [DataField]
    public TimeSpan StepInterval = TimeSpan.FromSeconds(1);

    /// <summary>Generated chemicals above this tier must be completed by the faction before they can be programmed.</summary>
    [DataField]
    public int MaxGenTierUnresearched = 2;

    [DataField]
    public string ChamberSolution = "chamber";

    [DataField]
    public List<string> InputSlots = new() { "input1", "input2", "input3" };

    [DataField]
    public string OutputSlot = "output";

    [DataField]
    public string WasteSlot = "waste";

    [DataField]
    public string ProgramSlot = "program";

    /// <summary>Vial slot for programming from a sample (ARCH-07). Never touched by inserters.</summary>
    [DataField]
    public string SampleSlot = "sample";

    /// <summary>The sample vial's solution (the XRF scanner reads the same one).</summary>
    [DataField]
    public string SampleSolution = "beaker";

    /// <summary>A sample must hold at least this much of exactly one reagent (XRF scanner validity).</summary>
    [DataField]
    public FixedPoint2 SampleMinVolume = FixedPoint2.New(30);

    /// <summary>
    /// The fluid output port the product leaves through. Its kind is set to <see cref="ProgramProduct"/> by the server
    /// (FLOW-13); with no program it is switched off so it never pumps reactants out.
    /// </summary>
    [DataField]
    public string ProductPort = "product_out";

    /// <summary>Conditioning faults if the chamber has not reached the band after this long (powered time).</summary>
    [DataField]
    public TimeSpan ConditioningTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// M3: a report was inserted after the current program was set, so the next start reprograms from it. Cleared when
    /// any program is set (report, sample or admin), so a report left in its slot never overrides a later sample program.
    /// </summary>
    [DataField]
    public bool ReportPending;

    /// <summary>
    /// M4 (ARCH-04): the chain plan made by the last start, in execution order (post-order); the last step is the
    /// <see cref="Program"/> itself. Empty for a single-step run.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<CMUSynthStep> Plan = new();

    /// <summary>The plan step being run.</summary>
    [DataField, AutoNetworkedField]
    public int PlanIndex;

    /// <summary>The reagent a failed plan could not get (the <c>$reagent</c> of the fault), or null.</summary>
    [DataField, AutoNetworkedField]
    public string? PlanBlockReagent;

    /// <summary>Holds the intermediate products between plan steps (<c>canReact: false</c>).</summary>
    [DataField]
    public string BufferSolution = "buffer";

    /// <summary>Faction clearance that unlocks the chain planner (E6).</summary>
    [DataField]
    public int ChainPlannerClearance = 3;

    /// <summary>Unlocks the chain planner regardless of clearance (admins, tests).</summary>
    [DataField, AutoNetworkedField]
    public bool ChainPlannerUnlocked;

    /// <summary>Player toggle: when on and the inputs lack the program's reactants, a start plans a chain.</summary>
    [DataField, AutoNetworkedField]
    public bool ChainEnabled;

    /// <summary>The longest chain of steps (including the final one) the planner may make along any branch.</summary>
    [DataField]
    public int MaxPlanDepth = 3;

    /// <summary>Seconds accumulated towards the next step (or, while <see cref="Waiting"/>, the next start attempt). Only advances while powered.</summary>
    [ViewVariables]
    public float Accumulator;

    /// <summary>Powered seconds spent in the current Conditioning stage.</summary>
    [ViewVariables]
    public float ConditioningElapsed;
}

[Serializable, NetSerializable]
public enum CMUChemSynthesizerStage : byte
{
    Idle,
    Metering,
    Conditioning,
    Reacting,
    Output,
    Fault,
}

[Serializable, NetSerializable]
public enum CMUChemSynthesizerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CMUChemSynthesizerStartMessage() : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUChemSynthesizerStopMessage() : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUChemSynthesizerSetBatchesMessage(int batches) : BoundUserInterfaceMessage
{
    public readonly int Batches = batches;
}

/// <summary>Programs the machine from the vial in its sample slot (ARCH-07).</summary>
[Serializable, NetSerializable]
public sealed class CMUChemSynthesizerProgramFromSampleMessage() : BoundUserInterfaceMessage;

/// <summary>Turns the chain planner on or off (M4).</summary>
[Serializable, NetSerializable]
public sealed class CMUChemSynthesizerSetChainMessage(bool enabled) : BoundUserInterfaceMessage
{
    public readonly bool Enabled = enabled;
}

/// <summary>CMU14 FLOW-02: turns continuous mode on or off. Turning it off while waiting leaves the machine idle.</summary>
[Serializable, NetSerializable]
public sealed class CMUChemSynthesizerSetContinuousMessage(bool enabled) : BoundUserInterfaceMessage
{
    public readonly bool Enabled = enabled;
}

/// <summary>One step of a chain plan: run <see cref="Reaction"/> for <see cref="Product"/>, <see cref="Batches"/> times.</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class CMUSynthStep
{
    [DataField(required: true)]
    public string Reaction = string.Empty;

    [DataField(required: true)]
    public string Product = string.Empty;

    [DataField]
    public int Batches = 1;

    public CMUSynthStep()
    {
    }

    public CMUSynthStep(string reaction, string product, int batches)
    {
        Reaction = reaction;
        Product = product;
        Batches = batches;
    }
}
