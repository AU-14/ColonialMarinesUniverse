using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Weapons.Ranged.IFF;

[Serializable, NetSerializable]
public sealed partial class CMUIFFRemovalDoAfterEvent : SimpleDoAfterEvent
{
    public readonly bool Trained;

    public CMUIFFRemovalDoAfterEvent(bool trained)
    {
        Trained = trained;
    }

    public override DoAfterEvent Clone() => new CMUIFFRemovalDoAfterEvent(Trained);
}
