using Content.Shared._RMC14.Dropship;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Dropship;

/// <summary>
///     Wall-mounted ship fire alarm. Pulling it sets the craft's
///     <see cref="DropshipComponent.FireAlarmActive"/>, freezing life support
///     vents until the alarm is reset by hand. Runs without power or a device
///     network, so it works on unpowered decks.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUShipFireAlarmComponent : Component
{
    /// <summary>Whether the alarm on this craft is currently pulled.</summary>
    [DataField, AutoNetworkedField]
    public bool Active;
}

[Serializable, NetSerializable]
public enum CMUShipFireAlarmVisuals : byte
{
    Active,
}
