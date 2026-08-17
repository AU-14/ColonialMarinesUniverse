using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Walker;

[Serializable, NetSerializable]
public sealed class CMUPathogenWalkerReviveWarningEvent : EntityEventArgs
{
    public NetEntity Target;
    public double Seconds;
    public CMUPathogenWalkerReviveWarningEvent(NetEntity target, double seconds)
    {
        Target = target;
        Seconds = seconds;
    }
}