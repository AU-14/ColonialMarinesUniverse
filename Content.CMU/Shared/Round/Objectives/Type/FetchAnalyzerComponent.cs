using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.CMU14.Round.Objectives.Components;

namespace Content.Shared.CMU14.Round.Objectives.Type;

/// <summary>The Analyzer Machine: scans for fetch objective/items and converts inserted submissions</summary>
[RegisterComponent]
public sealed partial class FetchAnalyzerComponent : Robust.Shared.GameObjects.Component
{
    /// <summary>Who this belongs to, only matching fetch faction (or neutral) will be seen by Scan. Leave empty to match with all</summary>
    [DataField] public string Faction { get; set; } = string.Empty;

    public int CashStored;
    [DataField] public bool IncludeDollars = true;
    public Dictionary<string, int> Banked = new();

    [DataField] public List<AnalyzerConversionEntry> Conversions = new();

    /// <summary>Scanning RMC intel items credits this faction's intel points, multiplied by this.</summary>
    [DataField] public float IntelPointMultiplier = 1f;

    /// <summary>How long the working animation plays after the Analyzer processes something.</summary>
    [DataField] public TimeSpan WorkingDuration = TimeSpan.FromSeconds(3.2);

    public TimeSpan? WorkingUntil;
}

[Serializable, NetSerializable]
public enum FetchAnalyzerVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum FetchAnalyzerVisualState : byte
{
    Idle,
    Working,
    Off,
    Broken,
}

[DataDefinition]
public sealed partial class AnalyzerConversionEntry
{
    [DataField]
    public EntProtoId Entity;

    [DataField]
    public bool PointsPerItemMode;

    [DataField]
    public int AmountPerPoint = 15;

    [DataField]
    public int PointsPerItem = 1;
}
