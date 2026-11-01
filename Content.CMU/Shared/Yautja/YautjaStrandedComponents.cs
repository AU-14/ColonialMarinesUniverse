using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja;

// cmss13 FACTION_YAUTJA_STRANDED. marks a stranded hunter so the bracer skips the auto-lock and
// minimap setup, the id chip only carries secure access, and overseer broadcasts can tell them apart
[RegisterComponent]
public sealed partial class YautjaStrandedComponent : Component;

// cmss13 /obj/effect/step_trigger/teleporter/yautja_survivor, the one-way pad out of the stranded hut
[RegisterComponent]
public sealed partial class YautjaStrandedTeleporterComponent : Component;

[Serializable, NetSerializable]
public sealed record YautjaStrandedTeleportChosenEvent(NetEntity User, NetEntity Destination);
