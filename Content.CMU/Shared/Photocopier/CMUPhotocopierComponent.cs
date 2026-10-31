using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Photocopier;

[RegisterComponent]
public sealed partial class CMUPhotocopierComponent : Component
{
    /// <summary>
    /// Toner units left, one page costs one unit.
    /// </summary>
    [DataField]
    public int Toner = 45;

    [DataField]
    public int TonerMax = 45;

    [DataField]
    public TimeSpan CopyDelay = TimeSpan.FromSeconds(2);
}

[Serializable, NetSerializable]
public sealed partial class CMUPhotocopierCopyDoAfterEvent : SimpleDoAfterEvent;
