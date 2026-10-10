using Content.Server.Electrocution;
using Content.Shared._RMC14.Projectiles;
using Content.Shared.Damage.Systems;
using Content.Shared.Electrocution;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Events;

namespace Content.Server.CMU14.Weapons.Ranged.Hitscan;

public sealed partial class CMUHitscanImpactSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private ElectrocutionSystem _electrocution = default!;
    [Dependency] private RMCAreaDamageSystem _areaDamage = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUHitscanImpactComponent, HitscanRaycastFiredEvent>(OnHitscanHit);
    }

    private void OnHitscanHit(Entity<CMUHitscanImpactComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (args.Data.HitEntity is not { } target)
            return;

        var shooter = args.Data.Shooter ?? args.Data.Gun;
        var damage = ent.Comp.Damage * _damageable.UniversalProjectileDamageModifier;

        // Intact dropship hulls take hits through this event rather than Damageable.
        var targetHit = new ProjectileHitTargetEvent(damage, ent, shooter);
        RaiseLocalEvent(target, ref targetHit);

        _damageable.TryChangeDamage(target, damage, origin: shooter, tool: ent);

        if (HasComp<RMCAreaDamageComponent>(ent))
            _areaDamage.ApplyAreaDamage(ent, target, damage, shooter);

        if (HasComp<ElectrifiedComponent>(ent) && !TerminatingOrDeleted(target))
            _electrocution.TryDoElectrifiedAct(ent, target);
    }
}
