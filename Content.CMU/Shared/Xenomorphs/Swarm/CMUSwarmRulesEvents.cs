using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Xenomorphs.Swarm;

[Serializable, NetSerializable]
public sealed record CMUSwarmSetPassiveEvent(NetEntity Hive, bool Passive);

[Serializable, NetSerializable]
public sealed record CMUSwarmSetAllyEvent(NetEntity Hive, ProtoId<NpcFactionPrototype> Faction, bool Allied);
