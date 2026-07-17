using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Humanoid;

[Serializable, NetSerializable]
public sealed partial class CMUTieHairDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;

    public string TiedStyleId = string.Empty;
}

[Serializable, NetSerializable]
public sealed partial class CMUUntieHairDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}
