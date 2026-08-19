using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Ops.Sfx;

[Serializable, NetSerializable]
public sealed class PlayScriptedSoundNetEvent(
    int handle,
    ResolvedSoundSpecifier sound,
    AudioParams audioParams,
    bool global,
    string? layer,
    float? durationSeconds,
    NetCoordinates? anchorCoords) : EntityEventArgs
{
    public int Handle { get; } = handle;
    public ResolvedSoundSpecifier Sound { get; } = sound;
    public AudioParams Params { get; } = audioParams;
    public bool Global { get; } = global;
    public string? Layer { get; } = layer;
    public float? DurationSeconds { get; } = durationSeconds;
    public NetCoordinates? AnchorCoords { get; } = anchorCoords;
}

/// <summary>Sent by the client when it (re-)gains a body, changes map, unmutes, or to receive currently playing loop layers.</summary>
[Serializable, NetSerializable]
public sealed class RequestScriptedSoundResyncNetEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class StopScriptedSoundLayersNetEvent(
    int handle,
    string[]? layers) : EntityEventArgs
{
    public int Handle { get; } = handle;

    /// <summary>Null stops every layer of the sequence.</summary>
    public string[]? Layers { get; } = layers;
}
