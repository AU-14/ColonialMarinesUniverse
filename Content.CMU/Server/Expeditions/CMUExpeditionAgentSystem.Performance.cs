using Content.Server.NPC.Components;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Movement.Components;
using Content.Shared.NPC;
using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private IConfigurationManager _aiConfig = default!;

    private readonly List<(EntityUid Uid, CMUExpeditionAgentComponent Agent)> _dueThinks = new();
    private readonly List<(EntityUid Uid, CMUExpeditionAgentComponent Agent, TransformComponent Transform)> _reservationAgents = new();
    private EntityUid? _thinkingAgent;
    private bool _reservationAgentsBuilt;
    private readonly ObjectPool<HashSet<EntityUid>> _nearbyPool =
        new DefaultObjectPool<HashSet<EntityUid>>(new SetPolicy<EntityUid>());
    private readonly ObjectPool<CMUTacticalRoute.Workspace> _routeWorkspacePool =
        new DefaultObjectPool<CMUTacticalRoute.Workspace>(new DefaultPooledObjectPolicy<CMUTacticalRoute.Workspace>());

    public int ThinksLastUpdate { get; private set; }
    public int DeferredThinksLastUpdate { get; private set; }
    public double DecisionMilliseconds { get; private set; }

    private void RunDueThinks(TimeSpan now)
    {
        // Oldest deadlines win, with stable ties. Deferred agents keep their deadline,
        // so newly due agents and repeated gunfire cannot starve the rest of the squad.
        _dueThinks.Sort(static (a, b) =>
        {
            var deadline = a.Agent.NextThink.CompareTo(b.Agent.NextThink);
            return deadline != 0 ? deadline : a.Uid.CompareTo(b.Uid);
        });
        var budget = _aiConfig.GetCVar(CCVars.CMUExpeditionThinkBudgetMs);
        if (!float.IsFinite(budget) || budget <= 0)
            budget = CCVars.CMUExpeditionThinkBudgetMs.DefaultValue;
        var limit = Math.Max(1, _aiConfig.GetCVar(CCVars.CMUExpeditionThinksPerTick));
        var started = Stopwatch.GetTimestamp();
        ThinksLastUpdate = 0;
        foreach (var (uid, agent) in _dueThinks)
        {
            if (ThinksLastUpdate >= limit || ThinksLastUpdate > 0 &&
                Stopwatch.GetElapsedTime(started).TotalMilliseconds >= budget)
                break;
            if (!Exists(uid) || agent.Deleted || HasComp<ActorComponent>(uid) || !_mobs.IsAlive(uid))
                continue;
            var transform = Transform(uid);
            agent.NextThink = now + ThinkInterval;
            agent.Home ??= transform.Coordinates;
            LoadExperience(uid, agent);
            EnsureComp<InputMoverComponent>(uid);
            EnsureComp<ActiveNPCComponent>(uid);
            RemComp<NPCRangedCombatComponent>(uid);
            Think(uid, agent, transform, now);
            ThinksLastUpdate++;
        }
        DecisionMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        DeferredThinksLastUpdate = _dueThinks.Count - ThinksLastUpdate;
        _dueThinks.Clear();
    }

    private static void RequestThink(CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        // Interrupt a future think, but never move an already overdue deadline back.
        if (agent.NextThink > now)
            agent.NextThink = now;
    }

    private IEnumerable<(EntityUid Uid, CMUExpeditionAgentComponent Agent, TransformComponent Transform)> ReservationAgents(EntityUid uid)
    {
        // Reuse map membership within one synchronous decision. Positions, reservations
        // and life state are still read live. Outside Think, build a fresh enumeration.
        var cached = _thinkingAgent == uid;
        if (!cached || !_reservationAgentsBuilt)
        {
            var map = Transform(uid).MapID;
            var query = EntityQueryEnumerator<CMUExpeditionAgentComponent, TransformComponent>();
            while (query.MoveNext(out var other, out var agent, out var transform))
            {
                if (other == uid || transform.MapID != map)
                    continue;
                if (cached)
                    _reservationAgents.Add((other, agent, transform));
                else
                    yield return (other, agent, transform);
            }
            if (cached)
                _reservationAgentsBuilt = true;
        }
        if (cached)
            foreach (var entry in _reservationAgents)
                yield return entry;
    }
}
