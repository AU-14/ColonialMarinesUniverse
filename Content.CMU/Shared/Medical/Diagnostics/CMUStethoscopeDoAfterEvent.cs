using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Medical.Diagnostics;

[Serializable, NetSerializable]
public sealed partial class CMUStethoscopeDoAfterEvent : DoAfterEvent
{
    public readonly ulong Attempt;

    public CMUStethoscopeDoAfterEvent(ulong attempt)
    {
        Attempt = attempt;
    }

    public override DoAfterEvent Clone() => new CMUStethoscopeDoAfterEvent(Attempt);
}
