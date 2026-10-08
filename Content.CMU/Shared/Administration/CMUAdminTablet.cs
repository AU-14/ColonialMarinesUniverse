using Content.Shared._RMC14.AlertLevel;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Administration;

/// <summary>
/// Marks an admin variant of a command tablet: the comms UI gains an alert level control
/// that can set any <see cref="RMCAlertLevels"/> value, like the alertlevel:set command.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUAdminTabletComponent : Component;

[Serializable, NetSerializable]
public sealed class CMUAdminTabletSetAlertLevelMsg(RMCAlertLevels level) : BoundUserInterfaceMessage
{
    public RMCAlertLevels Level { get; } = level;
}

[Serializable, NetSerializable]
public sealed class CMUAdminTabletToggleFactionMsg : BoundUserInterfaceMessage;
