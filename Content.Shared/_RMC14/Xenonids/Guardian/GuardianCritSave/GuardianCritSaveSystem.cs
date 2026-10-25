using Content.Shared._RMC14.Xenonids.Guardian.GuardianAbsorb;  
using Content.Shared._RMC14.Xenonids.Hive;  
using Content.Shared.Damage;  
using Content.Shared.Mobs;  
using Content.Shared.Mobs.Components;  
using Content.Shared.Mobs.Systems;  
using Robust.Shared.Network;  
using Robust.Shared.Timing;  
  
namespace Content.Shared._RMC14.Xenonids.Guardian.GuardianCritSave;  
  
public sealed partial class GuardianCritSaveSystem : EntitySystem  
{  
    [Dependency] private readonly EntityLookupSystem _lookup = default!;  
    [Dependency] private readonly MobStateSystem _mobState = default!;  
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;  
    [Dependency] private readonly GuardianAbsorbSystem _guardianAbsorb = default!;  
    [Dependency] private readonly IGameTiming _timing = default!;  
    [Dependency] private readonly INetManager _net = default!;  
  
    private readonly HashSet<Entity<MobStateComponent>> _nearby = new();  
    private readonly HashSet<EntityUid> _validTargets = new();  
  
    public override void Update(float frameTime)  
    {  
        if (_net.IsClient)  
            return;  
  
        var time = _timing.CurTime;  
        var query = EntityQueryEnumerator<GuardianCritSaveComponent, DamageableComponent>();  
        while (query.MoveNext(out var uid, out var comp, out var damageable))  
        {  
            if (time < comp.NextCheck)  
                continue;  
  
            comp.NextCheck = time + comp.CheckEvery;  
  
            // Self-preservation gate: ability simply doesn't function below the threshold.  
            var currentHealth = damageable.TotalDamage;  
            if (currentHealth >= comp.MinimumHealth)  
            {  
                ClearAllTracked(uid, comp);  
                continue;  
            }  
  
            _validTargets.Clear();  
            _nearby.Clear();  
            _lookup.GetEntitiesInRange(Transform(uid).Coordinates, comp.Range, _nearby);  
  
            foreach (var target in _nearby)  
            {  
                if (target.Owner == uid)  
                    continue;  
  
                if (!_hive.FromSameHive(uid, target.Owner))  
                    continue;  
  
                if (!_mobState.IsCritical(target.Owner, target.Comp))  
                    continue;  
  
                _validTargets.Add(target.Owner);  
  
                if (!comp.ActiveTargets.Contains(target.Owner))  
                {  
                    comp.ActiveTargets.Add(target.Owner);  
                    _guardianAbsorb.SetAbsorb(target.Owner, uid, GuardianAbsorbType.PassiveCritSave,  
                        percent: 1f, ignoreResistances: false);  
                }  
            }  
  
            // Drop anything that's no longer in range/crit/same-hive.  
            comp.ActiveTargets.RemoveWhere(tracked =>  
            {  
                if (_validTargets.Contains(tracked))  
                    return false;  
  
                _guardianAbsorb.RemoveAbsorb(tracked, GuardianAbsorbType.PassiveCritSave);  
                return true;  
            });  
        }  
    }  
  
    private void ClearAllTargets(EntityUid uid, GuardianCritSaveComponent comp)  
    {  
        foreach (var tracked in comp.ActiveTargets)  
        {  
            _guardianAbsorb.RemoveAbsorb(tracked, GuardianAbsorbType.PassiveCritSave);  
        }  
  
        comp.ActiveTargets.Clear();  
    }  
}
