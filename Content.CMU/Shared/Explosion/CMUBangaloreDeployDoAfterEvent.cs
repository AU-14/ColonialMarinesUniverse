using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Explosion;

[Serializable, NetSerializable]
public sealed partial class CMUBangaloreDeployDoAfterEvent : SimpleDoAfterEvent;
