using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Content.Shared.Explosion;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Shields;

public sealed partial class CrusherShieldSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private XenoShieldSystem _shield = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrusherShieldComponent, DamageModifyAfterResistEvent>(OnDamage, before: [typeof(XenoShieldSystem)]);
        SubscribeLocalEvent<CrusherShieldComponent, GetExplosionResistanceEvent>(OnGetExplosionResistance);
        SubscribeLocalEvent<CrusherShieldComponent, RemovedShieldEvent>(OnShieldRemove);
        SubscribeLocalEvent<CrusherShieldComponent, XenoDefensiveShieldActionEvent>(OnXenoDefensiveShieldAction);
        SubscribeLocalEvent<CrusherShieldComponent, ProjectileReflectAttemptEvent>(OnReflectAttempt);
    }

    private void OnXenoDefensiveShieldAction(Entity<CrusherShieldComponent> xeno, ref XenoDefensiveShieldActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_xenoPlasma.TryRemovePlasma(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        args.Handled = true;

        EnsureComp<XenoShieldComponent>(xeno);
        _shield.ApplyShield(xeno, XenoShieldSystem.ShieldType.Crusher, xeno.Comp.Amount, visualState: "king-shield", maxShield: xeno.Comp.Amount.Double());
        ApplyEffects(xeno);

        if (_net.IsClient)
            return;

        _popup.PopupEntity(Loc.GetString("rmc-xeno-defensive-shield-activate", ("user", xeno)), xeno, Filter.PvsExcept(xeno), true, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("rmc-xeno-defensive-shield-activate-self", ("user", xeno)), xeno, xeno, PopupType.Medium);
        SpawnAttachedTo(xeno.Comp.Effect, xeno.Owner.ToCoordinates());
        foreach (var action in _rmcActions.GetActionsWithEvent<XenoDefensiveShieldActionEvent>(xeno))
        {
            _actions.SetToggled((action, action), true);
        }
    }


    public void ApplyEffects(Entity<CrusherShieldComponent> ent)
    {
        if (!TryComp<CMArmorComponent>(ent, out var armor))
            return;

        ent.Comp.ExplosionOffAt = _timing.CurTime + ent.Comp.ExplosionResistanceDuration;
        ent.Comp.ShieldOffAt = _timing.CurTime + ent.Comp.ShieldDuration;
        ent.Comp.ExplosionResistApplying = true;

    }

    public void OnShieldRemove(Entity<CrusherShieldComponent> ent, ref RemovedShieldEvent args)
    {
        if (args.Type == XenoShieldSystem.ShieldType.Crusher)
        {
            _popup.PopupEntity(Loc.GetString("rmc-xeno-defensive-shield-end"), ent, ent, PopupType.MediumCaution);
            foreach (var action in _rmcActions.GetActionsWithEvent<XenoDefensiveShieldActionEvent>(ent))
            {
                _actions.SetToggled(action.Owner, false);
            }
        }
    }

    private void OnReflectAttempt(Entity<CrusherShieldComponent> xeno, ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        // Only reflect while shield is active.
        if (!TryComp<XenoShieldComponent>(xeno, out var shield) ||
            !shield.Active ||
            shield.Shield != XenoShieldSystem.ShieldType.Crusher)
            return;

        // Don't reflect xeno projectiles.
        if (HasComp<XenoProjectileComponent>(args.ProjUid))
            return;

        var chance = GetDirectionalReflectChance(xeno, args.ProjUid);
        if (!_random.Prob(chance))
            return;

        args.Cancelled = true;
        if (TryComp(args.ProjUid, out PhysicsComponent? physics))
        {
            var velocity = _physics.GetMapLinearVelocity(args.ProjUid, component: physics);
            var rotation = _random.NextAngle(-Angle.FromDegrees(90), Angle.FromDegrees(90)).Opposite();
            var reflected = rotation.RotateVec(velocity);
            _physics.SetLinearVelocity(args.ProjUid, reflected, body: physics);
            _transform.SetWorldRotationNoLerp(args.ProjUid, reflected.ToWorldAngle() + args.Component.Angle);
        }

        if (_net.IsServer)
        {
            RemComp<PredictedProjectileServerComponent>(args.ProjUid);
        }
        else if (TryComp(args.ProjUid, out PredictedProjectileClientComponent? predicted))
        {
            predicted.Hit = false;
        }

        args.Component.Shooter = xeno.Owner;
        args.Component.Weapon = xeno.Owner;
        Dirty(args.ProjUid, args.Component);
    }

    private float GetDirectionalReflectChance(Entity<CrusherShieldComponent> xeno, EntityUid projectile)
    {
        var projectileCoords = _transform.GetMapCoordinates(projectile);
        var defenderCoords = _transform.GetMapCoordinates(xeno);
        if (projectileCoords.MapId != defenderCoords.MapId)
            return xeno.Comp.ReflectChanceBack;

        var diff = (projectileCoords.Position - defenderCoords.Position).ToWorldAngle().GetCardinalDir();
        var dir = _transform.GetWorldRotation(xeno).GetCardinalDir();

        if (dir == diff)
            return xeno.Comp.ReflectChanceFront;

        var perpendiculars = diff.GetPerpendiculars();
        if (dir == perpendiculars.First || dir == perpendiculars.Second)
            return xeno.Comp.ReflectChanceSide;

        return xeno.Comp.ReflectChanceBack;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;

        var crusherQuery = EntityQueryEnumerator<CrusherShieldComponent, XenoShieldComponent>();
        while (crusherQuery.MoveNext(out var uid, out var crushShield, out var shield))
        {
            if (crushShield.ExplosionResistApplying && crushShield.ExplosionOffAt <= time)
            {
                crushShield.ExplosionResistApplying = false;
                _popup.PopupEntity(Loc.GetString("rmc-xeno-defensive-shield-resist-end"), uid, uid, PopupType.SmallCaution);
            }

            if (shield.Active && shield.Shield == XenoShieldSystem.ShieldType.Crusher && crushShield.ShieldOffAt <= time)
                _shield.RemoveShield(uid, XenoShieldSystem.ShieldType.Crusher);
        }
    }

    public void OnDamage(Entity<CrusherShieldComponent> ent, ref DamageModifyAfterResistEvent args)
    {
        if (!TryComp<XenoShieldComponent>(ent, out var shield))
            return;

        if (!shield.Active || shield.Shield != XenoShieldSystem.ShieldType.Crusher)
            return;

        foreach (var type in args.Damage.DamageDict)
        {
            if (args.Damage.DamageDict[type.Key] <= 0)
                continue;

            args.Damage.DamageDict[type.Key] -= ent.Comp.DamageReduction;

            if (args.Damage.DamageDict[type.Key] < 0)
                args.Damage.DamageDict[type.Key] = 0;
        }
    }

    public void OnGetExplosionResistance(Entity<CrusherShieldComponent> ent, ref GetExplosionResistanceEvent args)
    {
        if (!ent.Comp.ExplosionResistApplying)
            return;

        var explosionResist = ent.Comp.ExplosionResistance;

        var resist = (float) Math.Pow(1.1, explosionResist / 5.0); // From armor calcualtion
        args.DamageCoefficient /= resist;
    }
}
