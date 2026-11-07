using System.Numerics;
using Content.Server.NPC.HTN;
using Content.Shared.CMU14.Threats.Mobs.Threat;
using Content.Shared._RMC14.Tackle;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Stunnable;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Threats.Mobs.Threat;

public sealed partial class CMUThreatSystem
{
    private static readonly Vector2 JockeyRideOffset = new(0f, 0.45f);
    private static readonly TimeSpan TongueUpdateInterval = TimeSpan.FromSeconds(0.5);
    private const float TongueUpdateDistanceSquared = 0.25f;

    private void StartPin(
        EntityUid attacker,
        EntityUid victim,
        CMUThreatPinKind kind,
        DamageSpecifier damage,
        TimeSpan interval,
        TimeSpan duration,
        float pullSpeed,
        float holdDistance,
        bool paralyze,
        float walkModifier = 1f,
        float sprintModifier = 1f,
        bool expires = true)
    {
        if (HasComp<CMUThreatPinComponent>(victim))
            RemComp<CMUThreatPinComponent>(victim);

        var pin = EnsureComp<CMUThreatPinComponent>(victim);
        pin.Attacker = attacker;
        pin.Kind = kind;
        pin.Damage = damage;
        pin.DamageInterval = interval;
        pin.EndsAt = _timing.CurTime + duration;
        pin.Expires = expires;
        pin.NextDamageAt = _timing.CurTime + interval;
        pin.PullSpeed = pullSpeed;
        pin.HoldDistance = holdDistance;
        pin.BreakDistance = MathF.Max(holdDistance + 5f, 7f);
        pin.ParalyzeVictim = paralyze;
        pin.WalkModifier = walkModifier;
        pin.SprintModifier = sprintModifier;
        if (kind == CMUThreatPinKind.Smoker)
            UpdateTongue(victim, pin);
        else if (kind == CMUThreatPinKind.Jockey)
        {
            if (TryComp(attacker, out PhysicsComponent? physics))
                pin.AttackerCanCollide = physics.CanCollide;
            AttachJockey(attacker, victim);
        }
        _movementSpeed.RefreshMovementSpeedModifiers(victim);
        _actionBlocker.UpdateCanMove(victim);
        SetState(attacker, CMUThreatAiState.Pinning);
        if (TryComp(attacker, out HTNComponent? htn))
            PauseNpcPlan(attacker, htn);
        if (kind is CMUThreatPinKind.Hunter or CMUThreatPinKind.Smoker)
        {
            _actionBlocker.UpdateCanMove(attacker);
            StopMover(attacker);
        }
    }

    private void UpdatePins()
    {
        var query = EntityQueryEnumerator<CMUThreatPinComponent>();
        while (query.MoveNext(out var victim, out var pin))
        {
            var attacker = pin.Attacker;
            if ((pin.Expires && _timing.CurTime >= pin.EndsAt) ||
                !IsValidVictim(attacker, victim) ||
                !Exists(attacker) ||
                _mobState.IsDead(attacker) ||
                (pin.Kind != CMUThreatPinKind.Hunter && HasComp<StunnedComponent>(attacker)) ||
                (pin.Kind != CMUThreatPinKind.Hunter && HasComp<KnockedDownComponent>(attacker)) ||
                !TryDistance(attacker, victim, out var distance) ||
                distance > pin.BreakDistance)
            {
                RemCompDeferred<CMUThreatPinComponent>(victim);
                continue;
            }

            if (pin.Kind == CMUThreatPinKind.Jockey)
            {
                MaintainJockeyAttachment(attacker, victim);
                SteerJockeyVictim(attacker, victim, pin);
            }
            else if (pin.Kind == CMUThreatPinKind.Hunter)
            {
                StopMover(attacker);
            }
            else
            {
                if (pin.Kind == CMUThreatPinKind.Smoker)
                {
                    StopMover(attacker);
                    if (!_interaction.InRangeUnobstructed(attacker, victim, pin.BreakDistance))
                    {
                        RemCompDeferred<CMUThreatPinComponent>(victim);
                        continue;
                    }
                    UpdateTongue(victim, pin);
                }
                if (distance > pin.HoldDistance)
                    PullTowards(victim, attacker, pin.PullSpeed);
                if (pin.ParalyzeVictim)
                    _stun.TryParalyze(victim, TimeSpan.FromSeconds(0.4), true);
            }

            if (_timing.CurTime < pin.NextDamageAt)
                continue;

            pin.NextDamageAt = _timing.CurTime + pin.DamageInterval;
            if (TryComp(attacker, out CMUThreatComponent? threat))
                TryDelimb(attacker, victim, threat.DelimbChance);
            _damage.TryChangeDamage(victim, pin.Damage, origin: attacker);
        }
    }

    private void PullTowards(EntityUid mover, EntityUid destination, float speed)
    {
        if (!TryComp(mover, out PhysicsComponent? physics))
            return;

        var from = _transform.GetMapCoordinates(mover);
        var to = _transform.GetMapCoordinates(destination);
        var direction = to.Position - from.Position;
        if (direction.LengthSquared() < 0.01f)
            return;

        _physics.ApplyLinearImpulse(mover, Vector2.Normalize(direction) * speed * physics.Mass, body: physics);
    }

    private void AttachJockey(EntityUid rider, EntityUid victim)
    {
        if (TryComp(rider, out PhysicsComponent? physics))
            _physics.SetCanCollide(rider, false, body: physics);
        _transform.SetParent(rider, victim);
        _transform.SetLocalPosition(rider, JockeyRideOffset);
        _transform.SetLocalRotation(rider, Angle.Zero);
        _rmcSprite.SetRenderOrder(rider, 100);
        StopMover(rider);
    }

    private void MaintainJockeyAttachment(EntityUid rider, EntityUid victim)
    {
        var xform = Transform(rider);
        if (xform.ParentUid != victim)
            _transform.SetParent(rider, victim);
        if (xform.LocalPosition != JockeyRideOffset)
            _transform.SetLocalPosition(rider, JockeyRideOffset, xform);
        if (xform.LocalRotation != Angle.Zero)
            _transform.SetLocalRotation(rider, Angle.Zero, xform);
        if (TryComp(rider, out PhysicsComponent? physics) && physics.CanCollide)
            _physics.SetCanCollide(rider, false, body: physics);
        StopMover(rider);
    }

    private void SteerJockeyVictim(EntityUid rider, EntityUid victim, CMUThreatPinComponent pin)
    {
        if (!TryComp(victim, out PhysicsComponent? physics))
            return;

        if (HasComp<ActorComponent>(rider) && TryComp(rider, out InputMoverComponent? mover))
        {
            var direction = DirectionFromButtons(mover.HeldMoveButtons);
            _physics.SetLinearVelocity(victim, direction * pin.PullSpeed, body: physics);
            return;
        }

        if (_timing.CurTime >= pin.NextSteerAt || pin.SteerDirection.LengthSquared() < 0.01f)
        {
            var angle = _random.NextFloat(0f, MathF.Tau);
            pin.SteerDirection = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            pin.NextSteerAt = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(0.75f, 1.75f));
        }

        _physics.SetLinearVelocity(victim, pin.SteerDirection * pin.PullSpeed, body: physics);
    }

    private static Vector2 DirectionFromButtons(MoveButtons buttons)
    {
        var direction = Vector2.Zero;
        if ((buttons & MoveButtons.Up) != 0)
            direction.Y += 1f;
        if ((buttons & MoveButtons.Down) != 0)
            direction.Y -= 1f;
        if ((buttons & MoveButtons.Right) != 0)
            direction.X += 1f;
        if ((buttons & MoveButtons.Left) != 0)
            direction.X -= 1f;
        return direction.LengthSquared() > 1f ? Vector2.Normalize(direction) : direction;
    }

    private void ReleasePinsByAttacker(EntityUid attacker)
    {
        var query = EntityQueryEnumerator<CMUThreatPinComponent>();
        while (query.MoveNext(out var victim, out var pin))
        {
            if (pin.Attacker == attacker)
                RemCompDeferred<CMUThreatPinComponent>(victim);
        }
    }

    private void OnPinRefreshSpeed(Entity<CMUThreatPinComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.WalkModifier, ent.Comp.SprintModifier);
    }

    private void OnPinUpdateCanMove(Entity<CMUThreatPinComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.Kind == CMUThreatPinKind.Jockey)
            args.Cancel();
    }

    private void OnHunterUpdateCanMove(Entity<CMUHunterThreatComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (TryComp(ent, out CMUThreatComponent? threat) && threat.State == CMUThreatAiState.Pinning)
            args.Cancel();
    }

    private void OnSmokerUpdateCanMove(Entity<CMUSmokerThreatComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (TryComp(ent, out CMUThreatComponent? threat) && threat.State == CMUThreatAiState.Pinning)
            args.Cancel();
    }

    private void OnPinShutdown(Entity<CMUThreatPinComponent> ent, ref ComponentShutdown args)
    {
        _line.DeleteBeam(ent.Comp.TongueSegments);
        if (ent.Comp.Kind == CMUThreatPinKind.Jockey &&
            Exists(ent.Comp.Attacker) &&
            !TerminatingOrDeleted(ent.Comp.Attacker))
        {
            _rmcSprite.SetRenderOrder(ent.Comp.Attacker, 0);
            _transform.AttachToGridOrMap(ent.Comp.Attacker);
            if (TryComp(ent.Comp.Attacker, out PhysicsComponent? physics))
                _physics.SetCanCollide(ent.Comp.Attacker, ent.Comp.AttackerCanCollide, body: physics);
            StopMover(ent.Comp.Attacker);
        }
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        _actionBlocker.UpdateCanMove(ent.Owner);
        if (Exists(ent.Comp.Attacker) &&
            !TerminatingOrDeleted(ent.Comp.Attacker) &&
            !_mobState.IsDead(ent.Comp.Attacker))
        {
            SetState(ent.Comp.Attacker, CMUThreatAiState.Chasing);
            if (ent.Comp.Kind is CMUThreatPinKind.Hunter or CMUThreatPinKind.Smoker)
                _actionBlocker.UpdateCanMove(ent.Comp.Attacker);
            if (TryComp(ent.Comp.Attacker, out CMUThreatComponent? threat) &&
                threat.CurrentTarget is { } target &&
                IsValidCombatTarget(ent.Comp.Attacker, target))
            {
                SetNpcTarget(ent.Comp.Attacker, target, threat);
            }
            else if (TryComp(ent.Comp.Attacker, out HTNComponent? htn))
            {
                ResumeNpcPlan(ent.Comp.Attacker, htn);
            }
        }
    }

    private void UpdateTongue(EntityUid victim, CMUThreatPinComponent pin)
    {
        if (_timing.CurTime < pin.NextTongueUpdate)
            return;

        pin.NextTongueUpdate = _timing.CurTime + TongueUpdateInterval;
        var source = _transform.GetMapCoordinates(pin.Attacker);
        var target = _transform.GetMapCoordinates(victim);
        if (pin.TongueSegments.Count > 0 &&
            Vector2.DistanceSquared(source.Position, pin.LastTongueSource) < TongueUpdateDistanceSquared &&
            Vector2.DistanceSquared(target.Position, pin.LastTongueTarget) < TongueUpdateDistanceSquared)
            return;

        pin.LastTongueSource = source.Position;
        pin.LastTongueTarget = target.Position;
        var difference = target.Position - source.Position;
        if (difference.LengthSquared() > 0.01f && pin.TongueSegments.Count > 0)
        {
            var direction = Vector2.Normalize(difference);
            var segmentCount = Math.Max(1, (int) MathF.Ceiling((difference - direction).Length()));
            if (pin.TongueSegments.Count == segmentCount)
            {
                var position = source.Offset(direction);
                var angle = difference.ToWorldAngle();
                var allSegmentsValid = true;
                foreach (var segment in pin.TongueSegments)
                {
                    if (!Exists(segment))
                    {
                        allSegmentsValid = false;
                        break;
                    }

                    _transform.SetMapCoordinates(segment, position);
                    _transform.SetWorldRotation(segment, angle);
                    position = position.Offset(direction);
                }

                if (allSegmentsValid)
                    return;
            }
        }

        _line.DeleteBeam(pin.TongueSegments);
        if (!_line.TryCreateLine(pin.Attacker, victim, "CMUThreatTongueSegment", out var segments))
            return;

        pin.TongueSegments = segments;
        foreach (var segment in segments)
            EnsureComp<CMUThreatTongueSegmentComponent>(segment).Victim = victim;
    }

    private void OnTongueDamaged(Entity<CMUThreatTongueSegmentComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageIncreased && HasComp<CMUThreatPinComponent>(ent.Comp.Victim))
            RemComp<CMUThreatPinComponent>(ent.Comp.Victim);
    }

    private void OnThreatDisarmed(Entity<CMUThreatComponent> ent, ref CMDisarmEvent args)
    {
        ReleasePinsByAttacker(ent);
        if (HasComp<CMUThreatPinComponent>(ent))
            RemComp<CMUThreatPinComponent>(ent);
    }

    private void OnPinnedVictimDisarmed(Entity<CMUThreatPinComponent> ent, ref CMDisarmEvent args)
    {
        if (args.User != ent.Comp.Attacker)
            RemComp<CMUThreatPinComponent>(ent);
    }
}
