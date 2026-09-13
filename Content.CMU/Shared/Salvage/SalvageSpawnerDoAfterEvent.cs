using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Salvage;

[Serializable, NetSerializable]
public sealed partial class SalvageSpawnerDoAfterEvent : SimpleDoAfterEvent;
