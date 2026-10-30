using Content.Shared.Chemistry.Reagent;
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
    [DataField(required: true)] public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Reagents = new();
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
public sealed class CMUYautjaExperimentUiState(string status, string clues, string procedure) : BoundUserInterfaceState
{
    public readonly string Status = status;
    public readonly string Clues = clues;
    public readonly string Procedure = procedure;
}
