using System.Linq;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private void Decision(CMUExpeditionAgentComponent agent, string owner, string reason, double seconds = 0)
    {
        if (agent.DecisionOwner != owner)
        {
            agent.DecisionHistory.Enqueue($"{_timing.CurTime.TotalSeconds:F1}: {agent.DecisionOwner} -> {owner}: {reason}");
            while (agent.DecisionHistory.Count > 12)
                agent.DecisionHistory.Dequeue();
        }
        agent.DecisionOwner = owner;
        agent.DecisionUntil = _timing.CurTime + TimeSpan.FromSeconds(seconds);
    }

    // A productive burst owns optional decisions. Native firing, emergency evasion and
    // actual obstruction still run; this never holds an agent in a blocked firing state.
    private bool MaintainDecision(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.RushTarget != null || now - agent.LastHit < TimeSpan.FromSeconds(0.3) ||
            agent.LastDamage >= agent.EmergencyHealDamage || GrenadeDanger(Transform(uid).Coordinates))
        {
            Decision(agent, "survival", "immediate-threat");
            return false;
        }
        if (agent.Action != null || agent.Treatment != null || agent.PendingWeapon != null || agent.FlareItem != null ||
            agent.State != CMUExpeditionAgentState.Engage || now >= agent.BurstEnd ||
            agent.ShotsFired >= VolleySize(agent) || !_guns.TryGetGun(uid, out var gun) || WeaponAmmo(gun) <= 0 ||
            !TryAimPoint(uid, agent, gun, out var aim) || !SafeShot(uid, agent, gun, aim))
            return false;
        Decision(agent, "fire", "productive-volley", 0.3);
        return true;
    }

    private bool OptionalDecisionReady(CMUExpeditionAgentComponent agent) =>
        _timing.CurTime >= agent.DecisionUntil || agent.DecisionOwner != "fire";

    private void RememberBadCover(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates point)
    {
        var now = _timing.CurTime;
        agent.BadCover.RemoveAll(entry => entry.Until <= now);
        var index = agent.BadCover.FindIndex(entry => _transform.InRange(entry.Point, point, 1.2f));
        var hits = index >= 0 ? agent.BadCover[index].Hits + 1 : 1;
        if (index >= 0)
            agent.BadCover.RemoveAt(index);
        if (agent.BadCover.Count >= 12)
            agent.BadCover.RemoveAt(0);
        agent.BadCover.Add((point, now + TimeSpan.FromSeconds(Math.Min(45, 12 * hits)), hits));
        Decision(agent, "reassess-cover", "hit-at-shelter-or-repeated-peek");
    }

    private float CoverHistoryCost(CMUExpeditionAgentComponent agent, EntityCoordinates point) =>
        agent.BadCover.Where(entry => entry.Until > _timing.CurTime && _transform.InRange(entry.Point, point, 1.3f))
            .Sum(entry => 6f * entry.Hits);

    private static readonly Dictionary<string, (float Aggression, float Courage, float Range, float Commitment)> Doctrines = new()
    {
        ["balanced"] = (.5f, .5f, 0, 1), ["scavenger"] = (.3f, .4f, 1, 1.1f),
        ["uscm"] = (.65f, .75f, 0, 1.2f), ["rmc"] = (.55f, .8f, 1, 1.3f),
        ["upp"] = (.7f, .75f, -1, 1.3f), ["pmc"] = (.45f, .7f, 1, 1.2f),
        ["clf"] = (.8f, .55f, -1, .8f), ["cmb"] = (.3f, .65f, 1, 1.4f),
        ["lacn"] = (.6f, .65f, 0, 1.1f), ["ccaf"] = (.65f, .65f, -1, 1.1f),
        ["uacg"] = (.45f, .7f, 1, 1.3f), ["prodigy"] = (.75f, .8f, 0, 1.2f),
    };

    public static IEnumerable<string> DoctrineNames => Doctrines.Keys;

    public bool SetDoctrine(EntityUid uid, string name)
    {
        if (!TryComp<CMUExpeditionAgentComponent>(uid, out var agent) || !Doctrines.TryGetValue(name, out var doctrine))
            return false;
        agent.BasePreferredRange ??= agent.PreferredFireRange;
        agent.BaseAggression ??= agent.Aggression;
        agent.BaseCourage ??= agent.Courage;
        agent.BasePositionCommit ??= agent.PositionCommitDuration;
        agent.Doctrine = name;
        agent.Aggression = Math.Clamp(agent.BaseAggression.Value + doctrine.Aggression - .5f, .1f, .95f);
        agent.Courage = Math.Clamp(agent.BaseCourage.Value + doctrine.Courage - .5f, .1f, .95f);
        agent.Disposition = agent.Aggression >= .7f ? CMUExpeditionDisposition.Aggressive :
            agent.Aggression <= .35f ? CMUExpeditionDisposition.Cautious : CMUExpeditionDisposition.Steady;
        agent.PreferredFireRange = Math.Clamp(agent.BasePreferredRange.Value + doctrine.Range, 2, agent.FireRange - 1);
        agent.PositionCommitDuration = agent.BasePositionCommit.Value * doctrine.Commitment;
        Decision(agent, "doctrine", name);
        return true;
    }
}
