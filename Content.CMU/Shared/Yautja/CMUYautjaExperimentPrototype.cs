using Content.Shared.CMU14.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Tools.Components;
using Content.Shared.Tools;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja;

[Prototype("cmuYautjaExperiment")]
public sealed partial class CMUYautjaExperimentPrototype : IPrototype
{
    public const float EffectDuration = 8f;
    public const float ReleaseAt = .3f;
    public const float CollapseAt = .84f;
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public LocId Name;
    [DataField(required: true)] public LocId Response;
    [DataField(required: true)] public LocId Clue;
    [DataField(required: true)] public LocId Lore;
    [DataField(required: true)] public ProtoId<ToolQualityPrototype> Tool;
    [DataField(required: true)] public ProtoId<ReagentPropertyPrototype> Property;
    [DataField] public int PropertyLevel = 2;
    [DataField] public int MinimumTier = 3;
    [DataField] public FixedPoint2 SampleAmount = 5;
    [DataField] public TimeSpan Duration = TimeSpan.FromSeconds(8);
    [DataField] public int Heat = 1;
    [DataField] public int Charge;
    [DataField] public int ResearchReward = 5;
    [DataField] public EntProtoId? Output;
    [DataField] public Color Color = Color.Cyan;
    [DataField] public Color CoreColor = Color.FromHex("#EEF8FF");
    [DataField] public float LightEnergy = .7f;
    [DataField(required: true)] public CMUYautjaExperimentMaterial Material;
    [DataField(required: true)] public CMUYautjaExperimentVisual Visual;
    [DataField(required: true)] public SoundSpecifier Sound = default!;
    [DataField(required: true)] public SoundSpecifier ReleaseSound = default!;
    [DataField(required: true)] public SoundSpecifier CollapseSound = default!;
}

// Values are shared with the experiment material shaders.
[Serializable, NetSerializable]
public enum CMUYautjaExperimentMaterial : byte
{
    Plasma, Discharge, Projection, Vapor, Cryogenic, Optical, Magnetic, Thermal,
}

[Serializable, NetSerializable]
public enum CMUYautjaExperimentVisual : byte
{
    Scan, Glyphs, Solvent, Coolant, Bridge, Capacitor, Prism, Veil, Echo, Radar,
    Pulse, Catalyst, Decoy, Tether, Orbit, Plasma, Flash, Static, Containment, Root,
}

[RegisterComponent]
public sealed partial class CMUYautjaExperimentComponent : Component
{
    public const int RequiredCount = 5;
    [DataField] public ProtoId<CMUYautjaExperimentPrototype> Selected = "CMUYautjaDiagnostic";
    [DataField] public HashSet<ProtoId<CMUYautjaExperimentPrototype>> Discoveries = new();
    // Chosen once for this device, not for its current operator. Saved with the bracer.
    [DataField] public HashSet<ProtoId<CMUYautjaExperimentPrototype>> RequiredExperiments = new();
    [DataField] public int Heat;
    [DataField] public bool Unlocked;
    // Serializes access to the shared sample well; only this operation may consume its reagents.
    public EntityUid? Operator;
    public DoAfterId? Pending;
}

/// <summary>The researcher learned the bracer's stolen interface without becoming a Yautja.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUYautjaResearchOperatorComponent : Component;

[Serializable, NetSerializable]
public sealed partial class CMUYautjaExperimentDoAfterEvent : DoAfterEvent
{
    [DataField] public ProtoId<CMUYautjaExperimentPrototype> Experiment;
    public override DoAfterEvent Clone() => (CMUYautjaExperimentDoAfterEvent) MemberwiseClone();
}

[Serializable, NetSerializable]
public enum CMUYautjaExperimentUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CMUYautjaExperimentUiState(
    ProtoId<CMUYautjaExperimentPrototype> selected,
    List<CMUYautjaExperimentSeal> seals,
    HashSet<ProtoId<CMUYautjaExperimentPrototype>> discoveries,
    int heat, bool unlocked, bool busy, string samples) : BoundUserInterfaceState
{
    public readonly ProtoId<CMUYautjaExperimentPrototype> Selected = selected;
    public readonly List<CMUYautjaExperimentSeal> Seals = seals;
    public readonly HashSet<ProtoId<CMUYautjaExperimentPrototype>> Discoveries = discoveries;
    public readonly int Heat = heat;
    public readonly bool Unlocked = unlocked;
    public readonly bool Busy = busy;
    public readonly string Samples = samples;
}

// Unsolved seals send their clue, never the hidden recipe ID.
[Serializable, NetSerializable]
public sealed record CMUYautjaExperimentSeal(string Clue, string? RecoveredName);

[Serializable, NetSerializable]
public enum CMUYautjaExperimentCommand : byte
{
    Select, Purge, Authorize,
}

[Serializable, NetSerializable]
public sealed class CMUYautjaExperimentMessage(CMUYautjaExperimentCommand command,
    ProtoId<CMUYautjaExperimentPrototype>? recipe = null) : BoundUserInterfaceMessage
{
    public readonly CMUYautjaExperimentCommand Command = command;
    public readonly ProtoId<CMUYautjaExperimentPrototype>? Recipe = recipe;
}
