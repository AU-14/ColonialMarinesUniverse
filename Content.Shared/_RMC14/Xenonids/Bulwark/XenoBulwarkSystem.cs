using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared._RMC14.Xenonids.Sweep;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffectNew;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Bulwark;

public sealed partial class XenoBulwarkSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private CMArmorSystem _armor = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private RMCSizeStunSystem _size = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private XenoSystem _xeno = default!;

    private readonly HashSet<EntityUid> _nearbyTargets = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoBulwarkComponent, CMGetArmorEvent>(OnGetArmor);
        SubscribeLocalEvent<XenoBulwarkComponent, BeforeStatusEffectAddedEvent>(OnBeforeStatusAdded);
        SubscribeLocalEvent<XenoBulwarkComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<XenoBulwarkComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<XenoBulwarkComponent, XenoEncasedPlatesActionEvent>(OnEncasedPlatesAction);
        SubscribeLocalEvent<XenoBulwarkComponent, XenoPlateBashActionEvent>(OnPlateBashAction);
        SubscribeLocalEvent<XenoBulwarkComponent, XenoBulwarkTailSwingActionEvent>(OnTailSwingAction);
        SubscribeLocalEvent<XenoBulwarkComponent, XenoReflectiveShieldActionEvent>(OnReflectiveShieldAction);
        SubscribeLocalEvent<XenoBulwarkComponent, ProjectileReflectAttemptEvent>(OnReflectAttempt);
    }

    private void OnGetArmor(Entity<XenoBulwarkComponent> xeno, ref CMGetArmorEvent args)
    {
        args.FrontalArmor += xeno.Comp.PassiveFrontalArmor;
        args.SideArmor += xeno.Comp.PassiveSideArmor;

        if (!xeno.Comp.Encased)
            return;

        args.FrontalArmor += xeno.Comp.EncasedFrontalArmor;
        args.SideArmor += xeno.Comp.EncasedSideArmorPenalty;
    }

    private void OnRefreshSpeed(Entity<XenoBulwarkComponent> xeno, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (xeno.Comp.Encased)
            args.ModifySpeed(xeno.Comp.EncasedSpeedMultiplier, xeno.Comp.EncasedSpeedMultiplier);
    }

    private void OnBeforeStatusAdded(Entity<XenoBulwarkComponent> xeno, ref BeforeStatusEffectAddedEvent args)
    {
        if (xeno.Comp.Encased && xeno.Comp.EncasedImmuneToStatuses.Contains(args.Effect.Id))
            args.Cancelled = true;
    }

    private void OnGetMeleeDamage(Entity<XenoBulwarkComponent> xeno, ref GetMeleeDamageEvent args)
    {
        if (xeno.Comp.Encased)
            args.Damage.ExclusiveAdd(xeno.Comp.EncasedMeleePenalty);
    }

    private void OnEncasedPlatesAction(Entity<XenoBulwarkComponent> xeno, ref XenoEncasedPlatesActionEvent args)
    {
        if (args.Handled || !_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        xeno.Comp.Encased = !xeno.Comp.Encased;
        if (!xeno.Comp.Encased && xeno.Comp.Reflecting)
            StopReflecting(xeno, true);

        UpdateEncasedSize(xeno);

        Dirty(xeno);
        _speed.RefreshMovementSpeedModifiers(xeno);
        _armor.UpdateArmorValue((xeno, null));

        foreach (var action in _rmcActions.GetActionsWithEvent<XenoEncasedPlatesActionEvent>(xeno))
            _actions.SetToggled(action.AsNullable(), xeno.Comp.Encased);
    }

    private void OnPlateBashAction(Entity<XenoBulwarkComponent> xeno, ref XenoPlateBashActionEvent args)
    {
        if (args.Handled || !_xeno.CanAbilityAttackTarget(xeno, args.Target))
            return;

        if (xeno.Comp.Encased && !InPlateBashRange(xeno.Owner, args.Target, xeno.Comp.PlateBashEncasedRange))
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-bulwark-plate-bash-adjacent"), xeno, xeno);
            return;
        }

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;

        _audio.PlayPredicted(xeno.Comp.PlateBashSound, xeno, xeno);
        if (!xeno.Comp.Encased)
            DoPlateBashLeap(xeno, args.Target);

        var damage = new DamageSpecifier(xeno.Comp.PlateBashDamage);
        _damageable.TryChangeDamage(args.Target, damage, armorPiercing: 10, origin: xeno, tool: xeno);
        _stun.TryParalyze(args.Target, xeno.Comp.PlateBashParalyzeTime, true);

        var knockBackDistance = xeno.Comp.Encased
            ? xeno.Comp.PlateBashEncasedKnockBackDistance
            : xeno.Comp.PlateBashUnencasedKnockBackDistance;
        _size.KnockBack(
            args.Target,
            _transform.GetMapCoordinates(xeno),
            knockBackDistance,
            knockBackDistance,
            xeno.Comp.PlateBashKnockBackSpeed,
            ignoreSize: xeno.Comp.Encased);
    }

    private void DoPlateBashLeap(Entity<XenoBulwarkComponent> xeno, EntityUid target)
    {
        var origin = _transform.GetMapCoordinates(xeno);
        var targetCoords = _transform.GetMapCoordinates(target);
        if (origin.MapId != targetCoords.MapId)
            return;

        var diff = targetCoords.Position - origin.Position;
        if (diff == Vector2.Zero)
            return;

        diff = diff.Normalized() * MathF.Min(diff.Length(), xeno.Comp.PlateBashUnencasedLeapRange);
        _throwing.TryThrow(xeno.Owner, diff, xeno.Comp.PlateBashUnencasedLeapSpeed, animated: false);
    }

    private bool InPlateBashRange(EntityUid xeno, EntityUid target, float range)
    {
        var origin = _transform.GetMapCoordinates(xeno);
        var targetCoords = _transform.GetMapCoordinates(target);
        return origin.MapId == targetCoords.MapId && origin.InRange(targetCoords, range);
    }

    private void OnTailSwingAction(Entity<XenoBulwarkComponent> xeno, ref XenoBulwarkTailSwingActionEvent args)
    {
        if (args.Handled)
            return;

        if (xeno.Comp.Encased)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-bulwark-tail-swing-encased"), xeno, xeno);
            return;
        }

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        EnsureComp<XenoSweepingComponent>(xeno.Owner);
        _audio.PlayPredicted(xeno.Comp.TailSwingSound, xeno, xeno);

        var origin = _transform.GetMoverCoordinates(xeno);
        var mapOrigin = _transform.GetMapCoordinates(xeno);
        _nearbyTargets.Clear();
        _entityLookup.GetEntitiesInRange(origin, 1.75f, _nearbyTargets);

        var hit = false;
        foreach (var target in _nearbyTargets)
        {
            if (target == xeno.Owner)
                continue;

            if (_tags.HasTag(target, xeno.Comp.TailSwingFlingable))
            {
                hit = true;
                _size.KnockBack(
                    target,
                    mapOrigin,
                    xeno.Comp.TailSwingFlingDistance,
                    xeno.Comp.TailSwingFlingDistance,
                    xeno.Comp.TailSwingFlingSpeed,
                    ignoreSize: true);
                continue;
            }

            if (!_xeno.CanAbilityAttackTarget(xeno, target))
                continue;

            hit = true;
            _damageable.TryChangeDamage(target, new DamageSpecifier { DamageDict = { ["Slash"] = 20 } }, origin: xeno, tool: xeno);
            _stun.TryParalyze(target, xeno.Comp.TailSwingParalyzeTime, true);
        }

        if (!hit)
            SetTailSwingCooldown(xeno.Owner, xeno.Comp.TailSwingMissCooldown);
    }

    private void OnReflectiveShieldAction(Entity<XenoBulwarkComponent> xeno, ref XenoReflectiveShieldActionEvent args)
    {
        if (args.Handled)
            return;

        if (!xeno.Comp.Encased)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-bulwark-reflective-shield-encase"), xeno, xeno);
            return;
        }

        args.Handled = true;
        if (xeno.Comp.Reflecting)
        {
            if (!_rmcActions.TryUseAction(args))
                return;

            StopReflecting(xeno, true);
            return;
        }

        if (!_rmcActions.TryUseAction(args))
            return;

        xeno.Comp.Reflecting = true;
        xeno.Comp.ReflectStartedAt = _timing.CurTime;
        xeno.Comp.ReflectExpiresAt = _timing.CurTime + xeno.Comp.ReflectDuration;
        Dirty(xeno);

        foreach (var action in _rmcActions.GetActionsWithEvent<XenoReflectiveShieldActionEvent>(xeno))
            _actions.SetToggled(action.AsNullable(), true);
    }

    private void OnReflectAttempt(Entity<XenoBulwarkComponent> xeno, ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled || !xeno.Comp.Reflecting || HasComp<XenoProjectileComponent>(args.ProjUid))
            return;

        var chance = GetDirectionalReflectChance(xeno.Owner, args.ProjUid);
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

        ResetReflectedProjectilePrediction(args.ProjUid);
        args.Component.Shooter = xeno.Owner;
        args.Component.Weapon = xeno.Owner;
        Dirty(args.ProjUid, args.Component);
    }

    private void ResetReflectedProjectilePrediction(EntityUid projectile)
    {
        if (_net.IsServer)
        {
            RemComp<PredictedProjectileServerComponent>(projectile);
            return;
        }

        if (TryComp(projectile, out PredictedProjectileClientComponent? predicted))
            predicted.Hit = false;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<XenoBulwarkComponent>();
        while (query.MoveNext(out var uid, out var bulwark))
        {
            if (!bulwark.Reflecting || time < bulwark.ReflectExpiresAt)
                continue;

            StopReflecting((uid, bulwark), false);
        }
    }

    private void UpdateEncasedSize(Entity<XenoBulwarkComponent> xeno)
    {
        if (!TryComp<RMCSizeComponent>(xeno, out var size))
            return;

        if (xeno.Comp.Encased)
        {
            xeno.Comp.OriginalSize = size.Size;
            size.Size = xeno.Comp.EncasedSize;
        }
        else
        {
            size.Size = xeno.Comp.OriginalSize ?? RMCSizes.Xeno;
            xeno.Comp.OriginalSize = null;
        }

        Dirty(xeno.Owner, size);
    }

    private void StopReflecting(Entity<XenoBulwarkComponent> xeno, bool refund)
    {
        xeno.Comp.Reflecting = false;
        Dirty(xeno);

        TimeSpan cooldown;
        if (refund)
        {
            var elapsed = _timing.CurTime - xeno.Comp.ReflectStartedAt;
            var seconds = Math.Max(
                xeno.Comp.ReflectMinCooldown.TotalSeconds,
                elapsed.TotalSeconds * xeno.Comp.ReflectCooldownPerSecond);
            cooldown = TimeSpan.FromSeconds(seconds);
        }
        else
        {
            cooldown = xeno.Comp.ReflectFullCooldown;
        }

        foreach (var action in _rmcActions.GetActionsWithEvent<XenoReflectiveShieldActionEvent>(xeno))
        {
            _actions.SetToggled(action.AsNullable(), false);
            _actions.SetCooldown(action.AsNullable(), cooldown);
        }
    }

    private void SetTailSwingCooldown(EntityUid xeno, TimeSpan cooldown)
    {
        foreach (var (actionId, _) in _rmcActions.GetActionsWithEvent<XenoBulwarkTailSwingActionEvent>(xeno))
        {
            _actions.SetCooldown(actionId, cooldown);
        }
    }

    private float GetDirectionalReflectChance(EntityUid defender, EntityUid projectile)
    {
        var projectileCoords = _transform.GetMapCoordinates(projectile);
        var defenderCoords = _transform.GetMapCoordinates(defender);
        if (projectileCoords.MapId != defenderCoords.MapId)
            return 0.3f;

        var diff = (projectileCoords.Position - defenderCoords.Position).ToWorldAngle().GetCardinalDir();
        var dir = _transform.GetWorldRotation(defender).GetCardinalDir();
        if (dir == diff)
            return 0.8f;

        var perpendiculars = diff.GetPerpendiculars();
        if (dir == perpendiculars.First || dir == perpendiculars.Second)
            return 0.65f;

        return 0.3f;
    }
}
