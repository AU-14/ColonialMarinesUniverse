using System.Numerics;
using Content.Shared._RMC14.Atmos;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.DroneOperator;

public sealed partial class CMUCombatDroneSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private bool _relativeMovement;

    public override void Initialize()
    {
        Subs.CVar(_config, CCVars.RelativeMovement, value => _relativeMovement = value, true);
        SubscribeLocalEvent<CMUCombatDroneComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<CMUCombatDroneComponent, ChangeDirectionAttemptEvent>(OnChangeDirectionAttempt);
        SubscribeLocalEvent<CMUCombatDroneComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<CMUCombatDroneComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUCombatDroneComponent, RMCIgniteAttemptEvent>(OnIgniteAttempt);
        SubscribeLocalEvent<CMUCombatDroneComponent, RMCGetFireImmunityEvent>(OnGetFireImmunity);
        SubscribeAllEvent<CMUCombatDroneAimEvent>(OnAim);
    }

    private void OnAim(CMUCombatDroneAimEvent args, EntitySessionEventArgs session)
    {
        if (_timing.ApplyingState || !double.IsFinite(args.Angle.Theta) ||
            session.SenderSession.AttachedEntity is not { } uid ||
            !TryComp<CMUCombatDroneComponent>(uid, out var drone) || drone.Wrecked ||
            !_mobState.IsAlive(uid) || (_net.IsServer && !HasComp<CMUDroneControlSessionComponent>(uid)) ||
            drone.TurretVisual is not { } turret || TerminatingOrDeleted(turret))
            return;

        _transform.SetWorldRotation(turret, ClampAim(_transform.GetWorldRotation(uid), args.Angle, drone.FireArcDegrees));
    }

    public static Angle ClampAim(Angle heading, Angle aim, float arcDegrees)
    {
        var halfArc = Math.Clamp(arcDegrees, 0, 360) / 2;
        return heading + Angle.FromDegrees(Math.Clamp(Angle.ShortestDistance(heading, aim).Degrees, -halfArc, halfArc));
    }

    private void OnChangeDirectionAttempt(Entity<CMUCombatDroneComponent> ent, ref ChangeDirectionAttemptEvent args)
    {
        // Mouse aiming must not turn the chassis and bypass its forward firing arc.
        args.Cancel();
    }

    private void OnMoveInput(Entity<CMUCombatDroneComponent> ent, ref MoveInputEvent args)
    {
        // InputMover also raises this event while applying its replicated state. The
        // server's Transform states already carry both rotations in that case.
        if (_timing.ApplyingState || !args.HasDirectionalMovement || !args.Entity.Comp.CanMove || !_mobState.IsAlive(ent))
            return;

        var direction = _mover.DirVecForButtons(args.Entity.Comp.HeldMoveButtons);
        if (direction.LengthSquared() < 0.001f)
            return;

        if (_relativeMovement)
            direction = _mover.GetParentGridAngle(args.Entity.Comp).RotateVec(direction);
        _transform.SetWorldRotation(ent, Angle.FromWorldVec(direction).GetCardinalDir().ToAngle());
        if (ent.Comp.TurretVisual is { } turret && !TerminatingOrDeleted(turret))
            _transform.SetLocalRotation(turret, Angle.Zero);
    }

    /// <summary>Checks the cursor bearing against the chassis, independently of gun or camera rotation.</summary>
    public static bool IsWithinFireArc(Angle heading, Vector2 displacement, float arcDegrees = 180)
    {
        if (displacement.LengthSquared() < 0.0001f)
            return false;

        var angle = Math.Abs(Angle.ShortestDistance(heading, Angle.FromWorldVec(displacement)).Degrees);
        return angle <= Math.Clamp(arcDegrees, 0, 360) / 2 + 0.001;
    }

    private void OnAttemptShoot(Entity<CMUCombatDroneComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.User != ent.Owner || !_mobState.IsAlive(ent) ||
            (_net.IsServer && !HasComp<CMUDroneControlSessionComponent>(ent)) ||
            args.ToCoordinates is not { } targetCoordinates)
        {
            args.Cancelled = true;
            return;
        }

        var target = _transform.ToMapCoordinates(targetCoordinates);
        var origin = _transform.GetMapCoordinates(ent);
        var direction = target.Position - origin.Position;
        if (target.MapId != origin.MapId || !IsWithinFireArc(_transform.GetWorldRotation(ent), direction, ent.Comp.FireArcDegrees))
        {
            args.Cancelled = true;
            args.ResetCooldown = true;
            args.Message = Loc.GetString("cmu-combat-drone-outside-arc");
            return;
        }

        if (!_timing.ApplyingState && ent.Comp.TurretVisual is { } turret && !TerminatingOrDeleted(turret))
            _transform.SetWorldRotation(turret, Angle.FromWorldVec(direction));
    }

    private void OnIgniteAttempt(Entity<CMUCombatDroneComponent> ent, ref RMCIgniteAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnGetFireImmunity(Entity<CMUCombatDroneComponent> ent, ref RMCGetFireImmunityEvent args)
    {
        // Block fire sources, not Heat damage: acid also uses burn damage.
        args.Immune = true;
        args.Ignite = false;
    }

    private void OnExamined(Entity<CMUCombatDroneComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !TryComp<DamageableComponent>(ent, out var damageable))
            return;

        var damage = _damage.GetAllDamage((ent, damageable));
        var frame = SumDamage(damage, ent.Comp.FrameDamageTypes);
        var wiring = SumDamage(damage, ent.Comp.WiringDamageTypes);
        args.PushMarkup(Loc.GetString(frame > 0
            ? frame >= 100 ? "cmu-combat-drone-frame-buckled" : "cmu-combat-drone-frame-dented"
            : "cmu-combat-drone-frame-intact"));
        args.PushMarkup(Loc.GetString(wiring > 0
            ? wiring >= 100 ? "cmu-combat-drone-wires-charred" : "cmu-combat-drone-wires-burnt"
            : "cmu-combat-drone-wires-intact"));
        if (ent.Comp.Wrecked)
            args.PushMarkup(Loc.GetString("cmu-combat-drone-wreck-examine"));
        else if (!_mobState.IsAlive(ent))
            args.PushMarkup(Loc.GetString("cmu-combat-drone-disabled"));
    }

    public static FixedPoint2 SumDamage(DamageSpecifier damage, IEnumerable<ProtoId<DamageTypePrototype>> types)
    {
        var total = FixedPoint2.Zero;
        foreach (var type in types)
            total += damage.DamageDict.GetValueOrDefault(type);
        return total;
    }

    /// <summary>Repairs only the selected subsystem, capped at the tool's total repair budget.</summary>
    public static DamageSpecifier GetRepair(DamageSpecifier damage, IEnumerable<ProtoId<DamageTypePrototype>> types, FixedPoint2 budget)
    {
        var repair = new DamageSpecifier();
        foreach (var type in types)
        {
            var amount = FixedPoint2.Min(damage.DamageDict.GetValueOrDefault(type), budget);
            if (amount <= 0)
                continue;
            repair.DamageDict[type] = -amount;
            budget -= amount;
        }
        return repair;
    }
}
