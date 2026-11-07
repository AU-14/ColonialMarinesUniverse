using System.Numerics;
using Content.Shared.CMU14.Threats.Mobs.Threat;
using Content.Shared._RMC14.Xenonids.Leap;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.Server.CMU14.Threats.Mobs.Threat;

public sealed partial class CMUThreatSystem
{
    private void OnHunterLeapHit(Entity<CMUHunterThreatComponent> ent, ref XenoLeapHitEvent args)
    {
        TryHunterHit(ent, args.Hit, ent.Comp);
    }

    private bool TryStartHunterPounce(EntityUid uid, EntityUid target, CMUHunterThreatComponent hunter)
    {
        if (_timing.CurTime < hunter.NextUseAt ||
            hunter.PendingTarget != null ||
            HasComp<CMUHunterPouncingComponent>(uid) ||
            HasComp<CMUThreatPinComponent>(target) ||
            !CanTarget(uid, target, hunter.Range))
            return false;

        hunter.PendingTarget = target;
        hunter.PendingUseAt = _timing.CurTime + hunter.TelegraphTime;
        hunter.NextUseAt = _timing.CurTime + hunter.Cooldown;
        SetState(uid, CMUThreatAiState.Telegraphing);
        PlayAbilitySound(uid, hunter.Sound);
        if (hunter.TelegraphPrototype is { } effect)
            Spawn(effect, Transform(uid).Coordinates);
        return true;
    }

    private void UpdateHunterPounces()
    {
        var hunters = EntityQueryEnumerator<CMUHunterThreatComponent>();
        while (hunters.MoveNext(out var uid, out var hunter))
        {
            if (hunter.PendingTarget is not { } target || _timing.CurTime < hunter.PendingUseAt)
                continue;

            hunter.PendingTarget = null;
            if (!CanTarget(uid, target, hunter.Range))
            {
                SetState(uid, CMUThreatAiState.Chasing);
                continue;
            }
            LaunchHunter(uid, target, hunter);
        }

        var pounces = EntityQueryEnumerator<CMUHunterPouncingComponent>();
        while (pounces.MoveNext(out var uid, out var pounce))
        {
            if (_timing.CurTime >= pounce.EndsAt)
                RemCompDeferred<CMUHunterPouncingComponent>(uid);
        }
    }

    private void LaunchHunter(EntityUid uid, EntityUid target, CMUHunterThreatComponent hunter)
    {
        if (!TryComp(uid, out PhysicsComponent? physics))
            return;

        var origin = _transform.GetMapCoordinates(uid);
        var destination = _transform.GetMapCoordinates(target);
        var direction = destination.Position - origin.Position;
        if (direction.LengthSquared() < 0.01f)
            return;

        var pouncing = EnsureComp<CMUHunterPouncingComponent>(uid);
        pouncing.EndsAt = _timing.CurTime + hunter.FlightDuration;
        var speed = MathF.Min(hunter.Strength,
            direction.Length() / MathF.Max(0.1f, (float) hunter.FlightDuration.TotalSeconds) + 0.5f);
        _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
        _physics.ApplyLinearImpulse(uid, Vector2.Normalize(direction) * speed * physics.Mass, body: physics);
        _physics.SetBodyStatus(uid, physics, BodyStatus.InAir);
        SetState(uid, CMUThreatAiState.UsingAbility);

        foreach (var contact in _physics.GetContactingEntities(uid, physics))
        {
            if (TryHunterHit(uid, contact, hunter))
                break;
        }
    }

    private void OnHunterCollide(Entity<CMUHunterPouncingComponent> ent, ref StartCollideEvent args)
    {
        if (TryComp(ent, out CMUHunterThreatComponent? hunter))
            TryHunterHit(ent, args.OtherEntity, hunter);
    }

    private bool TryHunterHit(EntityUid uid, EntityUid target, CMUHunterThreatComponent hunter)
    {
        if (!IsValidVictim(uid, target) || HasComp<CMUThreatPinComponent>(target))
            return false;

        if (TryComp(uid, out CMUThreatComponent? threat))
            TryDelimb(uid, target, threat.DelimbChance);
        _damage.TryChangeDamage(target, hunter.ImpactDamage, origin: uid);
        _stun.TryParalyze(target, TimeSpan.FromSeconds(15), true);
        StartPin(uid, target, CMUThreatPinKind.Hunter, hunter.PinDamage,
            hunter.PinDamageInterval, hunter.MaxPinDuration, 0f, 1f, true);
        StopMover(uid);
        RemCompDeferred<CMUHunterPouncingComponent>(uid);
        return true;
    }

    private void OnHunterSleep(Entity<CMUHunterPouncingComponent> ent, ref PhysicsSleepEvent args)
    {
        RemCompDeferred<CMUHunterPouncingComponent>(ent);
    }

    private void OnHunterPounceRemove(Entity<CMUHunterPouncingComponent> ent, ref ComponentRemove args)
    {
        if (!TryComp(ent, out PhysicsComponent? physics))
            return;
        _physics.SetLinearVelocity(ent, Vector2.Zero, body: physics);
        _physics.SetBodyStatus(ent, physics, BodyStatus.OnGround);
        if (TryComp(ent, out CMUThreatComponent? threat) && threat.State == CMUThreatAiState.UsingAbility)
            SetState(ent, CMUThreatAiState.Chasing);
    }
}
