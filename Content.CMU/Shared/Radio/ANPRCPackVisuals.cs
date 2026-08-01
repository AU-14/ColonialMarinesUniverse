using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Radio;

// appearance key for the manpack's ground/item sprite
[Serializable, NetSerializable]
public enum ANPRCPackVisuals : byte
{
    State,
}

// which antenna is fitted and whether the pack is staked down as a retrans station
[Serializable, NetSerializable]
public enum ANPRCPackVisualState : byte
{
    Bare,
    Whip,
    Mast,
    DeployedWhip,
    DeployedMast,
}
