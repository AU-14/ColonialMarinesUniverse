using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Eui;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Administration;

[Serializable, NetSerializable]
public sealed record CMUAdminTargetDetails(
    NetEntity Target,
    string Name,
    bool IsXeno,
    NetEntity? CurrentHive,
    EntProtoId? CurrentSquad,
    Dictionary<EntProtoId<SkillDefinitionComponent>, int> Skills,
    List<CMUAdminHive> Hives);

[Serializable, NetSerializable]
public readonly record struct CMUAdminHive(
    NetEntity Id,
    string Name,
    bool Corrupted,
    int Members,
    int Larva,
    string? Queen);

[Serializable, NetSerializable]
public sealed class CMUAdminSetSkillMsg(EntProtoId<SkillDefinitionComponent> skill, int level) : EuiMessageBase
{
    public readonly EntProtoId<SkillDefinitionComponent> Skill = skill;
    public readonly int Level = level;
}

[Serializable, NetSerializable]
public sealed class CMUAdminRemoveSquadMsg : EuiMessageBase;

[Serializable, NetSerializable]
public sealed class CMUAdminSetHiveMsg(NetEntity? hive) : EuiMessageBase
{
    public readonly NetEntity? Hive = hive;
}

[Serializable, NetSerializable]
public sealed class CMUAdminCreateHiveMsg(EntProtoId prototype, string name, bool assignTarget) : EuiMessageBase
{
    public readonly EntProtoId Prototype = prototype;
    public readonly string Name = name;
    public readonly bool AssignTarget = assignTarget;
}
