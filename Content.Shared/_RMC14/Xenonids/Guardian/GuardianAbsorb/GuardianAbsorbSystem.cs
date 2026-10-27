using Content.Shared._RMC14.Damage;  
using Content.Shared._RMC14.Xenonids.Guardian.GuardianCritSave;  
using Content.Shared.Damage;  
using Content.Shared.Damage.Systems;  
using Content.Shared.Mobs.Systems;  
using Content.Shared.Popups;  
  
namespace Content.Shared._RMC14.Xenonids.Guardian.GuardianAbsorb;  
  
public sealed class GuardianAbsorbSystem : EntitySystem  
{  
    [Dependency] private readonly DamageableSystem _damageable = default!;  
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;  
    [Dependency] private readonly SharedPopupSystem _popup = default!;  
  
    public override void Initialize()  
    {  
        SubscribeLocalEvent<GuardianAbsorbComponent, DamageModifyAfterResistEvent>(OnDamageModify);  
        SubscribeLocalEvent<GuardianAbsorbComponent, EntityTerminatingEvent>(OnGuardianAbsorbRemoved);  
    }  
  
    private void OnDamageModify(Entity<GuardianAbsorbComponent> ent, ref DamageModifyAfterResistEvent args)  
    {  
        if (ent.Comp.Guardian is not { } guardian || !Exists(guardian))  
            return;  
  
        if (!args.Damage.AnyPositive())  
            return;  
  
        // Only ever redirect the positive portion; let healing pass straight through.  
        var redirected = DamageSpecifier.GetPositive(args.Damage) * ent.Comp.AbsorbPercent;  
  
        if (!redirected.AnyPositive())  
            return;  
  
        // Figure out how much headroom the Guardian actually has before it would hit crit.  
        if (!_mobThreshold.TryGetIncapThreshold(guardian, out var incapThreshold))  
            return;  
  
        var headroom = incapThreshold.Value - ent.Comp.GuardianCachedDamage;  
        if (headroom < 0)  
            headroom = 0;  
  
        var redirectedTotal = redirected.GetTotal();  
  
        // Clamp the redirected amount so it can never push the Guardian past its own crit threshold.  
        if (redirectedTotal > headroom)  
        {  
            if (headroom <= 0)  
            {  
                // Guardian has no headroom left at all; nothing gets redirected, so the overflow  
                // simply stays applied to the original target instead of one-shotting the Guardian.  
                redirected = new DamageSpecifier();  
            }  
            else  
            {  
                var scale = (headroom / redirectedTotal).Float();  
                redirected *= scale;  
            }  
        }  
  
        if (redirected.AnyPositive())  
        {  
            _damageable.TryChangeDamage(guardian, redirected, ignoreResistances: ent.Comp.IgnoreGuardianResistances, origin: ent.Comp.Guardian);  
            _popup.PopupEntity(Loc.GetString("rmc-xeno-guardian-absorbing-damage"), guardian, PopupType.Small);  
        }  
  
        // Whatever fraction of the original hit was actually redirected comes off the original target.  
        // (e.g. AbsorbPercent 1f + full headroom available => target takes 0; AbsorbPercent 0.5f => target still takes the other half).  
        args.Damage -= redirected;  
        args.Damage.ClampMin(0);  
    }  
  
    private void OnGuardianAbsorbRemoved(Entity<GuardianAbsorbComponent> ent, ref EntityTerminatingEvent args)  
    {  
    }  
  
    public void SetAbsorb(EntityUid target, EntityUid guardian, GuardianAbsorbType type,  
        float percent = 0.5f, bool ignoreResistances = true)  
    {  
        var comp = EnsureComp<GuardianAbsorbComponent>(target);  
        comp.Guardian = guardian;  
        comp.Type = type;  
        comp.AbsorbPercent = percent;  
        comp.IgnoreGuardianResistances = ignoreResistances;  
        Dirty(target, comp);  
    }  
  
    public void RemoveAbsorb(EntityUid target, GuardianAbsorbType? type = null)  
    {  
        if (!TryComp<GuardianAbsorbComponent>(target, out var comp))  
            return;  
        if (type != null && comp.Type != type)  
            return;  
        RemComp<GuardianAbsorbComponent>(target);  
    }  
}
