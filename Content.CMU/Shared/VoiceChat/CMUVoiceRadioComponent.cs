using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.VoiceChat;

/// <summary>
/// The headset channel a player picked for voice radio, from the headset window or the cycle key.
/// Ignored, and the headset default used instead, once the headset can no longer transmit on it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class CMUVoiceRadioComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [DataField, AutoNetworkedField]
    public ProtoId<RadioChannelPrototype>? Channel;
}

/// <summary>
/// Asks the server to talk on <see cref="Channel"/> with voice radio, from the headset window.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUVoiceRadioSelectEvent(ProtoId<RadioChannelPrototype> channel) : EntityEventArgs
{
    public readonly ProtoId<RadioChannelPrototype> Channel = channel;
}
