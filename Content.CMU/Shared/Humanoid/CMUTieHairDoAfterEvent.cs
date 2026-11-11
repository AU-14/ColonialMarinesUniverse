using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Humanoid;

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

    /// <summary>
    /// Style to untie into when there is no stored original, i.e. hair that spawned tied back.
    /// </summary>
    public string? LooseStyleId;
}
