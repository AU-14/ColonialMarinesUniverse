/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Xeno.ManageHive;

[Serializable, NetSerializable]
public sealed record HiveSetAllyStatusIndividualEvent(NetEntity Ent, bool State);

[Serializable, NetSerializable]
public sealed record HiveSetAllyStatusEvent(NetEntity Ent, bool State);

[Serializable, NetSerializable]
public sealed record ManageHiveBreakPersonalAllyEvent(NetEntity Hive);

[Serializable, NetSerializable]
public sealed record ManageHiveMakePersonalAllyEvent(NetEntity Hive);

[Serializable, NetSerializable]
public sealed record ManageHiveMakeFactionAllyEvent(NetEntity Hive);

[Serializable, NetSerializable]
public sealed record HiveSetFactionAllyStatusEvent(ProtoId<NpcFactionPrototype> Fac, string FacName, bool State);

[Serializable, NetSerializable]
public sealed record ManageHiveBreakAllPersonalAlliancesEvent(NetEntity Hive);

[Serializable, NetSerializable]
public sealed record ConfirmBreakAllAlliancesEvent(NetEntity Hive);

[Serializable, NetSerializable]
public sealed record ManageHiveBreakFactionAllyEvent(NetEntity Hive);
