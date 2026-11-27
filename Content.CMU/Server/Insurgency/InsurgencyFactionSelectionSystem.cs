using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.CMU14.Round;
using Content.Shared.CMU14.Insurgency;
using Content.Shared.CMU14.Insurgency.Selection;
using Content.Shared.GameTicking;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Server.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Insurgency;

/// <summary>
///     GOVFOR to Default-faction matching and the server-authoritative selection path. A Default
///     faction declares the GOVFOR platoons (USMC, TWE RMC, UPP, and so on) it may oppose; only
///     factions that list the round's chosen GOVFOR platoon are offered, and the match is re-checked
///     here before a faction is applied so a tampered client cannot force an unmatched faction.
/// </summary>
public sealed partial class InsurgencyFactionSelectionSystem : EntitySystem
{
    [Dependency] private PlatoonSpawnRuleSystem _platoons = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private InsurgencyFactionApplySystem _apply = default!;

    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _players = default!;

    public IEnumerable<CMUInsurgencyFactionPrototype> Factions =>
        _prototypes.EnumeratePrototypes<CMUInsurgencyFactionPrototype>();

    public bool IsAvailable(CMUInsurgencyFactionPrototype faction) =>
        faction.OpposesAll || _platoons.SelectedGovforPlatoon is { } platoon &&
        faction.Definition.Metadata.OpposedGovforFactions.Contains(platoon.ID, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<NetUserId, int> _votes = new();
    private readonly Dictionary<NetUserId, HashSet<int>> _excluded = new();
    private TimeSpan? _deadline;
    public TimeSpan? Deadline => _deadline;
    public event Action? BallotClosed;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => ResetBallot());
        SubscribeLocalEvent<InsurgencyFactionAppliedEvent>((ref InsurgencyFactionAppliedEvent _) => ResetBallot());
    }

    private void ResetBallot()
    {
        _deadline = null;
        _votes.Clear();
        _excluded.Clear();
        BallotClosed?.Invoke();
    }

    public bool TryStartBallot()
    {
        if (_apply.GetActiveFaction() != null)
            return false;
        _deadline ??= _timing.CurTime + TimeSpan.FromSeconds(45);
        return _timing.CurTime < _deadline;
    }

    private bool Eligible(ICommonSession player) => _deadline is { } end && _timing.CurTime < end &&
        player.AttachedEntity is { } mob && HasComp<InsurgencyPendingFactionSelectionComponent>(mob);

    public void SetExclusions(ICommonSession player, IEnumerable<int> ids)
    {
        if (!Eligible(player))
            return;

        var excluded = ids.Take(256).ToHashSet();
        _excluded[player.UserId] = excluded;
        if (_votes.TryGetValue(player.UserId, out var vote) && excluded.Contains(vote))
            _votes.Remove(player.UserId);
    }

    public bool Vote(ICommonSession player, int id)
    {
        if (!Eligible(player) || _excluded.TryGetValue(player.UserId, out var excluded) && excluded.Contains(id) ||
            !Factions.Any(faction => faction.BallotId == id && IsAvailable(faction)))
            return false;
        _votes[player.UserId] = id;
        return true;
    }

    public override void Update(float frameTime)
    {
        if (_deadline is not { } end || _timing.CurTime < end)
            return;
        _deadline = null;
        var candidates = Factions.Where(IsAvailable).ToDictionary(faction => faction.BallotId);
        var voters = _players.Sessions.Where(player => player.AttachedEntity is { } member &&
            HasComp<InsurgencyPendingFactionSelectionComponent>(member)).Select(player => player.UserId).ToArray();
        // Prefer a faction everyone enabled. If the cell vetoed every option, minimize objections.
        var finalists = candidates.Keys.GroupBy(id => voters.Count(voter =>
            _excluded.TryGetValue(voter, out var veto) && veto.Contains(id))).OrderBy(group => group.Key).First();
        var winners = finalists.GroupBy(id => voters.Count(voter => _votes.TryGetValue(voter, out var choice) && choice == id))
            .OrderByDescending(group => group.Key).First().ToList();
        _apply.ApplyFaction(candidates[_random.Pick(winners)].Definition);
    }
}
