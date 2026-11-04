using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Round.Antags.Rider;

/// <summary>
/// Admin/ghost tool: lists every rider and the host they ride, with a jump.
/// </summary>
[RegisterComponent]
public sealed partial class RiderLocatorComponent : Component;

public sealed partial class RiderLocatorActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public enum RiderLocatorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class RiderLocatorEntry
{
    public NetEntity Rider;
    public string RiderName;
    public string HostName;
    public bool Latched;

    public RiderLocatorEntry(NetEntity rider, string riderName, string hostName, bool latched)
    {
        Rider = rider;
        RiderName = riderName;
        HostName = hostName;
        Latched = latched;
    }
}

[Serializable, NetSerializable]
public sealed class RiderLocatorState : BoundUserInterfaceState
{
    public List<RiderLocatorEntry> Riders = new();
}

[Serializable, NetSerializable]
public sealed class RiderLocatorFollowMessage : BoundUserInterfaceMessage
{
    public NetEntity Rider;

    public RiderLocatorFollowMessage(NetEntity rider)
        => Rider = rider;
}
