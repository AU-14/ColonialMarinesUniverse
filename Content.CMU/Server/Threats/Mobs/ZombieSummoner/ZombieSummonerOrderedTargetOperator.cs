using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared._CMU14.Threats.Mobs.ZombieSummoner;
using Content.Shared.Damage;
using Content.Shared.Zombies;
using Robust.Shared.Map;

namespace Content.Server._CMU14.Threats.Mobs.ZombieSummoner;

public sealed partial class ZombieSummonerOrderedTargetOperator : HTNOperator
{
    [Dependency] private IEntityManager _entity = default!;

    [DataField]
    public string TargetKey = "Target";

    [DataField]
    public string TargetCoordinatesKey = "TargetCoordinates";

    public override Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>(NPCBlackboard.CurrentOrderedTarget, out var target, _entity) ||
            target == EntityUid.Invalid ||
            _entity.Deleted(target) ||
            !_entity.HasComponent<DamageableComponent>(target) ||
            _entity.HasComponent<ZombieComponent>(target) ||
            _entity.HasComponent<ZombieSummonerComponent>(target) ||
            !_entity.HasComponent<TransformComponent>(target))
        {
            return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((false, null));
        }

        return Task.FromResult<(bool Valid, Dictionary<string, object>? Effects)>((true, new Dictionary<string, object>
        {
            { TargetKey, target },
            { TargetCoordinatesKey, new EntityCoordinates(target, Vector2.Zero) },
        }));
    }
}
