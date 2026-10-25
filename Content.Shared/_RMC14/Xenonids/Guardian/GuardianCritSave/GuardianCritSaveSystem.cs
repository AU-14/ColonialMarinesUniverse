using Content.Shared._RMC14.Xenonids.Guardian.GuardianAbsorb;  
using Content.Shared._RMC14.Xenonids.Hive;  
using Content.Shared.Mobs;  
using Content.Shared.Mobs.Systems;  
using Robust.Shared.Timing;  
  
namespace Content.Shared._RMC14.Xenonids.Guardian.GuardianCritSave;  
  
public sealed partial class GuardianCritSaveSystem : EntitySystem  
{  
    [Dependency] private readonly GuardianAbsorbSystem _guardianAbsorb = default!;  
    [Dependency] private readonly MobStateSystem _mobState = default!;  
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;  
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;  
    [Dependency] private readonly IGameTiming _timing = default!;  
  
    public override void Update(float frameTime)  
    {  
        var time = _timing.CurTime;  
        var query = EntityQueryEnumerator<GuardianCritSaveComponent>();  
        while (query.MoveNext(out var uid, out var guardian))  
        {  
            if (guardian.NextCheck > time)  
                continue;  
  
            guardian.NextCheck = time + guardian.CheckEvery;  
  
             
            if (_mobThreshold.TryGetIncapPercentage(uid, GetGuardianDamage(uid), out var incapPercent) &&  
                incapPercent > guardian.MaxSelfDamagePercent)  
            {  
                ClearAllTargets(uid, guardian);  
                continue;  
            }  
  
            ScanForCritAllies(uid, guardian);  
        }  
    }  
  
    private void ScanForCritAllies(EntityUid uid, GuardianCritSaveComponent guardian)  
    {  
        var inRange = new HashSet<EntityUid>();  
  
        foreach (var ally in _hive.GetNearbyMembers(uid, guardian.Range)) 
        {  
            if (ally == uid)  
                continue;  
  
            if (!_mobState.IsCritical(ally))  
                continue;  
  
            inRange.Add(ally);  
  
            if (!guardian.ActiveTargets.Contains(ally))  
            {  
                _guardianAbsorb.SetAbsorb(ally, uid, GuardianAbsorbType.PassiveCritSave, percent: 1f, ignoreResistances: false);  
                guardian.ActiveTargets.Add(ally);  
            }  
        }  
  
       
        foreach (var target in new List<EntityUid>(guardian.ActiveTargets))  
        {  
            if (inRange.Contains(target))  
                continue;  
  
            _guardianAbsorb.RemoveAbsorb(target, GuardianAbsorbType.PassiveCritSave);  
            guardian.ActiveTargets.Remove(target);  
        }  
    }  
  
    private void ClearAllTargets(EntityUid uid, GuardianCritSaveComponent guardian)  
    {  
        foreach (var target in guardian.ActiveTargets)  
            _guardianAbsorb.RemoveAbsorb(target, GuardianAbsorbType.PassiveCritSave);  
  
        guardian.ActiveTargets.Clear();  
    }  
}
