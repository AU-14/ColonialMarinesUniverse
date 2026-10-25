using Content.Shared._RMC14.Xenonids;  
using Content.Shared._RMC14.Xenonids.Guardian.GuardianAbsorb;  
using Content.Shared._RMC14.Xenonids.Hive;  
using Content.Shared.Damage.Components;  
using Content.Shared.Damage.Systems;  
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
    [Dependency] private readonly IGameTiming _timing = default!;  
  
    public override void Initialize()  
    {  
        base.Initialize();  
  
        SubscribeLocalEvent<GuardianCritSaveComponent, DamageChangedEvent>(OnGuardianDamageChanged);  
    }  
  
    private void OnGuardianDamageChanged(Entity<GuardianCritSaveComponent> ent, ref DamageChangedEvent args)  
    {  
        if (args.DamageDelta == null)  
            return;  
  
        ent.Comp.CachedDamage += args.DamageDelta.GetTotal();  
        Dirty(ent);  
    }  
  
    public override void Update(float frameTime)  
    {  
        base.Update(frameTime);  
  
        var time = _timing.CurTime;  
        var query = EntityQueryEnumerator<GuardianCritSaveComponent>();  
        while (query.MoveNext(out var uid, out var save))  
        {  
            if (time < save.NextCheck)  
                continue;  
  
            save.NextCheck = time + save.CheckEvery;  
  
            var remaining = save.MinimumHealth - save.CachedDamage;  
            if (remaining <= FixedPoint2.Zero)  
            {  
                foreach (var old in save.ActiveTargets)  
                    _guardianAbsorb.RemoveAbsorb(old, GuardianAbsorbType.PassiveCritSave);  
  
                save.ActiveTargets.Clear();  
                continue;  
            }  
  
            var nearby = new HashSet<Entity<MobStateComponent>>();  
            _lookup.GetEntitiesInRange(uid, save.Range, nearby, LookupFlags.Uncontained);  
  
            var stillActive = new HashSet<EntityUid>();  
            foreach (var target in nearby)  
            {  
                if (!HasComp<XenoComponent>(target.Owner))  
                    continue;  
  
                if (!_hive.FromSameHive(uid, target.Owner))  
                    continue;  
  
                if (!_mobState.IsCritical(target.Owner, target.Comp))  
                    continue;  
  
                _guardianAbsorb.SetAbsorb(target.Owner, uid, GuardianAbsorbType.PassiveCritSave, save.AbsorbPercent);  
                stillActive.Add(target.Owner);  
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
