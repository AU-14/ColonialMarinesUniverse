using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.K9.Events;

public sealed partial class K9ArmGrabActionEvent : EntityTargetActionEvent;

public sealed partial class K9TrackMasterActionEvent : InstantActionEvent;

public sealed partial class K9RequestMasterActionEvent : EntityTargetActionEvent;

public sealed partial class K9TameActionEvent : EntityTargetActionEvent;

public sealed partial class K9SicEmActionEvent : EntityTargetActionEvent;

public sealed partial class K9EvacuateActionEvent : EntityTargetActionEvent;

public sealed partial class K9GoodBoyActionEvent : EntityTargetActionEvent;

/// <summary>
/// Confirmation event raised when a dog agrees to be tamed by a handler.
/// </summary>
[Serializable, NetSerializable]
public sealed class K9TameConfirmEvent : EntityEventArgs
{
    public NetEntity HandlerNet;
    public NetEntity DogNet;

    public K9TameConfirmEvent(NetEntity handlerNet, NetEntity dogNet)
    {
        HandlerNet = handlerNet;
        DogNet = dogNet;
    }
}

/// <summary>
/// Confirmation event raised when a marine agrees to become a dog's master upon request.
/// </summary>
[Serializable, NetSerializable]
public sealed class K9MasterRequestConfirmEvent : EntityEventArgs
{
    public NetEntity DogNet;
    public NetEntity MarineNet;

    public K9MasterRequestConfirmEvent(NetEntity dogNet, NetEntity marineNet)
    {
        DogNet = dogNet;
        MarineNet = marineNet;
    }
}
