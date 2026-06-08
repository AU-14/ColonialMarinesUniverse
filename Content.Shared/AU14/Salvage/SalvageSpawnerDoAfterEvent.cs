using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.AU14.Salvage;

[Serializable, NetSerializable]
public sealed partial class SalvageSpawnerDoAfterEvent : SimpleDoAfterEvent
{
    public NetEntity User;
}
