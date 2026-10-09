using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Expeditions;

[Serializable, NetSerializable]
public sealed partial class CMUExpeditionUtilityDoAfterEvent : SimpleDoAfterEvent;
