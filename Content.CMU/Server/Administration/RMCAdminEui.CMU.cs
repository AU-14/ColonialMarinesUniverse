using System.Linq;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.CMU14.Administration;
using Content.Shared.Eui;
using Content.Shared.Mobs.Systems;
using Content.Shared.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Admin;

public sealed partial class RMCAdminEui
{
    [Dependency] private IPrototypeManager _cmuPrototypes = default!;

    private CMUAdminTargetDetails? CreateCMUDetails()
    {
        if (!_entities.TryGetEntity(_target, out var target) ||
            !_entities.TryGetComponent(target, out MetaDataComponent? metadata))
            return null;

        var skills = _entities.System<SkillsSystem>();
        var levels = new Dictionary<EntProtoId<SkillDefinitionComponent>, int>();
        foreach (var skill in skills.Skills)
        {
            if (!_cmuPrototypes.Index(skill).Abstract)
                levels.Add(skill, skills.GetSkill(target.Value, skill));
        }

        var members = new Dictionary<EntityUid, int>();
        var mobState = _entities.System<MobStateSystem>();
        var memberQuery = _entities.EntityQueryEnumerator<XenoComponent, HiveMemberComponent>();
        while (memberQuery.MoveNext(out var uid, out _, out var member))
        {
            if (member.Hive is { } hive && !mobState.IsDead(uid))
                members[hive] = members.GetValueOrDefault(hive) + 1;
        }

        var hives = new List<CMUAdminHive>();
        var query = _entities.EntityQueryEnumerator<HiveComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var hive, out var meta))
        {
            string? queen = null;
            if (_entities.TryGetComponent(hive.CurrentQueen, out MetaDataComponent? queenMeta))
                queen = queenMeta.EntityName;

            hives.Add(new CMUAdminHive(_entities.GetNetEntity(uid), meta.EntityName, hive.Corrupted,
                members.GetValueOrDefault(uid), hive.BurrowedLarva, queen));
        }

        EntProtoId? currentSquad = null;
        if (_squad.TryGetMemberSquad(target.Value, out var squad) &&
            _entities.TryGetComponent(squad.Owner, out MetaDataComponent? squadMeta))
            currentSquad = squadMeta.EntityPrototype?.ID;

        var currentHive = _hive.GetHive(target.Value);
        return new CMUAdminTargetDetails(_target, metadata.EntityName,
            _entities.HasComponent<XenoComponent>(target),
            currentHive == null ? null : _entities.GetNetEntity(currentHive.Value.Owner),
            currentSquad, levels, hives.OrderBy(hive => hive.Name).ToList());
    }

    /// <summary>
    /// Called only after the EUI's Fun permission check. Resolve the current target on every request,
    /// since transforming replaces it and an open window can outlive the original entity.
    /// </summary>
    private bool HandleCMUMessage(EuiMessageBase msg)
    {
        if (msg is Content.Shared._RMC14.Admin.RMCAdminRefresh)
        {
            StateDirty();
            return true;
        }

        if (msg is not (CMUAdminSetSkillMsg or CMUAdminRemoveSquadMsg or CMUAdminSetHiveMsg or CMUAdminCreateHiveMsg))
            return false;

        if (!_entities.TryGetEntity(_target, out var target) || !_entities.EntityExists(target.Value))
            return true;

        switch (msg)
        {
            case CMUAdminSetSkillMsg skill:
                if (skill.Level < 0 ||
                    !_cmuPrototypes.TryIndex(skill.Skill, out var skillProto) || skillProto.Abstract ||
                    !skillProto.HasComp<SkillDefinitionComponent>(_compFactory))
                    break;

                _entities.System<SkillsSystem>().SetSkill(target.Value, skill.Skill, skill.Level);
                break;
            case CMUAdminRemoveSquadMsg:
                _squad.RemoveSquad(target.Value, null);
                break;
            case CMUAdminSetHiveMsg change:
                if (!_entities.HasComponent<XenoComponent>(target))
                    break;

                EntityUid? hive = null;
                if (change.Hive is { } netHive &&
                    (!_entities.TryGetEntity(netHive, out hive) || !_entities.HasComponent<HiveComponent>(hive)))
                    break;

                _hive.SetHive(target.Value, hive);
                break;
            case CMUAdminCreateHiveMsg create:
                if (!_cmuPrototypes.TryIndex(create.Prototype, out var proto) || proto.Abstract ||
                    !proto.HasComp<HiveComponent>(_compFactory) || create.Name.Length > 64)
                    break;

                var name = create.Name.Trim();
                var created = _hive.CreateHive(string.IsNullOrWhiteSpace(name) ? proto.Name : name, create.Prototype);
                if (create.AssignTarget && _entities.HasComponent<XenoComponent>(target))
                    _hive.SetHive(target.Value, created);
                break;
        }

        StateDirty();
        return true;
    }
}
