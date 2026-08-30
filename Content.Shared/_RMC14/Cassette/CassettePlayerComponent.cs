using Content.Shared.Inventory;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Cassette;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCassetteSystem))]
public sealed partial class CassettePlayerComponent : Component
{
    [DataField]
    public EntProtoId PlayPauseActionId = "RMCActionCassettePlayPause";

    [DataField]
    public EntityUid? PlayPauseAction;

    [DataField]
    public EntProtoId NextActionId = "RMCActionCassetteNext";

    [DataField]
    public EntityUid? NextAction;

    [DataField]
    public EntProtoId RestartActionId = "RMCActionCassetteRestart";

    [DataField]
    public EntityUid? RestartAction;

    [DataField]
    public SlotFlags Slots = SlotFlags.EARS;

    [DataField]
    public string ContainerId = "rmc_cassette_player";

    [DataField]
    public EntityUid? AudioStream;

    [DataField]
    public EntityUid? CustomAudioStream;

    [DataField]
    public AudioState State;

    [DataField]
    public AudioParams AudioParams = AudioParams.Default.WithVolume(-6f);

    [DataField]
    public int Tape;

    [DataField]
    public SoundSpecifier PlayPauseSound = new SoundPathSpecifier("/Audio/_RMC14/Machines/click.ogg");

    [DataField]
    public SoundSpecifier InsertEjectSound = new SoundPathSpecifier("/Audio/_RMC14/Weapons/handcuffs.ogg");

    [DataField]
    public SpriteSpecifier.Rsi WornSprite = new(new ResPath("_RMC14/Objects/Devices/cassette_player.rsi"), "mob_overlay");

    [DataField]
    public SpriteSpecifier.Rsi MusicSprite = new(new ResPath("_RMC14/Objects/Devices/cassette_player.rsi"), "music");
}

[Serializable, NetSerializable]
public sealed class CassettePlayerComponentState : ComponentState
{
    public EntProtoId PlayPauseActionId { get; init; }
    public NetEntity? PlayPauseAction { get; init; }
    public EntProtoId NextActionId { get; init; }
    public NetEntity? NextAction { get; init; }
    public EntProtoId RestartActionId { get; init; }
    public NetEntity? RestartAction { get; init; }
    public SlotFlags Slots { get; init; }
    public string ContainerId { get; init; } = string.Empty;
    public NetEntity? AudioStream { get; init; }
    public AudioState State { get; init; }
    public AudioParams AudioParams { get; init; }
    public int Tape { get; init; }
    public SoundSpecifier PlayPauseSound { get; init; } = default!;
    public SoundSpecifier InsertEjectSound { get; init; } = default!;
    public SpriteSpecifier.Rsi WornSprite { get; init; } = default!;
    public SpriteSpecifier.Rsi MusicSprite { get; init; } = default!;
}
