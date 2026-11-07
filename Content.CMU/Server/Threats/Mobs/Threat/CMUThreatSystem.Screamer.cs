using System.Numerics;
using Content.Shared.CMU14.Threats.Mobs.Threat;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Threats.Mobs.Threat;

public sealed partial class CMUThreatSystem
{
    private void OnScreamerDamaged(Entity<CMUScreamerThreatComponent> ent, ref Content.Shared.Damage.Systems.DamageChangedEvent args)
    {
        if (!args.DamageIncreased || _mobState.IsDead(ent))
            return;

        var target = args.Origin is { } origin && IsValidVictim(ent, origin)
            ? origin
            : FindNearestVictim(ent, ent.Comp.HordeTargetRadius, humanOnly: true);
        if (target is { } victim)
            TryStartScreamerHorde(ent, victim, ent.Comp);
    }

    private bool TryStartScreamerHorde(EntityUid uid, EntityUid target, CMUScreamerThreatComponent screamer)
    {
        if (_timing.CurTime < screamer.NextUseAt ||
            screamer.PendingTarget != null ||
            !IsValidVictim(uid, target))
            return false;

        screamer.NextUseAt = _timing.CurTime + screamer.Cooldown;
        screamer.PendingUseAt = _timing.CurTime + screamer.TelegraphTime;
        screamer.PendingTarget = target;
        SetState(uid, CMUThreatAiState.Telegraphing);
        PlayAbilitySound(uid, screamer.Sound);
        return true;
    }

    private void UpdateScreamers()
    {
        var query = EntityQueryEnumerator<CMUScreamerThreatComponent>();
        while (query.MoveNext(out var uid, out var screamer))
        {
            if (screamer.PendingTarget is not { } target || _timing.CurTime < screamer.PendingUseAt)
                continue;

            screamer.PendingTarget = null;
            if (_mobState.IsDead(uid) || !IsValidVictim(uid, target))
            {
                SetState(uid, CMUThreatAiState.Chasing);
                continue;
            }

            SummonHorde(uid, target, screamer);
            SetState(uid, CMUThreatAiState.Chasing);
        }
    }

    private void SummonHorde(EntityUid uid, EntityUid target, CMUScreamerThreatComponent screamer)
    {
        var center = _transform.GetMapCoordinates(uid);
        for (var i = 0; i < screamer.HordeCount; i++)
        {
            var angle = _random.NextFloat(0f, MathF.Tau);
            var radius = _random.NextFloat(1f, screamer.SpawnRadius);
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            var spawnCoordinates = new MapCoordinates(center.Position + offset, center.MapId);
            if (!_interaction.InRangeUnobstructed(center, spawnCoordinates, screamer.SpawnRadius + 0.5f))
                continue;

            if (screamer.HordePrototypes.Count == 0)
                break;

            var threat = Spawn(screamer.HordePrototypes[_random.Next(screamer.HordePrototypes.Count)], spawnCoordinates);
            var emerging = EnsureComp<CMUThreatEmergingComponent>(threat);
            emerging.Duration = screamer.EmergenceDuration;
            emerging.EndsAt = _timing.CurTime + screamer.EmergenceDuration;
            emerging.Target = FindNearestVictim(threat, screamer.HordeTargetRadius, humanOnly: true) ?? target;
            Dirty(threat, emerging);
            _npc.SleepNPC(threat);
            SetState(threat, CMUThreatAiState.Emerging);
            if (screamer.EmergenceEffectPrototype is { } effect)
                Spawn(effect, spawnCoordinates);
        }
    }

    private void UpdateEmerging()
    {
        var query = EntityQueryEnumerator<CMUThreatEmergingComponent>();
        while (query.MoveNext(out var uid, out var emerging))
        {
            if (_timing.CurTime < emerging.EndsAt)
                continue;

            RemCompDeferred<CMUThreatEmergingComponent>(uid);
            if (_mobState.IsDead(uid))
                continue;

            _npc.WakeNPC(uid);
            if (TryComp(uid, out CMUThreatComponent? threat))
            {
                var target = emerging.Target is { } assigned && IsValidVictim(uid, assigned)
                    ? assigned
                    : TryFindTarget(uid, threat);
                if (target is { } victim)
                    SetNpcTarget(uid, victim, threat);
                else
                    SetState(uid, CMUThreatAiState.Idle);
            }
        }
    }
}
