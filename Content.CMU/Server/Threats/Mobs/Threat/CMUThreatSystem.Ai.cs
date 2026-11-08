using System.Numerics;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Components;
using Content.Server.NPC.Pathfinding;
using Content.Shared.CMU14.Threats.Mobs.Threat;
using Content.Shared.Mobs.Components;
using Content.Shared.Humanoid;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Threats.Mobs.Threat;

public sealed partial class CMUThreatSystem
{
    private const float IsolationRadius = 2.5f;
    private static readonly TimeSpan WitchUpdateInterval = TimeSpan.FromSeconds(1);
    private readonly HashSet<Entity<MobStateComponent>> _targetCandidateEntities = new();
    private readonly Dictionary<EntityUid, Vector2> _targetCandidatePositions = new();

    private void UpdateAi()
    {
        var query = EntityQueryEnumerator<CMUThreatComponent>();
        while (query.MoveNext(out var uid, out var threat))
        {
            if (_mobState.IsDead(uid) ||
                HasComp<CMUThreatEmergingComponent>(uid) ||
                HasComp<CMUHunterPouncingComponent>(uid) ||
                HasComp<CMUJockeyLeapingComponent>(uid) ||
                threat.State == CMUThreatAiState.Pinning)
                continue;

            if (TryComp(uid, out CMUWitchThreatComponent? witch) && !witch.Enraged)
            {
                if (_timing.CurTime >= witch.NextUpdateAt)
                {
                    var elapsed = Math.Max(0f, (float) (_timing.CurTime - witch.LastUpdateAt).TotalSeconds);
                    witch.LastUpdateAt = _timing.CurTime;
                    witch.NextUpdateAt = _timing.CurTime + WitchUpdateInterval;
                    UpdateSleepingWitch(uid, witch, elapsed);
                }
                continue;
            }

            if (HasComp<ActorComponent>(uid) || _timing.CurTime < threat.NextAiUpdate)
                continue;

            threat.NextAiUpdate = _timing.CurTime + TimeSpan.FromSeconds(threat.AiUpdateInterval);
            if (UpdateRallyOrder(uid, threat))
                continue;

            var target = TryFindTarget(uid, threat);
            if (target == null)
            {
                ClearNpcTarget(uid, threat);
                continue;
            }

            SetNpcTarget(uid, target.Value, threat);
            TryUseSpecialAi(uid, target.Value);
        }
    }

    private EntityUid? TryFindTarget(EntityUid uid, CMUThreatComponent threat)
    {
        if (TryComp(uid, out CMUWitchThreatComponent? witch) &&
            witch.Enraged &&
            witch.Offender is { } offender &&
            IsValidVictim(uid, offender) &&
            TryDistance(uid, offender, out var offenderDistance) &&
            offenderDistance <= threat.DetectionRadius)
            return offender;

        var origin = _transform.GetMapCoordinates(uid);
        EntityUid? closest = null;
        var bestScore = float.MaxValue;
        var prefersIsolation = HasComp<CMUHunterThreatComponent>(uid) || HasComp<CMUSmokerThreatComponent>(uid);
        var smoker = HasComp<CMUSmokerThreatComponent>(uid);
        EntityUid? biled = null;
        var bileDistance = TryComp(uid, out CMUCommonThreatComponent? common)
            ? common.BilePriorityRadius
            : 0f;
        var searchRadius = MathF.Max(threat.DetectionRadius, bileDistance);

        _targetCandidateEntities.Clear();
        _targetCandidatePositions.Clear();
        _lookup.GetEntitiesInRange(origin,
            searchRadius + (prefersIsolation ? IsolationRadius : 0f),
            _targetCandidateEntities);
        foreach (var candidate in _targetCandidateEntities)
        {
            if (!IsValidVictim(uid, candidate))
                continue;

            _targetCandidatePositions[candidate] = _transform.GetMapCoordinates(candidate).Position;
        }

        foreach (var (candidate, position) in _targetCandidatePositions)
        {
            var distance = Vector2.Distance(origin.Position, position);

            if (distance <= threat.DetectionRadius)
            {
                var score = smoker ? MathF.Abs(distance - 5f) : distance;
                if (prefersIsolation)
                    score += CountNearbySurvivors(candidate, position, IsolationRadius) * 2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    closest = candidate;
                }
            }

            if (common != null &&
                HasComp<CMUThreatBileComponent>(candidate) &&
                distance < bileDistance)
            {
                bileDistance = distance;
                biled = candidate;
            }
        }

        var assigned = threat.CurrentTarget is { } current &&
                       IsValidCombatTarget(uid, current) &&
                       TryDistance(uid, current, out var assignedDistance) &&
                       assignedDistance <= threat.PursuitRadius
            ? current
            : (EntityUid?) null;

        // Living victims always take priority. NavSmash lets the threat destroy any
        // barricade or window that actually blocks its route to that victim.
        var victim = biled ?? closest ?? (assigned is { } assignedTarget && IsValidVictim(uid, assignedTarget)
            ? assignedTarget
            : null);
        if (victim != null)
            return victim;

        foreach (var candidate in _lookup.GetEntitiesInRange<CMUThreatBarricadeTargetComponent>(origin,
                     threat.DetectionRadius))
        {
            if (!IsValidBarricadeTarget(candidate) ||
                !TryDistance(uid, candidate, out var distance) ||
                distance >= bestScore)
                continue;

            bestScore = distance;
            closest = candidate;
        }

        foreach (var candidate in _lookup.GetEntitiesInRange<CMUThreatLightTargetComponent>(origin,
                     threat.DetectionRadius))
        {
            if (!IsValidLightTarget(candidate) ||
                !TryDistance(uid, candidate, out var distance) ||
                distance >= bestScore)
                continue;

            bestScore = distance;
            closest = candidate;
        }

        return closest ?? assigned;
    }

    private int CountNearbySurvivors(EntityUid center, Vector2 centerPosition, float radius)
    {
        var count = 0;
        var radiusSquared = radius * radius;
        foreach (var (candidate, position) in _targetCandidatePositions)
        {
            if (candidate != center && Vector2.DistanceSquared(centerPosition, position) <= radiusSquared)
                count++;
        }
        return count;
    }

    private void SetNpcTarget(EntityUid uid, EntityUid target, CMUThreatComponent threat)
    {
        if (!TryComp(uid, out HTNComponent? htn))
            return;

        var changed = threat.CurrentTarget != target ||
                      !htn.Blackboard.ContainsKey(TargetKey) ||
                      !htn.Blackboard.ContainsKey(TargetCoordinatesKey);
        if (!changed)
        {
            if (!TryComp(uid, out NPCMeleeCombatComponent? existingCombat))
                EnsureComp<NPCMeleeCombatComponent>(uid).Target = target;
            else if (existingCombat.Target != target)
                existingCombat.Target = target;

            SetState(uid, TryComp(uid, out CMUWitchThreatComponent? currentWitch) && currentWitch.Enraged
                ? CMUThreatAiState.Enraged
                : CMUThreatAiState.Chasing);
            if (!_npc.IsAwake(uid, htn))
                _npc.WakeNPC(uid, htn);
            return;
        }

        PauseNpcPlan(uid, htn);

        UntrackTarget(uid, threat.CurrentTarget);
        threat.CurrentTarget = target;
        TrackTarget(uid, target);
        _npc.SetBlackboard(uid, TargetKey, target, htn);
        _npc.SetBlackboard(uid, TargetCoordinatesKey, new EntityCoordinates(target, System.Numerics.Vector2.Zero), htn);
        EnsureComp<NPCMeleeCombatComponent>(uid).Target = target;
        SetState(uid, TryComp(uid, out CMUWitchThreatComponent? witch) && witch.Enraged
            ? CMUThreatAiState.Enraged
            : CMUThreatAiState.Chasing);

        Dirty(uid, threat);
        ResumeNpcPlan(uid, htn);
    }

    private void ClearNpcTarget(EntityUid uid, CMUThreatComponent threat)
    {
        var hadTarget = threat.CurrentTarget != null;
        var hasBlackboardTarget = TryComp(uid, out HTNComponent? htn) &&
                                  (htn.Blackboard.ContainsKey(TargetKey) ||
                                   htn.Blackboard.ContainsKey(TargetCoordinatesKey) ||
                                   htn.Blackboard.ContainsKey(TargetPathfindKey));
        if (!hadTarget && !hasBlackboardTarget)
            return;

        UntrackTarget(uid, threat.CurrentTarget);
        threat.CurrentTarget = null;
        if (hadTarget)
            Dirty(uid, threat);
        if (htn != null)
        {
            PauseNpcPlan(uid, htn);
            htn.Blackboard.Remove<EntityUid>(TargetKey);
            htn.Blackboard.Remove<EntityCoordinates>(TargetCoordinatesKey);
            htn.Blackboard.Remove<PathResultEvent>(TargetPathfindKey);
            ResumeNpcPlan(uid, htn);
        }
        RemComp<NPCMeleeCombatComponent>(uid);
        SetState(uid, CMUThreatAiState.Idle);
    }

    private void TryUseSpecialAi(EntityUid uid, EntityUid target)
    {
        if (!IsValidVictim(uid, target))
            return;

        if (TryComp(uid, out CMUHunterThreatComponent? hunter))
            TryStartHunterPounce(uid, target, hunter);
        else if (TryComp(uid, out CMUSmokerThreatComponent? smoker))
            TryStartSmokerTongue(uid, target, smoker);
        else if (TryComp(uid, out CMUBoomerThreatComponent? boomer))
            TryStartBoomerVomit(uid, target, boomer);
        else if (TryComp(uid, out CMUJockeyThreatComponent? jockey))
            TryStartJockeyRide(uid, target, jockey);
        else if (TryComp(uid, out CMUSpitterThreatComponent? spitter))
            TryStartSpitterAcid(uid, Transform(target).Coordinates, spitter);
    }

    private void SetRallyPoint(EntityUid uid, EntityUid rallyPoint)
    {
        if (!TryComp(uid, out CMUThreatComponent? threat) ||
            !TryComp(uid, out HTNComponent? htn) ||
            HasComp<ActorComponent>(uid))
            return;

        ClearNpcTarget(uid, threat);
        PauseNpcPlan(uid, htn);
        threat.RallyPoint = rallyPoint;
        htn.Blackboard.Remove<EntityCoordinates>(RallyCoordinatesKey);
        htn.Blackboard.Remove<PathResultEvent>(RallyPathfindKey);
        var steering = _steering.Register(uid, new EntityCoordinates(rallyPoint, System.Numerics.Vector2.Zero));
        // Register reuses the existing steering component. A failed combat path leaves that component in
        // NoPath, which otherwise makes the steering system ignore the new rally destination entirely.
        steering.Status = SteeringStatus.Moving;
        steering.FailedPathCount = 0;
        if (TryComp(rallyPoint, out CMUThreatRallyPointComponent? rally))
            steering.Range = rally.ArrivalRadius;
        StopMover(uid);
        _npc.WakeNPC(uid, htn);
        Dirty(uid, threat);
    }

    private bool UpdateRallyOrder(EntityUid uid, CMUThreatComponent threat)
    {
        if (threat.RallyPoint is not { } rallyPoint)
            return false;

        if (TryComp(rallyPoint, out CMUThreatRallyPointComponent? rally) &&
            TryDistance(uid, rallyPoint, out var distance) &&
            distance > rally.ArrivalRadius)
        {
            if (threat.CurrentTarget != null)
                ClearNpcTarget(uid, threat);
            var coordinates = new EntityCoordinates(rallyPoint, Vector2.Zero);
            if (_steering.TryRegister(uid, coordinates))
            {
                var steering = Comp<NPCSteeringComponent>(uid);
                steering.Range = rally.ArrivalRadius;
                if (TryComp(uid, out HTNComponent? activeHtn))
                    _npc.WakeNPC(uid, activeHtn);
            }
            return true;
        }

        threat.RallyPoint = null;
        Dirty(uid, threat);
        _steering.Unregister(uid);
        if (TryComp(uid, out HTNComponent? htn))
        {
            PauseNpcPlan(uid, htn);
            htn.Blackboard.Remove<EntityCoordinates>(RallyCoordinatesKey);
            htn.Blackboard.Remove<PathResultEvent>(RallyPathfindKey);
            ResumeNpcPlan(uid, htn);
        }
        return false;
    }

    private void PauseNpcPlan(EntityUid uid, HTNComponent htn)
    {
        _npc.SleepNPC(uid, htn);
        _htn.SetHTNEnabled((uid, htn), false);
        htn.PlanningToken?.Cancel();
        htn.PlanningToken = null;
        htn.PlanningJob = null;
    }

    private void ResumeNpcPlan(EntityUid uid, HTNComponent htn)
    {
        _htn.SetHTNEnabled((uid, htn), true);
        _npc.WakeNPC(uid, htn);
        _htn.Replan(htn);
    }

    private void UpdateSleepingWitch(EntityUid uid, CMUWitchThreatComponent witch, float elapsed)
    {
        if (_mobState.IsDead(uid))
            return;

        var nearby = FindNearestVictim(uid, witch.NoticeRadius);
        if (nearby != null)
        {
            witch.Offender = nearby;
            witch.Irritation += witch.NoticeGainPerSecond * elapsed;
        }
        else
        {
            witch.Irritation = MathF.Max(0f, witch.Irritation - witch.DecayPerSecond * elapsed);
        }

        if (witch.Irritation >= witch.IrritationThreshold)
            EnrageWitch(uid, witch);
    }

    private EntityUid? FindNearestVictim(EntityUid uid, float radius, EntityUid? exclude = null, bool humanOnly = false)
    {
        var origin = _transform.GetMapCoordinates(uid);
        EntityUid? nearest = null;
        var closestDistance = radius;
        foreach (var candidate in _lookup.GetEntitiesInRange<MobStateComponent>(origin, radius))
        {
            if (candidate == exclude ||
                (humanOnly && !HasComp<HumanoidProfileComponent>(candidate)) ||
                !IsValidVictim(uid, candidate) ||
                !TryDistance(uid, candidate, out var distance) ||
                distance >= closestDistance)
                continue;

            closestDistance = distance;
            nearest = candidate;
        }
        return nearest;
    }

    private void OnWitchDamaged(Entity<CMUWitchThreatComponent> ent, ref Content.Shared.Damage.Systems.DamageChangedEvent args)
    {
        if (!args.DamageIncreased || _mobState.IsDead(ent))
            return;

        ent.Comp.Offender = args.Origin is { } origin && IsValidVictim(ent, origin)
            ? origin
            : ent.Comp.Offender;
        ent.Comp.Irritation += ent.Comp.DamageGain;
        if (ent.Comp.Irritation >= ent.Comp.IrritationThreshold)
            EnrageWitch(ent, ent.Comp);
    }

    private void EnrageWitch(EntityUid uid, CMUWitchThreatComponent witch)
    {
        if (witch.Enraged)
            return;

        witch.Enraged = true;
        _npc.WakeNPC(uid);
        PlayAbilitySound(uid, witch.EnrageSound);
        SetState(uid, CMUThreatAiState.Enraged);
    }
}
