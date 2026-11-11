namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private const int MaximumRecognizedContacts = 32;

    // Observe runs synchronously inside the decision scheduler. Reuse scratch storage
    // across agents; retaining it on every component would multiply crowded-scene capacity.
    private readonly List<(EntityUid Target, float Distance)> _observationCandidates = new();
    private readonly List<(EntityUid Target, float Distance)> _recognizedCandidates = new();
    private readonly HashSet<EntityUid> _observedThisThink = new();
    private readonly HashSet<EntityUid> _observationShotNeighbors = new();
    private readonly List<EntityUid> _expiredRecognition = new(MaximumRecognizedContacts);

    private void ExpireRecognition(CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        _expiredRecognition.Clear();
        foreach (var (target, contact) in agent.ContactRecognition)
            if (!Exists(target) || now - contact.LastObserved > agent.MemoryDuration)
                _expiredRecognition.Add(target);
        foreach (var target in _expiredRecognition)
            agent.ContactRecognition.Remove(target);
    }

    private static bool RecognizedContact(CMUExpeditionAgentComponent agent, EntityUid target, TimeSpan now) =>
        agent.ContactRecognition.TryGetValue(target, out var contact) && now < contact.RecognizedUntil;

    private List<(EntityUid Target, float Distance)> RecognizeCandidates(EntityUid uid,
        CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        _recognizedCandidates.Clear();
        _observedThisThink.Clear();
        var budget = Math.Clamp(agent.SightChecksPerThink, 3, 16);
        // Keep the current target live, then check the nearest bodies before rotating
        // through the rest. No cached visibility grants permission to fire.
        foreach (var candidate in _observationCandidates)
            if (candidate.Target == agent.Target)
                ObserveCandidate(uid, agent, candidate, now);
        for (var i = 0; i < Math.Min(2, _observationCandidates.Count); i++)
            ObserveCandidate(uid, agent, _observationCandidates[i], now);
        foreach (var candidate in _observationCandidates)
        {
            if (_observedThisThink.Count >= budget)
                break;
            if (agent.RecentShooters.TryGetValue(candidate.Target, out var until) && now < until)
                ObserveCandidate(uid, agent, candidate, now);
        }
        for (var i = 0; i < _observationCandidates.Count && _observedThisThink.Count < budget; i++)
        {
            agent.ContactScanCursor %= _observationCandidates.Count;
            ObserveCandidate(uid, agent, _observationCandidates[agent.ContactScanCursor++], now);
        }
        _recognizedCandidates.Sort(static (a, b) =>
        {
            var distance = a.Distance.CompareTo(b.Distance);
            return distance != 0 ? distance : a.Target.CompareTo(b.Target);
        });
        agent.SightChecksLastThink = _observedThisThink.Count;
        return _recognizedCandidates;
    }

    private void ObserveCandidate(EntityUid uid, CMUExpeditionAgentComponent agent,
        (EntityUid Target, float Distance) candidate, TimeSpan now)
    {
        if (!_observedThisThink.Add(candidate.Target))
            return;
        if (!Visible(uid, candidate.Target, agent.DetectionRange))
        {
            if (agent.ContactRecognition.TryGetValue(candidate.Target, out var lost))
            {
                lost.Exposure = 0;
                agent.ContactRecognition[candidate.Target] = lost;
            }
            return;
        }
        if (!RecognizeContact(agent, candidate.Target, candidate.Distance, now))
            return;
        _recognizedCandidates.Add(candidate);
        agent.VisibleThreats.Add(Transform(candidate.Target).Coordinates);
    }

    // Called only after native visibility, darkness and smoke checks succeed. A report
    // or a damage attribution never supplies exposure to a body behind a wall.
    private bool RecognizeContact(CMUExpeditionAgentComponent agent, EntityUid target, float distance, TimeSpan now)
    {
        var immediate = distance <= agent.ImmediateRecognitionRange ||
            IsMeleeThreat(target) && distance <= agent.MeleeStandoffRange + 2 ||
            agent.RecentShooters.TryGetValue(target, out var shooterUntil) && now < shooterUntil ||
            target == agent.Target && !agent.ContactFromRadio && agent.LastSeen != null && now < agent.ForgetAt;
        if (!agent.ContactRecognition.TryGetValue(target, out var contact))
        {
            // Keep partial observations long enough to recognize them. Recycle an older
            // recognized contact first so a large crowd cannot permanently hide newcomers.
            if (agent.ContactRecognition.Count >= MaximumRecognizedContacts)
            {
                EntityUid? replace = null;
                var oldest = TimeSpan.MaxValue;
                foreach (var (other, memory) in agent.ContactRecognition)
                {
                    if (other == agent.Target || memory.LastObserved >= oldest ||
                        memory.RecognizedUntil == TimeSpan.Zero && now - memory.LastObserved < TimeSpan.FromSeconds(2))
                        continue;
                    replace = other;
                    oldest = memory.LastObserved;
                }
                if (replace is not { } expired)
                    return immediate;
                agent.ContactRecognition.Remove(expired);
            }
            contact.LastObserved = now;
        }
        var elapsed = now - contact.LastObserved;
        // Skipped/deferred checks do not grant free exposure. Actual failed sight checks
        // reset partial recognition; a budgeted interval alone is not evidence of occlusion.
        contact.Exposure += Math.Clamp(elapsed.TotalSeconds, 0, ThinkInterval.TotalSeconds);
        contact.LastObserved = now;
        var distanceFactor = Math.Clamp((distance - agent.ImmediateRecognitionRange) /
            Math.Max(1, agent.DetectionRange - agent.ImmediateRecognitionRange), 0.25f, 1);
        var attention = agent.Target != null && agent.Target != target ? 1.5 : 1;
        var recognized = immediate || now < contact.RecognizedUntil ||
            contact.Exposure >= agent.RecognitionTime.TotalSeconds * distanceFactor * attention;
        if (recognized)
            contact.RecognizedUntil = now + agent.MemoryDuration;
        agent.ContactRecognition[target] = contact;
        return recognized;
    }
}
