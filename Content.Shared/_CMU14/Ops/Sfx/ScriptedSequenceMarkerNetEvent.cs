using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Ops.Sfx;

[Serializable, NetSerializable]
public sealed class ScriptedSequenceMarkerNetEvent(
    string sequenceId,
    string marker,
    NetCoordinates? anchorCoords = null,
    ScriptedMarkerData? markerData = null) : EntityEventArgs
{
    public string SequenceId { get; } = sequenceId;
    public string Marker { get; } = marker;
    public NetCoordinates? AnchorCoords { get; } = anchorCoords;
    public ScriptedMarkerData? MarkerData { get; } = markerData;
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class ScriptedMarkerData
{
    [DataField] public float? Frequency { get; set; }
    [DataField] public float? ShakeIntensity { get; set; }
    /// <summary>Camera-shake tail duration for the Explode marker. Null = 1s</summary>
    [DataField] public float? Duration { get; set; }
    /// <summary>Whiteout flash duration for the Explode marker. Null = 0.25s</summary>
    [DataField("flashDuration")] public float? FlashDuration { get; set; }
    [DataField] public Color? Color { get; set; }
}
