public sealed class GuardianAbsorbSystem : EntitySystem  
{  
    [Dependency] private readonly DamageableSystem _damageable = default!;  
    [Dependency] private readonly SharedPopupSystem _popup = default!;  
  
    public override void Initialize()  
    {  
        SubscribeLocalEvent<GuardianAbsorbComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);  
        SubscribeLocalEvent<GuardianAbsorbComponent, EntityTerminatingEvent>(OnGuardianAbsorbRemoved);  
    }  
  
    private void OnBeforeDamageChanged(Entity<GuardianAbsorbComponent> ent, ref BeforeDamageChangedEvent args)  
    {  
        if (args.Cancelled || ent.Comp.Guardian is not { } guardian || !args.Damage.AnyPositive())  
            return;  
  
        if (TerminatingOrDeleted(guardian) || Deleted(guardian))  
        {  
            RemComp<GuardianAbsorbComponent>(ent);  
            return;  
        }  
  
        var redirected = args.Damage * ent.Comp.AbsorbPercent;  
        args.Damage -= redirected; // victim only takes the remainder  
  
        _damageable.TryChangeDamage(  
            guardian,  
            redirected,  
            ignoreResistances: ent.Comp.IgnoreGuardianResistances,  
            interruptsDoAfters: false,  
            origin: args.Origin);  
  
        _popup.PopupEntity(Loc.GetString("guardian-absorbing-damage"), guardian, guardian);  
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
