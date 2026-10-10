using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Power;

[Serializable, NetSerializable]
public sealed partial class CMUApcCellRemoveDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUApcCellInsertDoAfterEvent : SimpleDoAfterEvent;
