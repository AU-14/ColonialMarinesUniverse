using System.Linq;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool AnnounceNewContact(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid target, TimeSpan now)
    {
        var recentlyReported = false;
        var nearby = new List<CMUExpeditionAgentComponent>();
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
        {
            if (!_mobs.IsAlive(other) || !IsFriendly(uid, other) ||
                !_transform.InRange(Transform(uid).Coordinates, Transform(other).Coordinates, 40))
                continue;
            nearby.Add(buddy);
            foreach (var stale in buddy.AnnouncedContacts.Where(pair => now - pair.Value > TimeSpan.FromSeconds(60))
                         .Select(pair => pair.Key).ToArray())
                buddy.AnnouncedContacts.Remove(stale);
            recentlyReported |= buddy.AnnouncedContacts.ContainsKey(target);
        }
        // Refresh an existing contact's deduplication while it is still observed. Neither
        // target cycling, movement nor the passage of a periodic timer makes it new again.
        if (recentlyReported)
        {
            foreach (var buddy in nearby)
                buddy.AnnouncedContacts[target] = now;
            return false;
        }
        if (now < agent.NextRadioAnnouncement)
            return false;
        foreach (var buddy in nearby)
        {
            buddy.NextRadioAnnouncement = now + TimeSpan.FromSeconds(25);
            buddy.AnnouncedContacts[target] = now;
        }
        return true;
    }

    private string ContactPhrase(CMUExpeditionAgentComponent agent)
    {
        var tone = agent.RushTarget != null ? "rush" : agent.LastDamage >= agent.RetreatDamage ? "hurt" :
            agent.Stress > 0.65f ? "shaken" : agent.Aggression > 0.7f ? "bold" :
            agent.CombatRole == CMUExpeditionCombatRole.Medic ? "medic" :
            agent.Disposition == CMUExpeditionDisposition.Cautious ? "careful" : "steady";
        var choice = _visionRandom.Next(1, 4);
        var key = $"cmu-expedition-contact-{tone}-{choice}";
        if (key == agent.LastContactPhrase)
            key = $"cmu-expedition-contact-{tone}-{choice % 3 + 1}";
        agent.LastContactPhrase = key;
        return Loc.GetString(key);
    }
}
