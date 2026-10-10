using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Threats;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Ops.ThirdParty;

/// <summary>Lets ghosts call in a random minor third party once enough of the round's players are dead.</summary>
public sealed partial class GhostThirdPartyCallSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private AuRoundSystem _auRound = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedGameTicker _ticker = default!;
    [Dependency] private ThirdPartySystem _thirdParty = default!;

    private readonly HashSet<ThirdPartyPrototype> _calledParties = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
        => _calledParties.Clear();

    /// <summary>Snapshot for the ghost role menu, or null when the feature is disabled by cvar.</summary>
    public GhostThirdPartyCallState? GetState()
    {
        if (!_cfg.GetCVar(CCVars.GhostThirdPartyCallEnabled))
            return null;

        var ratio = _cfg.GetCVar(CCVars.GhostThirdPartyCallDeadRatio);
        var (dead, living) = CountPlayers();
        var minutesLeft = MinutesLeft();
        var pool = BuildPool();
        var pending = PendingParty() != null;
        return new(pool.Count > 0 && !pending
            && GhostThirdPartyCall.MeetsDeadThreshold(dead, living, ratio) && minutesLeft <= 0,
            dead, living, GhostThirdPartyCall.RequiredDead(living, ratio), minutesLeft, pending);
    }

    /// <summary>Validates the call gates, then queues a weighted random ghost-callable party for the caller.</summary>
    public void RequestCall(ICommonSession caller)
    {
        if (!_cfg.GetCVar(CCVars.GhostThirdPartyCallEnabled))
            return;

        if (caller.AttachedEntity is not { } entity || !HasComp<GhostComponent>(entity))
            return;

        if (PendingParty() is { } pending)
        {
            Deny(caller, "cmu-ghost-call-deny-pending",
                ("party", pending.DisplayName ?? pending.ID));
            return;
        }

        var ratio = _cfg.GetCVar(CCVars.GhostThirdPartyCallDeadRatio);
        var (dead, living) = CountPlayers();
        if (!GhostThirdPartyCall.MeetsDeadThreshold(dead, living, ratio))
        {
            Deny(caller, "cmu-ghost-call-deny-dead",
                ("dead", dead), ("required", GhostThirdPartyCall.RequiredDead(living, ratio)));
            return;
        }

        var minutesLeft = MinutesLeft();
        if (minutesLeft > 0)
        {
            Deny(caller, "cmu-ghost-call-deny-time", ("minutes", minutesLeft));
            return;
        }

        var pool = BuildPool();
        if (_auRound.PickWeightedThirdParty(pool) is not { } pick
            || !_prototypes.TryIndex(pick.PartySpawn, out PartySpawnPrototype? spawn))
        {
            Deny(caller, "cmu-ghost-call-deny-none");
            return;
        }

        // Set before spawning: the spawn pushes the menu state and the pending flag must already read true.
        _calledParties.Add(pick);
        _thirdParty.SpawnThirdParty(pick, spawn, false);

        _adminLog.Add(LogType.EventStarted, LogImpact.Low,
            $"{caller.Name} ({caller.UserId}) ghost-interest third party {pick.ID}");
        // _chat.DispatchServerMessage(caller,
        //    Loc.GetString("cmu-ghost-call-sent", ("party", pick.DisplayName ?? pick.ID)));
    }

    private void Deny(ICommonSession caller, string reason, params (string, object)[] args)
        => _chat.DispatchServerMessage(caller, Loc.GetString(reason, args));

    private List<ThirdPartyPrototype> BuildPool()
    {
        var queued = _thirdParty.GetQueuedThirdParties();
        var pool = new List<ThirdPartyPrototype>();
        foreach (var party in _prototypes.EnumeratePrototypes<ThirdPartyPrototype>())
        {
            // The round's own schedule still brings selected and roundstart parties on its own timer.
            if (party.Abstract || !party.GhostsCallable || party.RoundStart
                || _auRound.SelectedThirdParties.Contains(party) || queued.Contains(party)
                || _calledParties.Contains(party)
                || _auRound.SpawnedThirdParties.Contains(party)
                || !_auRound.IsThirdPartyAllowedForCurrentContext(party))
                continue;

            pool.Add(party);
        }

        return pool;
    }

    /// <summary>A called party that is still queued; deployment removes it from the queue and reopens the gate.</summary>
    private ThirdPartyPrototype? PendingParty()
        => _thirdParty.GetQueuedThirdParties().FirstOrDefault(_calledParties.Contains);

    private int MinutesLeft()
    {
        var floor = TimeSpan.FromMinutes(Math.Max(0, _cfg.GetCVar(CCVars.GhostThirdPartyCallMinRoundMinutes)));
        var left = floor - _ticker.RoundDuration();
        return left <= TimeSpan.Zero ? 0 : (int) Math.Ceiling(left.TotalMinutes);
    }

    private (int Dead, int Living) CountPlayers()
    {
        var dead = 0;
        var living = 0;
        foreach (var session in _players.Sessions)
        {
            if (session.Status != SessionStatus.InGame || session.AttachedEntity is not { } entity)
                continue;

            if (HasComp<GhostComponent>(entity))
                dead++;
            else if (TryComp(entity, out MobStateComponent? mob))
            {
                if (mob.CurrentState == MobState.Dead)
                    dead++;
                else if (mob.CurrentState is MobState.Alive or MobState.Critical)
                    living++;
            }
        }

        return (dead, living);
    }
}
