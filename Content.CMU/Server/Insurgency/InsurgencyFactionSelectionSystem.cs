using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.CMU14.Round;
using Content.Shared.CMU14.Insurgency;
using Content.Shared.CMU14.Insurgency.Selection;
using Content.Shared.GameTicking;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Server.Player;
using Content.Server.CMU14.Insurgency.Database;

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
    [Dependency] private InsurgencyFactionDbSystem _db = default!;
    [Dependency] private InsurgencyFactionApplySystem _apply = default!;

    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _players = default!;

    public async Task<List<InsurgencyFactionDbSystem.StoredFaction>> GetOpposingDefaultFactionsAsync()
    {
        var govfor = _platoons.SelectedGovforPlatoon?.ID;
        var all = await _db.GetFactionsAsync();
        return all.Where(f => f.IsDefault && OpposesGovfor(f.Definition, govfor)).ToList();
    }

    private readonly Dictionary<NetUserId, int> _votes = new();
    private readonly Dictionary<NetUserId, HashSet<int>> _excluded = new();
    private readonly Dictionary<NetUserId, (int Id, FactionDefinition Definition)> _custom = new();
    private int _nextCustomId = -2;
    private int _generation;
    private TimeSpan? _deadline;
    private bool _resolving;
    public event Action? BallotClosed;
    public event Action? CandidatesChanged;
    public IEnumerable<(int Id, FactionDefinition Definition)> CustomCandidates => _custom.Values;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => ResetBallot());
        SubscribeLocalEvent<InsurgencyFactionAppliedEvent>((ref InsurgencyFactionAppliedEvent _) => ResetBallot());
    }

    private void ResetBallot()
    {
        _generation++;
        _resolving = false;
        _deadline = null;
        _votes.Clear();
        _excluded.Clear();
        _custom.Clear();
        _nextCustomId = -2;
        BallotClosed?.Invoke();
    }

    public bool TryStartBallot()
    {
        if (_resolving)
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
        if (!Eligible(player) || _excluded.TryGetValue(player.UserId, out var excluded) && excluded.Contains(id))
            return false;
        _votes[player.UserId] = id;
        return true;
    }

    public void Nominate(ICommonSession player, FactionDefinition definition)
    {
        if (!Eligible(player))
            return;

        var id = _custom.TryGetValue(player.UserId, out var prior) ? prior.Id : _nextCustomId--;
        _custom[player.UserId] = (id, definition);
        Vote(player, id);
        CandidatesChanged?.Invoke();
    }

    public override void Update(float frameTime)
    {
        if (_deadline is not { } end || _timing.CurTime < end)
            return;
        _deadline = null;
        _resolving = true;
        FinishBallot(_generation);
    }

    private async void FinishBallot(int generation)
    {
        List<InsurgencyFactionDbSystem.StoredFaction> available;
        try
        {
            available = await GetOpposingDefaultFactionsAsync();
        }
        catch (Exception error)
        {
            Log.Error($"Unable to load INSFOR ballot candidates: {error}");
            available = [];
        }
        if (_generation != generation || _apply.GetActiveFaction() != null)
            return;

        var candidates = available.ToDictionary(row => row.Id, row => row.Definition);
        if (!available.Any(row => row.Definition.Metadata.BuiltinOverrideOf == InsurgencyBuiltinFactions.VanillaClfId))
            candidates[InsurgencyBuiltinFactions.VanillaClfId] = InsurgencyBuiltinFactions.VanillaClf();
        foreach (var (id, definition) in CustomCandidates)
            candidates[id] = definition;

        var voters = _players.Sessions.Where(player => player.AttachedEntity is { } member &&
            HasComp<InsurgencyPendingFactionSelectionComponent>(member)).Select(player => player.UserId).ToArray();
        // Prefer a faction everyone enabled. If the cell vetoed every option, minimize objections.
        var finalists = candidates.Keys.GroupBy(id => voters.Count(voter =>
            _excluded.TryGetValue(voter, out var veto) && veto.Contains(id))).OrderBy(group => group.Key).First();
        var winners = finalists.GroupBy(id => voters.Count(voter => _votes.TryGetValue(voter, out var choice) && choice == id))
            .OrderByDescending(group => group.Key).First().ToList();
        _apply.ApplyFaction(candidates[_random.Pick(winners)]);
    }

    private static bool OpposesGovfor(FactionDefinition def, string? govforPlatoon)
    {
        // No GOVFOR platoon picked yet: nothing is confirmed to match, so offer nothing rather than
        // everything. A faction must explicitly list the platoon it opposes.
        if (govforPlatoon == null)
            return false;

        // The built-in CLF (now seeded as an ordinary DB row) opposes every GOVFOR, exactly as the
        // code-built copy always did, regardless of its OpposedGovforFactions list.
        if (def.Metadata.BuiltinOverrideOf == InsurgencyBuiltinFactions.VanillaClfId)
            return true;

        return def.Metadata.OpposedGovforFactions
            .Any(g => string.Equals(g, govforPlatoon, StringComparison.OrdinalIgnoreCase));
    }
}
