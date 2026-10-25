using Content.Shared._RMC14.Xenonids.Guardian.GuardianAbsorb;  
using Content.Shared._RMC14.Xenonids.Hive;  
using Content.Shared.Damage.Components;  
using Content.Shared.FixedPoint;  
using Content.Shared.Mobs.Components;  
using Content.Shared.Mobs.Systems;  
using Robust.Shared.Timing;  
  
namespace Content.Shared._RMC14.Xenonids.Guardian.GuardianCritSave;  
  
public sealed partial class GuardianCritSaveSystem : EntitySystem  
{  
    [Dependency] private readonly EntityLookupSystem _lookup = default!;  
    [Dependency] private readonly GuardianAbsorbSystem _guardianAbsorb = default!;  
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;  
    [Dependency] private readonly MobStateSystem _mobState = default!;  
    [Dependency] private readonly MobThresholdSystem _mobThresholds = default!;  
    [Dependency] private readonly IGameTiming _timing = default!;  
  
    private readonly HashSet<Entity<MobStateComponent>> _nearby = new();  
  
    public override void Update(float frameTime)  
    {  
        var time = _timing.CurTime;  
        var query = EntityQueryEnumerator<GuardianCritSaveComponent>();  
        while (query.MoveNext(out var uid, out var save))  
        {  
            if (time < save.NextCheck)  
                continue;  
  
            save.NextCheck = time + save.CheckEvery;  
  
            
            if (!_mobThresholds.TryGetIncapThreshold(uid, out var incapThreshold) ||  
                !TryComp<DamageableComponent>(uid, out var selfDamageable))  
            {  
                continue;  
            }  
  
            var remaining = incapThreshold.Value - selfDamageable.TotalDamage;  
            if (remaining <= save.MinimumHealth)  
            {  
                foreach (var old in save.ActiveTargets)  
                    _guardianAbsorb.RemoveAbsorb(old, GuardianAbsorbType.PassiveCritSave);  
  
                save.ActiveTargets.Clear();  
                continue;  
            }  
  
            _nearby.Clear();  
            _lookup.GetEntitiesInRange(Transform(uid).Coordinates, save.Range, _nearby);  
  
            var stillActive = new HashSet<EntityUid>();  
            foreach (var nearby in _nearby)  
            {  
                if (nearby.Owner == uid)  
                    continue;  
  
                if (!HasComp<XenoComponent>(nearby.Owner))  
                    continue;  
  
                if (!_hive.FromSameHive(uid, nearby.Owner))  
                    continue;  
  
                if (!_mobState.IsCritical(nearby.Owner, nearby.Comp))  
                    continue;  
  
                _guardianAbsorb.SetAbsorb(nearby.Owner, uid, GuardianAbsorbType.PassiveCritSave, save.AbsorbPercent);  
                stillActive.Add(nearby.Owner);  
            }  
  
            foreach (var old in save.ActiveTargets)  
            {  
                if (!stillActive.Contains(old))  
                    _guardianAbsorb.RemoveAbsorb(old, GuardianAbsorbType.PassiveCritSave);  
            }  
  
            save.ActiveTargets.Clear();  
            foreach (var active in stillActive)  
                save.ActiveTargets.Add(active);  
        }  
    }  
}
