using System.Numerics;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._CMU14.Threats.Mobs.Xeno.ZJump;

public sealed partial class CMUXenoZJumpSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    public const float ZJumpTakeoffLocalPosition = 0.25f;
    private const float LightTakeoffDashMaxDistance = 1f;
    private const float LightTakeoffDashMaxSpeed = 5f;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUXenoZJumpComponent, MapInitEvent>(OnZJumpMapInit);
        SubscribeLocalEvent<CMUXenoZJumpComponent, CMUXenoZJumpActionEvent>(OnZJumpAction);
        SubscribeLocalEvent<CMUXenoZJumpComponent, CMUXenoZJumpDoAfterEvent>(OnZJumpDoAfter);
    }

    private void OnZJumpMapInit(Entity<CMUXenoZJumpComponent> xeno, ref MapInitEvent args)
    {
        if (!HasComp<XenoComponent>(xeno)
            || xeno.Comp.Action != null
            || _actions.AddAction(xeno, xeno.Comp.ActionId) is not { } action)
            return;

        xeno.Comp.Action = action;
        Dirty(xeno);
    }

    private void OnZJumpAction(Entity<CMUXenoZJumpComponent> xeno, ref CMUXenoZJumpActionEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<XenoComponent>(xeno))
        {
            _popup.PopupClient(Loc.GetString(xeno.Comp.NotXenoPopup), xeno, xeno, PopupType.MediumCaution);
            return;
        }

        if (!CanStartZJumpWindup(xeno, args.Target, xeno.Comp))
            return;

        if (!_xenoPlasma.HasPlasmaPopup((xeno.Owner, null), xeno.Comp.PlasmaCost))
            return;

        var ev = new CMUXenoZJumpDoAfterEvent(GetNetCoordinates(args.Target));
        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.Windup, ev, xeno, args.Action)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DamageThreshold = FixedPoint2.New(10),
            DuplicateCondition = DuplicateConditions.SameEvent,
            Hidden = true
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        args.Handled = true;
    }

    private void OnZJumpDoAfter(Entity<CMUXenoZJumpComponent> xeno, ref CMUXenoZJumpDoAfterEvent args)
    {
        if (args.Handled)
            return;

        if (args.Cancelled)
        {
            _popup.PopupClient(Loc.GetString(xeno.Comp.CancelledPopup), xeno, xeno);
            return;
        }

        if (TryDoZJump(xeno, args.Coordinates))
            args.Handled = true;
    }

    private bool TryDoZJump(Entity<CMUXenoZJumpComponent> xeno, NetCoordinates targetCoordinates)
    {
        if (!TryComp(xeno, out CMUZPhysicsComponent? zPhysics))
        {
            _popup.PopupClient(Loc.GetString(xeno.Comp.NoZPhysicsPopup), xeno, xeno, PopupType.MediumCaution);
            return false;
        }

        if (!TryComp(xeno, out PhysicsComponent? physics))
            return false;

        if (!CanStartZJumpWindup(xeno, GetCoordinates(targetCoordinates), xeno.Comp))
            return false;

        MapCoordinates origin = _transform.GetMapCoordinates(xeno);
        var target = _transform.ToMapCoordinates(targetCoordinates);
        Vector2 direction = CMUXenoZJumpSystem.ClampJumpVector(target.Position - origin.Position, xeno.Comp.Range);
        if (direction == Vector2.Zero)
            return false;

        if (!_xenoPlasma.TryRemovePlasmaPopup((xeno.Owner, null), xeno.Comp.PlasmaCost))
            return false;

        _physics.ResetDynamics(xeno, physics);

        Vector2 takeoffDash = CMUXenoZJumpSystem.GetTakeoffDashVector(direction, xeno.Comp.TakeoffDashDistance);

        _throwing.TryThrow(xeno,
            takeoffDash,
            xeno.Comp.TakeoffDashSpeed,
            xeno,
            animated: false,
            compensateFriction: true);

        if (CMUXenoZJumpSystem.ShouldSetInAirForUpwardMomentum(xeno.Comp.ZVelocity))
        {
            _physics.SetBodyStatus(xeno, physics, BodyStatus.InAir);
            if (CMUXenoZJumpSystem.ShouldRaiseZJumpTakeoffLocalPosition(xeno.Comp.ZVelocity, zPhysics.LocalPosition))
                _zLevels.SetZLocalPosition((xeno.Owner, zPhysics), ZJumpTakeoffLocalPosition);

            _zLevels.SetZVelocity((xeno.Owner, zPhysics), xeno.Comp.ZVelocity);
        }

        return true;
    }

    private bool CanStartZJumpWindup(Entity<CMUXenoZJumpComponent> xeno,
        EntityCoordinates targetCoordinates,
        CMUXenoZJumpComponent component)
    {
        if (!TryComp<CMUZPhysicsComponent>(xeno, out _))
        {
            _popup.PopupClient(Loc.GetString(component.NoZPhysicsPopup), xeno, xeno, PopupType.MediumCaution);
            return false;
        }

        if (!TryComp<PhysicsComponent>(xeno, out _))
            return false;

        TransformComponent xform = Transform(xeno);
        var hasZMap = false;
        var hasMapAbove = false;
        if (xform.MapUid is { } mapUid && TryComp(mapUid, out CMUZLevelMapComponent? zMap))
        {
            hasZMap = true;
            hasMapAbove = _zLevels.TryMapUp((mapUid, zMap), out _);
        }

        bool canUseZJumpMap = CMUXenoZJumpSystem.CanUseZJumpMap(hasZMap, hasMapAbove);
        if (!canUseZJumpMap || !CMUXenoZJumpSystem.CanStartZJumpTakeoff(canUseZJumpMap, _zLevels.HasTileAbove(xeno)))
        {
            _popup.PopupClient(Loc.GetString(component.NoZPhysicsPopup), xeno, xeno, PopupType.MediumCaution);
            return false;
        }

        MapCoordinates origin = _transform.GetMapCoordinates(xeno);
        var target = _transform.ToMapCoordinates(targetCoordinates);
        if (origin.MapId != target.MapId)
            return false;

        Vector2 direction = CMUXenoZJumpSystem.ClampJumpVector(target.Position - origin.Position, component.Range);
        return direction != Vector2.Zero;
    }

    public static Vector2 ClampJumpVector(Vector2 vector, float range)
    {
        if (vector == Vector2.Zero || range <= 0f)
            return Vector2.Zero;

        float length = vector.Length();
        if (length <= range)
            return vector;

        return vector / length * range;
    }

    public static bool HasEnoughVerticalVelocity(float velocity, float requiredVelocity)
        => velocity >= requiredVelocity;

    public static bool ShouldSetInAirForUpwardMomentum(float zVelocity) => zVelocity > 0f;

    public static bool ShouldRaiseZJumpTakeoffLocalPosition(float zVelocity, float localPosition)
        => zVelocity > 0f && localPosition < ZJumpTakeoffLocalPosition;

    public static bool CanUseZJumpMap(bool hasZMap, bool hasMapAbove) => hasZMap && hasMapAbove;

    public static Vector2 GetTakeoffDashVector(Vector2 vector, float distance)
        => CMUXenoZJumpSystem.ClampJumpVector(vector, distance);

    public static bool IsLightTakeoffDash(float distance, float speed) => distance > 0f
        && distance <= LightTakeoffDashMaxDistance
        && speed > 0f
        && speed <= LightTakeoffDashMaxSpeed;

    public static bool CanStartZJumpTakeoff(bool canUseZJumpMap, bool hasBlockingTileAbove)
        => canUseZJumpMap && !hasBlockingTileAbove;

    public static bool ShouldUseWindupDoAfter(TimeSpan windup) => windup > TimeSpan.Zero;
}
