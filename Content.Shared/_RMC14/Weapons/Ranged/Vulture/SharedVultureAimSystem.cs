using System.Numerics;
using Content.Shared._RMC14.Scoping;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Weapons.Ranged.Vulture;

public abstract partial class SharedVultureAimSystem : EntitySystem
{
    [Dependency] protected SharedActionsSystem Actions = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] protected IGameTiming Timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VultureAimComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<VultureAimComponent, VultureScopeForwardActionEvent>(OnScopeForward);
        SubscribeLocalEvent<VultureAimComponent, VultureScopeBackwardActionEvent>(OnScopeBackward);
        SubscribeLocalEvent<VultureAimComponent, VultureScopeLeftActionEvent>(OnScopeLeft);
        SubscribeLocalEvent<VultureAimComponent, VultureScopeRightActionEvent>(OnScopeRight);
        SubscribeLocalEvent<VultureAimComponent, VultureWindageLeftActionEvent>(OnWindageLeft);
        SubscribeLocalEvent<VultureAimComponent, VultureWindageRightActionEvent>(OnWindageRight);
        SubscribeLocalEvent<VultureAimComponent, VultureElevationUpActionEvent>(OnElevationUp);
        SubscribeLocalEvent<VultureAimComponent, VultureElevationDownActionEvent>(OnElevationDown);
        SubscribeLocalEvent<VultureAimComponent, VultureBreathActionEvent>(OnBreath);
        SubscribeLocalEvent<VultureAimComponent, AttemptShootEvent>(OnAttemptShoot, before: [typeof(CMGunSystem)]);
    }

    private void OnGetItemActions(Entity<VultureAimComponent> ent, ref GetItemActionsEvent args)
    {
        args.AddAction(ref ent.Comp.ScopeForwardAction, ent.Comp.ScopeForwardActionId);
        args.AddAction(ref ent.Comp.ScopeBackwardAction, ent.Comp.ScopeBackwardActionId);
        args.AddAction(ref ent.Comp.ScopeLeftAction, ent.Comp.ScopeLeftActionId);
        args.AddAction(ref ent.Comp.ScopeRightAction, ent.Comp.ScopeRightActionId);
        args.AddAction(ref ent.Comp.WindageLeftAction, ent.Comp.WindageLeftActionId);
        args.AddAction(ref ent.Comp.WindageRightAction, ent.Comp.WindageRightActionId);
        args.AddAction(ref ent.Comp.ElevationUpAction, ent.Comp.ElevationUpActionId);
        args.AddAction(ref ent.Comp.ElevationDownAction, ent.Comp.ElevationDownActionId);
        args.AddAction(ref ent.Comp.BreathAction, ent.Comp.BreathActionId);

        if (_net.IsServer)
            Dirty(ent);
    }

    private void OnScopeForward(Entity<VultureAimComponent> ent, ref VultureScopeForwardActionEvent args)
    {
        if (!CanUse(ent, args))
            return;

        if (_net.IsClient)
        {
            args.Handled = true;
            return;
        }

        ent.Comp.ViewForwardOffset = Math.Clamp(ent.Comp.ViewForwardOffset + 1, -ent.Comp.MaxForwardOffset, ent.Comp.MaxForwardOffset);
        UpdateAimCoordinates(ent);
        Dirty(ent);
        args.Handled = true;
    }

    private void OnScopeBackward(Entity<VultureAimComponent> ent, ref VultureScopeBackwardActionEvent args)
    {
        if (!CanUse(ent, args))
            return;

        if (_net.IsClient)
        {
            args.Handled = true;
            return;
        }

        ent.Comp.ViewForwardOffset = Math.Clamp(ent.Comp.ViewForwardOffset - 1, -ent.Comp.MaxForwardOffset, ent.Comp.MaxForwardOffset);
        UpdateAimCoordinates(ent);
        Dirty(ent);
        args.Handled = true;
    }

    private void OnScopeLeft(Entity<VultureAimComponent> ent, ref VultureScopeLeftActionEvent args)
    {
        if (!CanUse(ent, args))
            return;

        if (_net.IsClient)
        {
            args.Handled = true;
            return;
        }

        ent.Comp.ViewSideOffset = Math.Clamp(ent.Comp.ViewSideOffset - 1, -ent.Comp.MaxSideOffset, ent.Comp.MaxSideOffset);
        UpdateAimCoordinates(ent);
        Dirty(ent);
        args.Handled = true;
    }

    private void OnScopeRight(Entity<VultureAimComponent> ent, ref VultureScopeRightActionEvent args)
    {
        if (!CanUse(ent, args))
            return;

        if (_net.IsClient)
        {
            args.Handled = true;
            return;
        }

        ent.Comp.ViewSideOffset = Math.Clamp(ent.Comp.ViewSideOffset + 1, -ent.Comp.MaxSideOffset, ent.Comp.MaxSideOffset);
        UpdateAimCoordinates(ent);
        Dirty(ent);
        args.Handled = true;
    }

    private void OnWindageLeft(Entity<VultureAimComponent> ent, ref VultureWindageLeftActionEvent args)
    {
        if (!CanUse(ent, args))
            return;

        if (_net.IsClient)
        {
            args.Handled = true;
            return;
        }

        ent.Comp.CrosshairAdjust = new Vector2i(
            Math.Clamp(ent.Comp.CrosshairAdjust.X - 1, -ent.Comp.BoxRadius, ent.Comp.BoxRadius),
            ent.Comp.CrosshairAdjust.Y);
        UpdateAimCoordinates(ent);
        Dirty(ent);
        args.Handled = true;
    }

    private void OnWindageRight(Entity<VultureAimComponent> ent, ref VultureWindageRightActionEvent args)
    {
        if (!CanUse(ent, args))
            return;

        if (_net.IsClient)
        {
            args.Handled = true;
            return;
        }

        ent.Comp.CrosshairAdjust = new Vector2i(
            Math.Clamp(ent.Comp.CrosshairAdjust.X + 1, -ent.Comp.BoxRadius, ent.Comp.BoxRadius),
            ent.Comp.CrosshairAdjust.Y);
        UpdateAimCoordinates(ent);
        Dirty(ent);
        args.Handled = true;
    }

    private void OnElevationUp(Entity<VultureAimComponent> ent, ref VultureElevationUpActionEvent args)
    {
        if (!CanUse(ent, args))
            return;

        if (_net.IsClient)
        {
            args.Handled = true;
            return;
        }

        ent.Comp.CrosshairAdjust = new Vector2i(
            ent.Comp.CrosshairAdjust.X,
            Math.Clamp(ent.Comp.CrosshairAdjust.Y + 1, -ent.Comp.BoxRadius, ent.Comp.BoxRadius));
        UpdateAimCoordinates(ent);
        Dirty(ent);
        args.Handled = true;
    }

    private void OnElevationDown(Entity<VultureAimComponent> ent, ref VultureElevationDownActionEvent args)
    {
        if (!CanUse(ent, args))
            return;

        if (_net.IsClient)
        {
            args.Handled = true;
            return;
        }

        ent.Comp.CrosshairAdjust = new Vector2i(
            ent.Comp.CrosshairAdjust.X,
            Math.Clamp(ent.Comp.CrosshairAdjust.Y - 1, -ent.Comp.BoxRadius, ent.Comp.BoxRadius));
        UpdateAimCoordinates(ent);
        Dirty(ent);
        args.Handled = true;
    }

    private void OnBreath(Entity<VultureAimComponent> ent, ref VultureBreathActionEvent args)
    {
        if (!CanUse(ent, args))
            return;

        if (_net.IsClient)
        {
            args.Handled = true;
            return;
        }

        if (ent.Comp.BreathCooldownEndsAt > Timing.CurTime)
        {
            _popup.PopupClient(Loc.GetString("rmc-vulture-breath-cooldown"), args.Performer, args.Performer, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        ent.Comp.BreathEndsAt = Timing.CurTime + ent.Comp.BreathDuration;
        ent.Comp.BreathCooldownEndsAt = Timing.CurTime + ent.Comp.BreathDuration + ent.Comp.BreathCooldown;
        ent.Comp.SwayOffset = Vector2i.Zero;
        ent.Comp.DriftOffset = Vector2.Zero;
        ent.Comp.DriftTarget = Vector2.Zero;
        ent.Comp.NextSwayAt = ent.Comp.BreathEndsAt;
        Actions.SetToggled(ent.Comp.BreathAction, true);
        UpdateAimCoordinates(ent);
        Dirty(ent);
        args.Handled = true;
    }

    private void OnAttemptShoot(Entity<VultureAimComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled ||
            !IsUsingVultureScope(ent, args.User) ||
            !TryGetCrosshairTarget(ent, args.User, out var target))
        {
            return;
        }

        args.ToCoordinates = _transform.ToCoordinates(target);
    }

    private bool TryGetCrosshairTarget(Entity<VultureAimComponent> ent, EntityUid user, out MapCoordinates target)
    {
        if (ent.Comp.CrosshairCoordinates is { } current)
        {
            target = current;
            return true;
        }

        if (!TryGetViewCenter(ent, user, out var center))
        {
            target = default;
            return false;
        }

        var reticle = GetReticleOffset(ent.Comp);

        target = AtPosition(center.Position + reticle, center.MapId);
        return true;
    }

    private bool TryGetViewCenter(Entity<VultureAimComponent> ent, EntityUid user, out MapCoordinates center)
    {
        if (ent.Comp.ViewCenter is { } current)
        {
            center = current;
            return true;
        }

        if (!TryComp(user, out ScopingComponent? scoping) ||
            scoping.Scope is not { } scope ||
            !TryComp(scope, out ScopeComponent? scopeComp))
        {
            center = default;
            return false;
        }

        var userMap = _transform.GetMapCoordinates(user);
        var direction = scopeComp.ScopingDirection ?? ent.Comp.Direction ?? _transform.GetWorldRotation(user).GetCardinalDir();
        var forward = direction.ToVec();
        var side = new Vector2(-forward.Y, forward.X);
        var position = userMap.Position +
                       forward * (ent.Comp.BaseViewDistance + ent.Comp.ViewForwardOffset) +
                       side * ent.Comp.ViewSideOffset;

        center = Centered(position, userMap.MapId);
        return true;
    }

    private bool CanUse(Entity<VultureAimComponent> ent, InstantActionEvent args)
    {
        if (args.Handled)
            return false;

        if (IsUsingVultureScope(ent, args.Performer))
        {
            return true;
        }

        _popup.PopupClient(Loc.GetString("rmc-vulture-must-scope"), args.Performer, args.Performer, PopupType.SmallCaution);
        args.Handled = true;
        return false;
    }

    protected bool IsUsingVultureScope(Entity<VultureAimComponent> ent, EntityUid user)
    {
        if (!TryComp(user, out ScopingComponent? scoping) ||
            scoping.Scope is not { } scope)
        {
            return false;
        }

        if (ent.Comp.RifleScope == scope ||
            ent.Comp.SpotterScope == scope)
        {
            return true;
        }

        return _container.TryGetContainingContainer(scope, out var container) &&
               container.Owner == ent.Owner;
    }

    protected void ResetAim(Entity<VultureAimComponent> ent, EntityUid sniper, EntityUid scope, Direction direction)
    {
        ent.Comp.Sniper = sniper;
        ent.Comp.RifleScope = scope;
        ent.Comp.Direction = direction;
        ent.Comp.ViewForwardOffset = 0;
        ent.Comp.ViewSideOffset = 0;
        ent.Comp.CrosshairAdjust = Vector2i.Zero;
        ent.Comp.SwayOffset = Vector2i.Zero;
        ent.Comp.DriftOffset = Vector2.Zero;
        ent.Comp.DriftTarget = Vector2.Zero;
        ent.Comp.BreathEndsAt = TimeSpan.Zero;
        ent.Comp.BreathCooldownEndsAt = TimeSpan.Zero;
        ent.Comp.NextSwayAt = Timing.CurTime;
        UpdateAimCoordinates(ent);
        Dirty(ent);
    }

    protected void ClearAim(Entity<VultureAimComponent> ent)
    {
        ent.Comp.Sniper = null;
        ent.Comp.RifleScope = null;
        ent.Comp.Spotter = null;
        ent.Comp.SpotterScope = null;
        ent.Comp.SpotterTripod = null;
        ent.Comp.Direction = null;
        ent.Comp.ViewCenter = null;
        ent.Comp.CrosshairCoordinates = null;
        ent.Comp.ViewForwardOffset = 0;
        ent.Comp.ViewSideOffset = 0;
        ent.Comp.CrosshairAdjust = Vector2i.Zero;
        ent.Comp.SwayOffset = Vector2i.Zero;
        ent.Comp.DriftOffset = Vector2.Zero;
        ent.Comp.DriftTarget = Vector2.Zero;
        Actions.SetToggled(ent.Comp.BreathAction, false);
        Dirty(ent);
    }

    protected void ClearSpotter(Entity<VultureAimComponent> ent)
    {
        ent.Comp.Spotter = null;
        ent.Comp.SpotterScope = null;
        ent.Comp.SpotterTripod = null;
        Dirty(ent);
    }

    protected void SetSpotter(Entity<VultureAimComponent> ent, EntityUid spotter, EntityUid scope, EntityUid tripod)
    {
        ent.Comp.Spotter = spotter;
        ent.Comp.SpotterScope = scope;
        ent.Comp.SpotterTripod = tripod;
        Dirty(ent);
    }

    protected void UpdateAimCoordinates(Entity<VultureAimComponent> ent)
    {
        if (ent.Comp.Sniper is not { } sniper || ent.Comp.Direction is not { } direction)
        {
            ent.Comp.ViewCenter = null;
            ent.Comp.CrosshairCoordinates = null;
            return;
        }

        var sniperMap = _transform.GetMapCoordinates(sniper);
        var forward = direction.ToVec();
        var side = new Vector2(-forward.Y, forward.X);
        var center = sniperMap.Position +
                     forward * (ent.Comp.BaseViewDistance + ent.Comp.ViewForwardOffset) +
                     side * ent.Comp.ViewSideOffset;

        ent.Comp.ViewCenter = Centered(center, sniperMap.MapId);

        var reticle = GetReticleOffset(ent.Comp);

        ent.Comp.CrosshairCoordinates = AtPosition(center + reticle, sniperMap.MapId);
    }

    protected static Vector2 GetReticleOffset(VultureAimComponent aim)
    {
        var reticle = new Vector2(
            aim.CrosshairAdjust.X + aim.SwayOffset.X,
            aim.CrosshairAdjust.Y + aim.SwayOffset.Y) + aim.DriftOffset;

        return new Vector2(
            Math.Clamp(reticle.X, -aim.BoxRadius, aim.BoxRadius),
            Math.Clamp(reticle.Y, -aim.BoxRadius, aim.BoxRadius));
    }

    protected static MapCoordinates Centered(Vector2 position, MapId mapId)
    {
        return new MapCoordinates(
            new Vector2(MathF.Floor(position.X) + 0.5f, MathF.Floor(position.Y) + 0.5f),
            mapId);
    }

    protected static MapCoordinates AtPosition(Vector2 position, MapId mapId)
    {
        return new MapCoordinates(position, mapId);
    }
}
