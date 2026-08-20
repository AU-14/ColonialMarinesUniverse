using Content.Shared.Chat;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Ops.Sfx;

[Prototype]
public sealed partial class ScriptedSoundSequencePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = string.Empty;
    [DataField] public string? DefaultSender;
    [DataField] public Color? DefaultAnnouncementColor;
    [DataField] public string? DefaultAnnouncementPreset;
    [DataField(required: true)] public List<ScriptedSoundEntry> Entries = new();
}

[DataDefinition]
public sealed partial class ScriptedAnnouncement
{
    [DataField(required: true)] public string Message = string.Empty;
    [DataField] public string? Sender;
    [DataField] public Color? Color;
    [DataField] public ChatChannel Channel = ChatChannel.Radio;
    [DataField] public string? Preset;
}

[DataDefinition]
public sealed partial class ScriptedSoundEntry
{
    [DataField] public string? Layer;
    [DataField] public SoundSpecifier? Sound;
    /// <summary>Play this shipwide (bypass PVS), instead of local on the ent anchor</summary>
    [DataField] public bool GlobalAudio = true;
    [DataField] public AudioParams? AudioParams;
    [DataField] public ScriptedAnnouncement? Announcement;

    /// <summary>Seconds from sequence start before firing</summary>
    [DataField("delay")] public float DelaySeconds;

    /// <summary>Stops a list of named layers, leaves others running</summary>
    [DataField("stopLoop")] public List<string>? StopLoops;
    [DataField] public bool StopAllLoops;
    [DataField] public bool Loop;
    /// <summary>Auto-kill a loop after X seconds. Null = forever</summary>
    [DataField("duration")] public float? DurationSeconds;
    /// <summary>Repeat this entry every N seconds instead of once. delayJitter randomizes each interval</summary>
    [DataField("repeat")] public float? RepeatSeconds;

    /// <summary>Random-ish +/- dB added to volume on every fire. Random pitch via audioParams</summary>
    [DataField("volumeJitter")] public float? VolumeJitter;
    /// <summary>Random-ish +/- seconds added to delay once per sequence, and to each repeat interval</summary>
    [DataField("delayJitter")] public float? DelayJitterSeconds;
    /// <summary>If true, delay offset is applied to the previous timed entry</summary>
    [DataField] public bool JitterInterval;

    [DataField] public string? Marker;
    [DataField] public ScriptedMarkerData? MarkerData;
}
