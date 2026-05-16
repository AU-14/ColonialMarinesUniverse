using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Database;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaSmartDiscSystem : EntitySystem
{
    private const float MinimumHuntDistanceSquared = 0.04f;
    private const float DiscOrbitSpeedRatio = 0.45f;

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrownItemSystem _thrown = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaSmartDiscComponent, ItemToggleActivateAttemptEvent>(OnActivateAttempt);
        SubscribeLocalEvent<YautjaSmartDiscComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<YautjaSmartDiscComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<YautjaSmartDiscComponent, ThrownEvent>(OnThrown);
        SubscribeLocalEvent<YautjaSmartDiscComponent, ThrowDoHitEvent>(OnThrowHit);
        SubscribeLocalEvent<YautjaSmartDiscComponent, StopThrowEvent>(OnStopThrow);
        SubscribeLocalEvent<YautjaSmartDiscComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<YautjaSmartDiscComponent, GotEquippedHandEvent>(OnGotEquippedHand);
        SubscribeLocalEvent<YautjaSmartDiscComponent, UseInHandEvent>(OnUseInHand, before: new[] { typeof(ItemToggleSystem) });
        SubscribeLocalEvent<YautjaSmartDiscComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnActivateAttempt(Entity<YautjaSmartDiscComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        if (args.User is not { } user)
        {
            args.Popup = Loc.GetString("cmu-yautja-tech-denied");
            args.Cancelled = true;
            return;
        }

        if (!HasComp<YautjaComponent>(user))
        {
            if (!IsValidRogueTarget(ent, user))
            {
                args.Popup = Loc.GetString("cmu-yautja-tech-denied");
                args.Cancelled = true;
                return;
            }

            ent.Comp.RogueTarget = user;
            ent.Comp.RogueActivator = user;
            return;
        }

        ent.Comp.RogueTarget = null;
        ent.Comp.RogueActivator = null;

        if (!TryGetOrSetOwner(ent, user, out var owner) || owner != user)
        {
            args.Popup = Loc.GetString("cmu-yautja-disc-owner-denied");
            args.Cancelled = true;
            return;
        }

        if (!TryFindNearestTarget(ent, out _))
        {
            args.Popup = Loc.GetString("cmu-yautja-disc-no-target");
            args.Cancelled = true;
        }
    }

    private void OnToggled(Entity<YautjaSmartDiscComponent> ent, ref ItemToggledEvent args)
    {
        if (args.Activated)
        {
            StartDisc(ent, args.User);
            return;
        }

        StopDisc(ent);
    }

    private void OnThrown(Entity<YautjaSmartDiscComponent> ent, ref ThrownEvent args)
    {
        if (ent.Comp.Active || args.User is not { } user)
            return;

        ent.Comp.PendingThrowActivator = user;
        ent.Comp.PendingThrowActivationAt = _timing.CurTime + ent.Comp.ThrowActivationDelay;
    }

    private void OnPreventCollide(Entity<YautjaSmartDiscComponent> ent, ref PreventCollideEvent args)
    {
        if (!ent.Comp.Active)
            return;

        if (args.OtherEntity == ent.Comp.YautjaOwner)
        {
            args.Cancelled = true;
            return;
        }

        if (TryResolveTarget(args.OtherEntity, out var target) &&
            HasComp<MobStateComponent>(target))
        {
            args.Cancelled = true;
            return;
        }
    }

    private void OnThrowHit(Entity<YautjaSmartDiscComponent> ent, ref ThrowDoHitEvent args)
    {
        if (!ent.Comp.Active ||
            !TryResolveTarget(args.Target, out var target) ||
            !IsValidTarget(ent, target))
            return;

        RegisterHit(ent, target);
    }

    private void OnStopThrow(Entity<YautjaSmartDiscComponent> ent, ref StopThrowEvent args)
    {
        if (!ent.Comp.Active)
        {
            ClearPendingThrowActivation(ent.Comp);
            return;
        }

        ent.Comp.NextRetarget = _timing.CurTime + ent.Comp.RetargetDelay;
    }

    private void OnPickupAttempt(Entity<YautjaSmartDiscComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (!ent.Comp.Active)
            return;

        if (!HasComp<YautjaComponent>(args.User))
        {
            args.Cancel();
            _popup.PopupEntity(Loc.GetString("cmu-yautja-disc-stolen-active"), ent.Owner, args.User, PopupType.SmallCaution);
            return;
        }

        _toggle.TrySetActive((ent.Owner, null), false, args.User, false);
    }

    private void OnGotEquippedHand(Entity<YautjaSmartDiscComponent> ent, ref GotEquippedHandEvent args)
    {
        ClearPendingThrowActivation(ent.Comp);

        if (ent.Comp.Active && HasComp<YautjaComponent>(args.User))
            _toggle.TrySetActive((ent.Owner, null), false, args.User, false);
    }

    private void OnUseInHand(Entity<YautjaSmartDiscComponent> ent, ref UseInHandEvent args)
    {
        if (!ent.Comp.Active)
            return;

        args.Handled = true;
        if (!HasComp<YautjaComponent>(args.User))
        {
            ent.Comp.RogueTarget = args.User;
            ent.Comp.RogueActivator = args.User;
            DropFromHands(ent.Owner, args.User);
            _popup.PopupEntity(Loc.GetString("cmu-yautja-disc-stolen-active"), ent.Owner, args.User, PopupType.SmallCaution);
            return;
        }

        _toggle.TrySetActive((ent.Owner, null), false, args.User, false);
    }

    private void OnShutdown(Entity<YautjaSmartDiscComponent> ent, ref ComponentShutdown args)
    {
        StopDisc(ent);
    }

    private void StartDisc(Entity<YautjaSmartDiscComponent> ent, EntityUid? user)
    {
        if (TryGetRogueTarget(ent, out var rogueTarget))
        {
            StartRogueDisc(ent, user, rogueTarget);
            return;
        }

        if (user is not { } owner && !TryGetOwner(ent, out owner))
        {
            _toggle.TrySetActive((ent.Owner, null), false, null, false);
            return;
        }

        if (!TryGetOrSetOwner(ent, owner, out owner))
        {
            _toggle.TrySetActive((ent.Owner, null), false, null, false);
            return;
        }

        ent.Comp.Active = true;
        ent.Comp.YautjaOwner = owner;
        ent.Comp.Hits = 0;
        ent.Comp.ActiveUntil = _timing.CurTime + ent.Comp.ActiveTime;
        ent.Comp.NextRetarget = _timing.CurTime;
        ent.Comp.NextHit = _timing.CurTime;
        ent.Comp.CurrentTarget = null;

        if (!ReleaseDisc(ent.Owner, owner))
        {
            _toggle.TrySetActive((ent.Owner, null), false, owner, false);
            return;
        }

        if (!TryFindNearestTarget(ent, out var target))
        {
            _toggle.TrySetActive((ent.Owner, null), false, owner, false);
            _popup.PopupEntity(Loc.GetString("cmu-yautja-disc-no-target"), ent.Owner, owner, PopupType.SmallCaution);
            return;
        }

        ent.Comp.CurrentTarget = target;
        if (TryComp(ent.Owner, out PhysicsComponent? physics))
            SteerDisc(ent.Owner, ent.Comp, physics, owner, target);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-disc-activated"), ent.Owner, owner);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(owner):player} activated Yautja smart disc {ToPrettyString(ent.Owner):disc}");
    }

    private void StopDisc(Entity<YautjaSmartDiscComponent> ent)
    {
        ClearPendingThrowActivation(ent.Comp);

        ent.Comp.Active = false;
        ent.Comp.CurrentTarget = null;
        ent.Comp.RogueTarget = null;
        ent.Comp.RogueActivator = null;
        ent.Comp.Hits = 0;
        ent.Comp.NextRetarget = TimeSpan.Zero;
        ent.Comp.NextHit = TimeSpan.Zero;
        ent.Comp.ActiveUntil = TimeSpan.Zero;

        if (TryComp(ent.Owner, out ThrownItemComponent? thrown))
            _thrown.StopThrow(ent.Owner, thrown);

        if (TryComp(ent.Owner, out PhysicsComponent? physics))
        {
            _physics.SetLinearVelocity(ent.Owner, Vector2.Zero, body: physics);
            _physics.SetAngularVelocity(ent.Owner, 0f, body: physics);
            _physics.SetBodyStatus(ent.Owner, physics, BodyStatus.OnGround);
        }
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<YautjaSmartDiscComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var disc, out var physics))
        {
            if (!disc.Active)
            {
                TryActivatePendingThrow((uid, disc));
                continue;
            }

            if (_timing.CurTime >= disc.ActiveUntil)
            {
                _toggle.TrySetActive((uid, null), false, disc.YautjaOwner, false);
                continue;
            }

            var ent = (uid, disc);
            if (!TryGetOwner(ent, out var owner))
            {
                _toggle.TrySetActive((uid, null), false, null, false);
                continue;
            }

            if (_containers.IsEntityInContainer(uid))
            {
                if (!ReleaseDisc(uid, disc.RogueActivator ?? owner))
                {
                    _toggle.TrySetActive((uid, null), false, owner, false);
                    continue;
                }
            }

            if (!IsValidTarget(ent, disc.CurrentTarget))
            {
                if (_timing.CurTime < disc.NextRetarget)
                    continue;

                if (!TryFindNearestTarget(ent, out var newTarget))
                {
                    _toggle.TrySetActive((uid, null), false, owner, false);
                    _popup.PopupEntity(Loc.GetString("cmu-yautja-disc-no-target"), uid, owner, PopupType.SmallCaution);
                    continue;
                }

                disc.CurrentTarget = newTarget;
            }

            if (disc.CurrentTarget is not { } currentTarget)
                continue;

            TryStrikeNearbyTarget(uid, disc);
            if (!disc.Active)
                continue;

            SteerDisc(uid, disc, physics, owner, currentTarget);
        }
    }

    private void TryActivatePendingThrow(Entity<YautjaSmartDiscComponent> ent)
    {
        if (ent.Comp.PendingThrowActivator is not { } user)
            return;

        if (_timing.CurTime < ent.Comp.PendingThrowActivationAt)
            return;

        ClearPendingThrowActivation(ent.Comp);

        if (TerminatingOrDeleted(user) ||
            !TryComp(ent.Owner, out ThrownItemComponent? _))
        {
            return;
        }

        _toggle.TrySetActive((ent.Owner, null), true, user, false);
    }

    private static void ClearPendingThrowActivation(YautjaSmartDiscComponent disc)
    {
        disc.PendingThrowActivator = null;
        disc.PendingThrowActivationAt = TimeSpan.Zero;
    }

    private void SteerDisc(EntityUid uid, YautjaSmartDiscComponent disc, PhysicsComponent physics, EntityUid owner, EntityUid target)
    {
        var discCoords = _transform.GetMapCoordinates(uid);
        var targetCoords = _transform.GetMapCoordinates(target);
        if (discCoords.MapId != targetCoords.MapId)
        {
            disc.CurrentTarget = null;
            return;
        }

        var direction = targetCoords.Position - discCoords.Position;
        var distanceSquared = direction.LengthSquared();
        if (distanceSquared <= disc.HitRange * disc.HitRange)
        {
            if (!EnsureThrown(uid, disc, physics, owner, direction))
                return;

            OrbitTarget(uid, disc, physics, direction);
            TryStrikeTarget(uid, disc, target);
            return;
        }

        if (distanceSquared <= 0f)
        {
            disc.CurrentTarget = null;
            disc.NextRetarget = _timing.CurTime + disc.RetargetDelay;
            return;
        }

        if (!EnsureThrown(uid, disc, physics, owner, direction))
            return;

        _physics.SetLinearVelocity(uid, direction.Normalized() * disc.ThrowSpeed, body: physics);
    }

    private bool EnsureThrown(EntityUid uid, YautjaSmartDiscComponent disc, PhysicsComponent physics, EntityUid owner, Vector2 direction)
    {
        if (direction.LengthSquared() <= 0f)
            direction = Vector2.UnitX;

        if (!TryComp(uid, out ThrownItemComponent? thrown))
        {
            _throwing.TryThrow(
                uid,
                direction,
                disc.ThrowSpeed,
                owner,
                pushbackRatio: 0f,
                compensateFriction: false,
                recoil: false,
                animated: true,
                playSound: false,
                doSpin: true,
                rotate: false);

            TryComp(uid, out thrown);
        }

        if (thrown == null)
            return false;

        thrown.LandTime = disc.ActiveUntil;
        thrown.PlayLandSound = false;
        Dirty(uid, thrown);

        _physics.SetBodyStatus(uid, physics, BodyStatus.InAir);
        _physics.SetAngularVelocity(uid, disc.SpinVelocity, body: physics);
        return true;
    }

    private void OrbitTarget(EntityUid uid, YautjaSmartDiscComponent disc, PhysicsComponent physics, Vector2 direction)
    {
        var normal = direction.LengthSquared() <= 0f
            ? Vector2.UnitX
            : direction.Normalized();

        var tangent = new Vector2(-normal.Y, normal.X);
        var velocity = tangent * (disc.ThrowSpeed * DiscOrbitSpeedRatio);

        if (velocity.LengthSquared() <= 0f)
            velocity = Vector2.UnitX * (disc.ThrowSpeed * DiscOrbitSpeedRatio);

        _physics.SetBodyStatus(uid, physics, BodyStatus.InAir);
        _physics.SetLinearVelocity(uid, velocity, body: physics);
        _physics.SetAngularVelocity(uid, disc.SpinVelocity, body: physics);
    }

    private bool TryStrikeNearbyTarget(EntityUid uid, YautjaSmartDiscComponent disc)
    {
        if (_timing.CurTime < disc.NextHit)
            return false;

        if (!TryFindHitTarget((uid, disc), out var target))
            return false;

        return TryStrikeTarget(uid, disc, target);
    }

    private bool TryStrikeTarget(EntityUid uid, YautjaSmartDiscComponent disc, EntityUid target)
    {
        if (_timing.CurTime < disc.NextHit || !IsValidTarget((uid, disc), target))
            return false;

        if (TryComp(uid, out DamageOtherOnHitComponent? damage))
        {
            var damageSpec = GetDiscDamageAgainstTarget(disc, target, damage.Damage);
            var dealt = _damage.TryChangeDamage(
                target,
                damageSpec * _damage.UniversalThrownDamageModifier,
                damage.IgnoreResistances,
                origin: disc.YautjaOwner ?? uid,
                tool: uid);

            if (dealt != null && !dealt.Empty)
                _audio.PlayPvs(disc.HitSound, target);
        }

        RegisterHit((uid, disc), target);
        return true;
    }

    private DamageSpecifier GetDiscDamageAgainstTarget(YautjaSmartDiscComponent disc, EntityUid target, DamageSpecifier damage)
    {
        if (IsHumanDiscTarget(target))
            return damage * disc.HumanDamageMultiplier;

        return damage;
    }

    private bool IsHumanDiscTarget(EntityUid target)
    {
        return HasComp<HumanoidAppearanceComponent>(target) && !HasComp<YautjaComponent>(target);
    }

    private bool TryFindHitTarget(Entity<YautjaSmartDiscComponent> ent, out EntityUid target)
    {
        target = default;
        var discCoords = _transform.GetMapCoordinates(ent.Owner);
        var hitRangeSquared = ent.Comp.HitRange * ent.Comp.HitRange;

        if (ent.Comp.CurrentTarget is { } current &&
            IsValidTarget(ent, current))
        {
            var currentCoords = _transform.GetMapCoordinates(current);
            if (currentCoords.MapId == discCoords.MapId &&
                (currentCoords.Position - discCoords.Position).LengthSquared() <= hitRangeSquared)
            {
                target = current;
                return true;
            }
        }

        EntityUid? closest = null;
        var closestDistance = hitRangeSquared;
        foreach (var uid in _lookup.GetEntitiesInRange(discCoords, ent.Comp.HitRange))
        {
            if (!TryResolveTarget(uid, out var candidate) ||
                !IsValidTarget(ent, candidate))
            {
                continue;
            }

            var coords = _transform.GetMapCoordinates(uid);
            if (coords.MapId != discCoords.MapId)
                continue;

            var distance = (coords.Position - discCoords.Position).LengthSquared();
            if (distance > closestDistance)
                continue;

            closest = candidate;
            closestDistance = distance;
        }

        if (closest is not { } found)
            return false;

        target = found;
        return true;
    }

    private void RegisterHit(Entity<YautjaSmartDiscComponent> ent, EntityUid target)
    {
        if (_timing.CurTime < ent.Comp.NextHit)
            return;

        ent.Comp.Hits++;
        ent.Comp.CurrentTarget = IsValidTarget(ent, target) ? target : null;
        ent.Comp.NextHit = _timing.CurTime + ent.Comp.HitDelay;
        ent.Comp.NextRetarget = _timing.CurTime + ent.Comp.RetargetDelay;

        _adminLog.Add(LogType.ThrowHit, LogImpact.Low, $"{ToPrettyString(ent.Owner):disc} Yautja smart disc hit {ToPrettyString(target):target}");

        if (ent.Comp.Hits >= ent.Comp.MaxHits)
            _toggle.TrySetActive((ent.Owner, null), false, ent.Comp.YautjaOwner, false);
    }

    private bool TryFindNearestTarget(Entity<YautjaSmartDiscComponent> ent, out EntityUid target)
    {
        target = default;
        if (TryGetRogueTarget(ent, out target))
            return true;

        var discCoords = _transform.GetMapCoordinates(ent.Owner);
        EntityUid? closest = null;
        var closestDistance = ent.Comp.SearchRange * ent.Comp.SearchRange;

        foreach (var uid in _lookup.GetEntitiesInRange(discCoords, ent.Comp.SearchRange))
        {
            if (!TryResolveTarget(uid, out var candidate) ||
                !IsValidTarget(ent, candidate))
                continue;

            var targetCoords = _transform.GetMapCoordinates(candidate);
            if (targetCoords.MapId != discCoords.MapId)
                continue;

            var distance = (targetCoords.Position - discCoords.Position).LengthSquared();
            if (distance <= MinimumHuntDistanceSquared)
                continue;

            if (distance > closestDistance)
                continue;

            closest = candidate;
            closestDistance = distance;
        }

        if (closest is not { } found)
            return false;

        target = found;
        return true;
    }

    private bool IsValidTarget(Entity<YautjaSmartDiscComponent> ent, EntityUid? target)
    {
        if (!TryResolveTarget(target, out var resolved) ||
            resolved == ent.Owner ||
            resolved == ent.Comp.YautjaOwner ||
            HasComp<YautjaComponent>(resolved) ||
            !TryComp(resolved, out MobStateComponent? mobState) ||
            !_mobState.IsAlive(resolved, mobState))
        {
            return false;
        }

        var discCoords = _transform.GetMapCoordinates(ent.Owner);
        var targetCoords = _transform.GetMapCoordinates(resolved);
        if (discCoords.MapId != targetCoords.MapId)
            return false;

        return (targetCoords.Position - discCoords.Position).LengthSquared() <= ent.Comp.SearchRange * ent.Comp.SearchRange;
    }

    private bool TryResolveTarget(EntityUid? target, out EntityUid resolved)
    {
        resolved = default;
        if (target == null || TerminatingOrDeleted(target.Value))
            return false;

        resolved = target.Value;
        if (TryComp(resolved, out BodyPartComponent? part) &&
            part.Body is { } body &&
            !TerminatingOrDeleted(body))
        {
            resolved = body;
        }

        return true;
    }

    private bool TryGetRogueTarget(Entity<YautjaSmartDiscComponent> ent, out EntityUid target)
    {
        target = ent.Comp.RogueTarget ?? default;
        return target.IsValid() && IsValidRogueTarget(ent, target);
    }

    private bool IsValidRogueTarget(Entity<YautjaSmartDiscComponent> ent, EntityUid target)
    {
        return !HasComp<YautjaComponent>(target) && IsValidTarget(ent, target);
    }

    private void StartRogueDisc(Entity<YautjaSmartDiscComponent> ent, EntityUid? user, EntityUid target)
    {
        ent.Comp.Active = true;
        ent.Comp.Hits = 0;
        ent.Comp.ActiveUntil = _timing.CurTime + ent.Comp.ActiveTime;
        ent.Comp.NextRetarget = _timing.CurTime;
        ent.Comp.NextHit = _timing.CurTime;
        ent.Comp.CurrentTarget = target;
        ent.Comp.RogueTarget = target;
        ent.Comp.RogueActivator = user ?? ent.Comp.RogueActivator ?? target;

        if (!ReleaseDisc(ent.Owner, ent.Comp.RogueActivator.Value))
        {
            _toggle.TrySetActive((ent.Owner, null), false, null, false);
            return;
        }

        if (TryComp(ent.Owner, out PhysicsComponent? physics))
            SteerDisc(ent.Owner, ent.Comp, physics, ent.Owner, target);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-disc-stolen-activated"), ent.Owner, target, PopupType.MediumCaution);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(target):target} activated stolen Yautja smart disc {ToPrettyString(ent.Owner):disc}");
    }

    private bool TryGetOwner(Entity<YautjaSmartDiscComponent> ent, out EntityUid owner)
    {
        owner = ent.Comp.YautjaOwner ?? default;
        if (owner.IsValid() && !TerminatingOrDeleted(owner))
            return true;

        if (TryGetRogueTarget(ent, out _))
        {
            owner = ent.Owner;
            return true;
        }

        if (TryComp(ent.Owner, out YautjaRecallableComponent? recallable) &&
            recallable.YautjaOwner is { } recallOwner &&
            !TerminatingOrDeleted(recallOwner))
        {
            owner = recallOwner;
            ent.Comp.YautjaOwner = recallOwner;
            return true;
        }

        owner = default;
        return false;
    }

    private bool TryGetOrSetOwner(Entity<YautjaSmartDiscComponent> ent, EntityUid user, out EntityUid owner)
    {
        owner = default;
        if (!HasComp<YautjaComponent>(user))
            return false;

        if (!TryComp(ent.Owner, out YautjaRecallableComponent? recallable))
        {
            ent.Comp.YautjaOwner = user;
            owner = user;
            return true;
        }

        if (recallable.YautjaOwner is { } existing && existing != user)
        {
            owner = existing;
            return true;
        }

        recallable.YautjaOwner = user;
        Dirty(ent.Owner, recallable);
        ent.Comp.YautjaOwner = user;
        owner = user;
        return true;
    }

    private void DropFromHands(EntityUid disc, EntityUid owner)
    {
        if (!TryComp(owner, out HandsComponent? hands) || !_hands.IsHolding((owner, hands), disc, out _))
            return;

        _hands.TryDrop((owner, hands), disc, checkActionBlocker: false, doDropInteraction: false);
    }

    private bool ReleaseDisc(EntityUid disc, EntityUid user)
    {
        DropFromHands(disc, user);

        if (_containers.IsEntityInContainer(disc))
            _containers.TryRemoveFromContainer(disc, true);

        return !_containers.IsEntityInContainer(disc);
    }
}
