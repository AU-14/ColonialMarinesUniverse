namespace Content.Shared._RMC14.Xenonids.GuardianSweep;  
  
public sealed partial class XenoGuardianSweepSystem : EntitySystem  
{  
    [Dependency] private SharedRMCActionsSystem _actions = default!;  
    [Dependency] private SharedRMCEmoteSystem _emote = default!;  
    [Dependency] private EntityLookupSystem _entityLookup = default!;  
    [Dependency] private SharedTransformSystem _transform = default!;  
    [Dependency] private XenoSystem _xeno = default!;  
    [Dependency] private SharedInteractionSystem _interact = default!;  
    [Dependency] private DamageableSystem _damage = default!;  
    [Dependency] private SharedColorFlashEffectSystem _colorFlash = default!;  
    [Dependency] private INetManager _net = default!;  
    [Dependency] private SharedAudioSystem _audio = default!;  
    [Dependency] private SharedHitLocationSystem _hitLocation = default!;  
    [Dependency] private RMCSlowSystem _slow = default!;  
    [Dependency] private RMCSizeStunSystem _size = default!;  
  
    public override void Initialize()  
    {  
        SubscribeLocalEvent<XenoGuardianSweepComponent, XenoGuardianSweepActionEvent>(OnXenoGuardianSweepAction);  
    }  
  
    private void OnXenoGuardianSweepAction(Entity<XenoGuardianSweepComponent> xeno, ref XenoGuardianSweepActionEvent args)  
    {  
        if (args.Handled)  
            return;  
  
        if (!_actions.TryUseAction(args))  
            return;  
  
        args.Handled = true;  
  
        EnsureComp<XenoSweepingComponent>(xeno);  
        _emote.TryEmoteWithChat(xeno, xeno.Comp.HissEmote);  
  
        using var targetingSuppression = _hitLocation.SuppressBodyZoneTargeting(xeno.Owner);  
        var origin = _transform.GetMapCoordinates(xeno);  
        foreach (var ent in _entityLookup.GetEntitiesInRange<MobStateComponent>(origin, xeno.Comp.Range))  
        {  
            if (!_xeno.CanAbilityAttackTarget(xeno, ent))  
                continue;  
  
            if (!_interact.InRangeUnobstructed(xeno.Owner, ent.Owner, xeno.Comp.Range))  
                continue;  
  
            var myDamage = _damage.TryChangeDamage(ent, xeno.Comp.Damage, origin: xeno, tool: xeno);  
            if (myDamage?.GetTotal() > FixedPoint2.Zero)  
            {  
                var filter = Filter.Pvs(ent, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == xeno.Owner);  
                _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { ent }, filter);  
            }  
  
            _slow.TrySlowdown(ent, xeno.Comp.SlowDuration);  
            _size.KnockBack(ent, origin, xeno.Comp.KnockBackDistance, xeno.Comp.KnockBackDistance);  
  
            if (_net.IsServer)  
                SpawnAttachedTo(xeno.Comp.Effect, ent.Owner.ToCoordinates());  
  
            _audio.PlayPredicted(xeno.Comp.Sound, xeno, xeno);  
        }  
    }  
}
