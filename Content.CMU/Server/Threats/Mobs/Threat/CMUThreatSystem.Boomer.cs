using System.Numerics;
using Content.Shared.CMU14.Threats.Mobs.Threat;
using Content.Shared.Mobs.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server.CMU14.Threats.Mobs.Threat;

public sealed partial class CMUThreatSystem
{
    private void OnBoomerAction(Entity<CMUBoomerThreatComponent> ent, ref CMUBoomerVomitActionEvent args)
    {
        if (args.Handled || !TryStartBoomerVomit(ent, args.Target, ent.Comp))
            return;
        args.Handled = true;
    }

    private bool TryStartBoomerVomit(EntityUid uid, EntityUid target, CMUBoomerThreatComponent boomer)
    {
        if (_timing.CurTime < boomer.NextUseAt || !CanTarget(uid, target, boomer.Range))
            return false;

        var origin = _transform.GetMapCoordinates(uid);
        var targetPosition = _transform.GetMapCoordinates(target);
        var facingVector = targetPosition.Position - origin.Position;
        if (facingVector.LengthSquared() < 0.01f)
            return false;
        var facing = Vector2.Normalize(facingVector);
        var minDot = MathF.Cos(boomer.ConeDegrees * MathF.PI / 360f);
        boomer.NextUseAt = _timing.CurTime + boomer.Cooldown;
        PlayAbilitySound(uid, boomer.Sound);
        SetState(uid, CMUThreatAiState.UsingAbility);

        var victims = new List<EntityUid>();
        foreach (var candidate in _lookup.GetEntitiesInRange<MobStateComponent>(origin, boomer.Range))
        {
            if (!CanTarget(uid, candidate, boomer.Range))
                continue;

            var direction = _transform.GetMapCoordinates(candidate).Position - origin.Position;
            if (direction.LengthSquared() < 0.01f || Vector2.Dot(facing, Vector2.Normalize(direction)) < minDot)
                continue;

            ApplyBile(uid, candidate, boomer.BileDuration, boomer.HordeRadius);
            victims.Add(candidate);
        }

        CommandBiledHorde(uid, victims, boomer.HordeRadius, boomer.Range);

        SetState(uid, CMUThreatAiState.Chasing);
        return true;
    }

    private void ExplodeBoomer(EntityUid uid, CMUBoomerThreatComponent boomer)
    {
        var origin = _transform.GetMapCoordinates(uid);
        switch (_random.Next(3))
        {
            case 0:
                _explosion.QueueExplosion(origin,
                    boomer.IedExplosion,
                    boomer.IedTotalIntensity,
                    boomer.IedIntensitySlope,
                    boomer.IedMaxIntensity,
                    uid,
                    canCreateVacuum: false);
                break;
            case 1:
                Spawn(boomer.AcidCloudPrototype, Transform(uid).Coordinates);
                break;
            default:
                Spawn(boomer.NeurotoxinCloudPrototype, Transform(uid).Coordinates);
                break;
        }
        var victims = new List<EntityUid>();
        foreach (var candidate in _lookup.GetEntitiesInRange<MobStateComponent>(origin, boomer.ExplosionRadius))
        {
            if (!IsValidVictim(uid, candidate))
                continue;

            ApplyBile(uid, candidate, boomer.BileDuration, boomer.HordeRadius);
            victims.Add(candidate);
            if (!TryComp(candidate, out PhysicsComponent? physics))
                continue;

            var away = _transform.GetMapCoordinates(candidate).Position - origin.Position;
            if (away.LengthSquared() > 0.01f)
                _physics.ApplyLinearImpulse(candidate, Vector2.Normalize(away) * boomer.ExplosionKnockback * physics.Mass, body: physics);
        }

        CommandBiledHorde(uid, victims, boomer.HordeRadius, boomer.ExplosionRadius);
    }

    private void ApplyBile(EntityUid source, EntityUid victim, TimeSpan duration, float hordeRadius)
    {
        var bile = EnsureComp<CMUThreatBileComponent>(victim);
        bile.Source = source;
        bile.HordeRadius = hordeRadius;
        bile.ExpiresAt = _timing.CurTime + duration;
        _blur.AddTemporaryBlurModifier(victim, TimeSpan.FromSeconds(4), 1.5f);
    }

    private void CommandBiledHorde(EntityUid source,
        List<EntityUid> victims,
        float hordeRadius,
        float effectRadius)
    {
        if (victims.Count == 0)
            return;

        var victimPositions = new List<(EntityUid Entity, Vector2 Position)>(victims.Count);
        foreach (var victim in victims)
            victimPositions.Add((victim, _transform.GetMapCoordinates(victim).Position));

        var origin = _transform.GetMapCoordinates(source);
        var hordeRadiusSquared = hordeRadius * hordeRadius;
        foreach (var common in _lookup.GetEntitiesInRange<CMUCommonThreatComponent>(origin,
                     hordeRadius + effectRadius))
        {
            if (!TryComp(common, out CMUThreatComponent? threat) || _mobState.IsDead(common))
                continue;

            var commonPosition = _transform.GetMapCoordinates(common).Position;
            EntityUid? nearest = null;
            var nearestDistance = hordeRadiusSquared;
            foreach (var (victim, position) in victimPositions)
            {
                var distance = Vector2.DistanceSquared(commonPosition, position);
                if (distance > nearestDistance)
                    continue;

                nearest = victim;
                nearestDistance = distance;
            }

            if (nearest is { } target)
                SetNpcTarget(common, target, threat);
        }
    }

    private void UpdateBile()
    {
        var query = EntityQueryEnumerator<CMUThreatBileComponent>();
        while (query.MoveNext(out var victim, out var bile))
        {
            if (_timing.CurTime >= bile.ExpiresAt)
                RemCompDeferred<CMUThreatBileComponent>(victim);
        }
    }
}
