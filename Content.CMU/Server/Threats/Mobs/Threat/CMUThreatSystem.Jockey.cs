using Content.Shared.CMU14.Threats.Mobs.Threat;
using System.Numerics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.Server.CMU14.Threats.Mobs.Threat;

public sealed partial class CMUThreatSystem
{
    private void OnJockeyAction(Entity<CMUJockeyThreatComponent> ent, ref CMUJockeyRideActionEvent args)
    {
        if (args.Handled || !TryStartJockeyRide(ent, args.Target, ent.Comp))
            return;
        args.Handled = true;
    }

    private bool TryStartJockeyRide(EntityUid uid, EntityUid target, CMUJockeyThreatComponent jockey)
    {
        if (_timing.CurTime < jockey.NextUseAt ||
            HasComp<CMUJockeyLeapingComponent>(uid) ||
            HasComp<CMUThreatPinComponent>(target) ||
            !CanTarget(uid, target, jockey.Range))
            return false;

        if (!TryComp(uid, out PhysicsComponent? physics))
            return false;
        var origin = _transform.GetMapCoordinates(uid);
        var destination = _transform.GetMapCoordinates(target);
        var direction = destination.Position - origin.Position;
        if (direction.LengthSquared() < 0.01f)
            return false;

        jockey.NextUseAt = _timing.CurTime + jockey.Cooldown;
        PlayAbilitySound(uid, jockey.Sound);
        EnsureComp<CMUJockeyLeapingComponent>(uid).EndsAt = _timing.CurTime + jockey.LeapDuration;
        var speed = MathF.Min(jockey.LeapStrength,
            direction.Length() / MathF.Max(0.1f, (float) jockey.LeapDuration.TotalSeconds) + 0.5f);
        _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
        _physics.ApplyLinearImpulse(uid, Vector2.Normalize(direction) * speed * physics.Mass, body: physics);
        _physics.SetBodyStatus(uid, physics, BodyStatus.InAir);
        SetState(uid, CMUThreatAiState.UsingAbility);

        foreach (var contact in _physics.GetContactingEntities(uid, physics))
        {
            if (TryJockeyHit(uid, contact, jockey))
                break;
        }
        return true;
    }

    private void UpdateJockeyLeaps()
    {
        var query = EntityQueryEnumerator<CMUJockeyLeapingComponent>();
        while (query.MoveNext(out var uid, out var leap))
        {
            if (_timing.CurTime >= leap.EndsAt)
                RemCompDeferred<CMUJockeyLeapingComponent>(uid);
        }
    }

    private void OnJockeyCollide(Entity<CMUJockeyLeapingComponent> ent, ref StartCollideEvent args)
    {
        if (TryComp(ent, out CMUJockeyThreatComponent? jockey))
            TryJockeyHit(ent, args.OtherEntity, jockey);
    }

    private bool TryJockeyHit(EntityUid uid, EntityUid target, CMUJockeyThreatComponent jockey)
    {
        if (!IsValidVictim(uid, target) || HasComp<CMUThreatPinComponent>(target))
            return false;

        StartPin(uid, target, CMUThreatPinKind.Jockey, jockey.RideDamage,
            jockey.DamageInterval, jockey.MaxPinDuration, jockey.PullSpeed,
            1f, false, jockey.WalkModifier, jockey.SprintModifier);
        RemCompDeferred<CMUJockeyLeapingComponent>(uid);
        return true;
    }

    private void OnJockeySleep(Entity<CMUJockeyLeapingComponent> ent, ref PhysicsSleepEvent args)
    {
        RemCompDeferred<CMUJockeyLeapingComponent>(ent);
    }

    private void OnJockeyLeapRemove(Entity<CMUJockeyLeapingComponent> ent, ref ComponentRemove args)
    {
        if (!TryComp(ent, out PhysicsComponent? physics))
            return;
        _physics.SetLinearVelocity(ent, Vector2.Zero, body: physics);
        _physics.SetBodyStatus(ent, physics, BodyStatus.OnGround);
        if (TryComp(ent, out CMUThreatComponent? threat) && threat.State == CMUThreatAiState.UsingAbility)
            SetState(ent, CMUThreatAiState.Chasing);
    }
}
