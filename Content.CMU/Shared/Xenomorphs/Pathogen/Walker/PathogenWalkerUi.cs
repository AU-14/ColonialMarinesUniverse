using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Walker;

[Serializable, NetSerializable]
public sealed class CMUPathogenWalkerOfferEvent : EntityEventArgs
{
    public NetEntity Target;
    public double TimeoutSeconds;
    public CMUPathogenWalkerOfferEvent(NetEntity target, double timeout)
    {
        Target = target;
        TimeoutSeconds = timeout;
    }
}

[Serializable, NetSerializable]
public sealed class CMUPathogenWalkerAcceptNetEvent : EntityEventArgs
{
    public NetEntity Target;
    public CMUPathogenWalkerAcceptNetEvent(NetEntity target) => Target = target;
}

[Serializable, NetSerializable]
public sealed class CMUPathogenWalkerDeclineNetEvent : EntityEventArgs
{
    public NetEntity Target;
    public CMUPathogenWalkerDeclineNetEvent(NetEntity target) => Target = target;
}