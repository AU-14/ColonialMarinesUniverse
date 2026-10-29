using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Server.Antag;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Round;

/// <summary>
/// Pity weighting for round-start antag selection: every consecutive round an
/// opted-in player goes unselected raises their draw weight by a step, up to a
/// cap; any successful antag initialization resets the counter. Weights are
/// consumed by <see cref="AntagSelectionSystem"/>'s round-start player pool,
/// so every player's long-run selection rate stays S/N — pity only compresses
/// the variance of the wait.
/// </summary>
public sealed partial class AntagPitySystem : EntitySystem
{
    private const long PruneAfterSeconds = 90L * 24 * 60 * 60;

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IServerDbManager _db = default!;

    // Published by full replacement from the load task; only main-thread
    // handlers mutate it afterwards.
    private Dictionary<NetUserId, int> _missed = new();
    private readonly HashSet<NetUserId> _eligible = new();
    private readonly HashSet<NetUserId> _selected = new();
    private bool _loaded;
    private AntagSelectionSystem? _antagSelection;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RulePlayerJobsAssignedEvent>(OnJobsAssigned);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<AfterAntagEntitySelectedEvent>(OnAfterAntagSelected);
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);

        _ = LoadFromDb();
    }

    public float GetWeight(ICommonSession player)
    {
        if (!_cfg.GetCVar(CCVars.AntagPityEnabled) || !_loaded)
            return 1f;

        _missed.TryGetValue(player.UserId, out var missed);
        var step = MathF.Max(0f, _cfg.GetCVar(CCVars.AntagPityStep));
        var cap = MathF.Max(1f, _cfg.GetCVar(CCVars.AntagPityCap));
        return MathF.Min(1f + missed * step, cap);
    }

    private async Task LoadFromDb()
    {
        try
        {
            var cutoff = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - PruneAfterSeconds;
            var entries = await _db.GetCmuAntagPityAsync(cutoff);

            var missed = new Dictionary<NetUserId, int>();
            foreach (var entry in entries)
                missed[new NetUserId(entry.PlayerUserId)] = entry.Missed;

            _missed = missed;
            _loaded = true;
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load antag pity state: {e}");
        }
    }

    private void OnJobsAssigned(RulePlayerJobsAssignedEvent args)
    {
        _eligible.Clear();

        foreach (var session in args.Players)
        {
            if (IsEligible(session))
                _eligible.Add(session.UserId);
        }
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        // Late joiners missed the snapshot but stay eligible for the rest of the round.
        if (!args.LateJoin || !IsEligible(args.Player))
            return;

        _eligible.Add(args.Player.UserId);
    }

    private void OnAfterAntagSelected(AfterAntagEntitySelectedEvent ev)
    {
        if (ev.Session is not { } session)
            return;

        _missed[session.UserId] = 0;
        _selected.Add(session.UserId);
    }

    private async void OnRoundEnd(RoundEndMessageEvent ev)
    {
        try
        {
            if (!_loaded)
                return;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var toSave = new List<CmuAntagPityEntry>();

            // Accrue first, all on the main thread; only locals cross an await.
            foreach (var userId in _eligible)
            {
                if (_selected.Contains(userId))
                    continue;

                _missed.TryGetValue(userId, out var missed);
                _missed[userId] = missed + 1;
            }

            foreach (var userId in _eligible)
                toSave.Add(new CmuAntagPityEntry(userId.UserId, _missed.GetValueOrDefault(userId), now));
            foreach (var userId in _selected)
                toSave.Add(new CmuAntagPityEntry(userId.UserId, _missed.GetValueOrDefault(userId), now));

            _eligible.Clear();
            _selected.Clear();

            foreach (var entry in toSave)
                await _db.SaveCmuAntagPityAsync(entry);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to save antag pity state: {e}");
        }
    }

    private bool IsEligible(ICommonSession session)
    {
        // Resolved lazily: AntagSelectionSystem reaches back into this system
        // from GetWeight the same way, so neither holds a boot-time dependency.
        _antagSelection ??= EntityManager.EntitySysManager.GetEntitySystem<AntagSelectionSystem>();
        return _antagSelection.TryGetValidAntagPreferences(session);
    }
}
