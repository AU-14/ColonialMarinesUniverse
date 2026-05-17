using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Underground.BurrowerTunnel;

[Serializable, NetSerializable]
public enum BurrowerTunnelUI : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class BurrowerTunnelBuiState(
    BurrowerTunnelType? selected,
    TimeSpan hiveTunnelCooldownRemaining) : BoundUserInterfaceState
{
    public readonly BurrowerTunnelType? Selected = selected;
    public readonly TimeSpan HiveTunnelCooldownRemaining = hiveTunnelCooldownRemaining;
}

[Serializable, NetSerializable]
public sealed class BurrowerTunnelChosenBuiMsg(BurrowerTunnelType choice) : BoundUserInterfaceMessage
{
    public readonly BurrowerTunnelType Choice = choice;
}

public sealed partial class BurrowerChooseTunnelActionEvent : InstantActionEvent;
