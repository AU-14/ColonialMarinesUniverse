using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.AU14.Radio;

/// <summary>
///     When this uniform accessory is attached to a worn uniform, it grants the wearer
///     intrinsic radio transmit/receive capability on the specified channels.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AccessoryHeadsetComponent : Component
{
    /// <summary>
    ///     The radio channels this earpiece provides.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public HashSet<ProtoId<RadioChannelPrototype>> Channels = new();

    /// <summary>
    ///     The default channel used when the wearer uses the :h radio prefix.
    ///     If null, the first channel in <see cref="Channels"/> is used.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<RadioChannelPrototype>? DefaultChannel;

    /// <summary>
    ///     Tracks which channels were added to ActiveRadioComponent (for clean removal).
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<RadioChannelPrototype>> ActiveAddedChannels = new();

    /// <summary>
    ///     Tracks which channels were added to IntrinsicRadioTransmitterComponent (for clean removal).
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<RadioChannelPrototype>> TransmitterAddedChannels = new();

    /// <summary>
    ///     The entity that currently has radio granted by this accessory.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? RadioGrantedTo;
}

