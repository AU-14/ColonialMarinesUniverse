using Content.Shared._RMC14.Xenonids;  
using Content.Shared._RMC14.Xenonids.Guardian.GuardianAbsorb;  
using Content.Shared._RMC14.Xenonids.Hive;  
using Content.Shared.Damage.Components;  
using Content.Shared.Damage.Systems;  
using Content.Shared.FixedPoint;  
using Content.Shared.Mobs.Components;  
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Timing;
using Robust.Shared.Network;
  
namespace Content.Shared._RMC14.Xenonids.Guardian.GuardianCritSave;  
  
public sealed partial class GuardianCritSaveSystem : EntitySystem  
{  
    [Dependency] private readonly EntityLookupSystem _lookup = default!;  
    [Dependency] private readonly GuardianAbsorbSystem _guardianAbsorb = default!;  
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;  
    [Dependency] private readonly MobStateSystem _mobState = default!;  
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;  
    [Dependency] private readonly IGameTiming _timing = default!;  
    [Dependency] private readonly INetManager _net = default!;  
    [Dependency] private readonly SharedPopupSystem _popup = default!;  
  
    private readonly HashSet<Entity<MobStateComponent>> _nearby = new();  
  
    public override void Update(float frameTime)  
    {  
        if (_net.IsClient)  
            return;  
  
        var time = _timing.CurTime;  
        var query = EntityQueryEnumerator<GuardianCritSaveComponent>();  
        while (query.MoveNext(out var uid, out var save))  
        {  
            if (time < save.NextCheck)  
                continue;  
  
            save.NextCheck = time + save.CheckEvery;  
  
            if (!_mobThreshold.TryGetIncapThreshold(uid, out var incapThreshold))  
                continue;  
  
            // Live damage read instead of a cached accumulator.  
            var guardianDamage = FixedPoint2.Zero;  
            if (TryComp<DamageableComponent>(uid, out var damageable))  
                guardianDamage = damageable.TotalDamage;  
  
            var remaining = incapThreshold.Value - guardianDamage;  
            if (remaining <= save.MinimumHealth)  
            {  
                foreach (var old in save.ActiveTargets)  
                {  
                    _guardianAbsorb.RemoveAbsorb(old, GuardianAbsorbType.PassiveCritSave);  
                    _popup.PopupEntity(  
                        Loc.GetString("rmc-xeno-guardian-critsave-end-target", ("user", uid)),  
                        old, old, PopupType.SmallCaution);  
                }  
  
                save.ActiveTargets.Clear();  
                continue;  
            }  
  
            _nearby.Clear();  
            var coords = Transform(uid).Coordinates;  
            _lookup.GetEntitiesInRange(coords, save.Range, _nearby, LookupFlags.Uncontained);  
  
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
  
                if (_guardianAbsorb.SetAbsorb(nearby.Owner, uid, GuardianAbsorbType.PassiveCritSave, save.AbsorbPercent)  
                    && !save.ActiveTargets.Contains(nearby.Owner))  
                {  
                    _popup.PopupEntity(  
                        Loc.GetString("rmc-xeno-guardian-critsave-start-target", ("user", uid)),  
                        nearby.Owner, nearby.Owner, PopupType.LargeCaution);  
                    _popup.PopupEntity(  
                        Loc.GetString("rmc-xeno-guardian-critsave-start-self", ("target", nearby.Owner)),  
                        uid, uid, PopupType.LargeCaution);  
                }  
  
                stillActive.Add(nearby.Owner);  
            }  
  
            foreach (var old in save.ActiveTargets)  
            {  
                if (stillActive.Contains(old))  
                    continue;  
  
                _guardianAbsorb.RemoveAbsorb(old, GuardianAbsorbType.PassiveCritSave);  
                _popup.PopupEntity(  
                    Loc.GetString("rmc-xeno-guardian-critsave-end-target", ("user", uid)),  
                    old, old, PopupType.MediumCaution);  
            }  
  
            save.ActiveTargets.Clear();  
            foreach (var active in stillActive)  
                save.ActiveTargets.Add(active);  
        }  
    }  
}
