using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Fob;

/// <summary>Presentation configuration. Appearance carries persistent status; cues are PVS-local one-shots.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUFobPresentationComponent : Component
{
    [DataField] public string BodyLayer = "fob-body";
    [DataField] public string EffectLayer = "fob-effect";
    [DataField] public CMUFobDeviceAnimation Device;
    [DataField] public float TurnSpeed = 180;
    [ViewVariables] public int ShotSequence;
    [DataField] public SoundSpecifier? PowerOnSound;
    [DataField] public SoundSpecifier? PowerOffSound;
}

[Serializable, NetSerializable]
public enum CMUFobPresentationVisuals : byte { Status, StatusSince, Aim, Open, LockDuration }

public enum CMUFobDeviceAnimation : byte { Relay, Terminal, Platform, HeavyGun, Rocket, Light, Sensor, Fence, Generator, Crate, Target }

[Serializable, NetSerializable]
public enum CMUFobPresentationStatus : byte
{
    Off,
    Ready,
    Starting,
    Active,
    Working,
    Warning,
    Empty,
    Broken,
    Spent,
}

[Serializable, NetSerializable]
public enum CMUFobPresentationCue : byte { Deploy, Pack, Repair, Charge, Shock, Shot, Reload, Break }

/// <summary>Never used as gameplay authority, and never replayed to late observers.</summary>
[Serializable, NetSerializable]
public sealed class CMUFobPresentationEvent(NetEntity entity, CMUFobPresentationCue cue, int sequence) : EntityEventArgs
{
    public readonly NetEntity Entity = entity;
    public readonly CMUFobPresentationCue Cue = cue;
    public readonly int Sequence = sequence;
}
