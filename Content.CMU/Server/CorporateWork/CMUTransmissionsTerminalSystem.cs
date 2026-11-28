using System.Linq;
using Content.Server.Popups;
using Content.Shared.CMU14.CorporateWork;
using Robust.Server.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.CorporateWork;

/// <summary>
///     Secure Transmissions: breaking into an intercepted signal by picking codes off a grid. See
///     <see cref="CMUTransmissionsTerminalComponent"/> for the rules. The server builds every grid around
///     a path that completes all its target sequences, so each case can be solved, and checks every pick.
/// </summary>
public sealed class CMUTransmissionsTerminalSystem : EntitySystem
{
    [Dependency] private CMUCorporateWorkSystem _work = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const string Task = "transmissions";
    private const int PathAttempts = 50;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUTransmissionsTerminalComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CMUTransmissionsTerminalComponent, CMUTransmissionsStartBuiMsg>(OnStart);
        SubscribeLocalEvent<CMUTransmissionsTerminalComponent, CMUTransmissionsPickBuiMsg>(OnPick);
        SubscribeLocalEvent<CMUTransmissionsTerminalComponent, CMUTransmissionsUploadBuiMsg>(OnUpload);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUTransmissionsTerminalComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Case is { Deadline: { } deadline } && now >= deadline)
                Finish((uid, comp), timedOut: true);
        }
    }

    private void OnUiOpened(Entity<CMUTransmissionsTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnStart(Entity<CMUTransmissionsTerminalComponent> ent, ref CMUTransmissionsStartBuiMsg args)
    {
        TryStartCase(ent, args.Tier, args.Actor);
    }

    private void OnPick(Entity<CMUTransmissionsTerminalComponent> ent, ref CMUTransmissionsPickBuiMsg args)
    {
        Pick(ent, args.Cell, args.Actor);
    }

    private void OnUpload(Entity<CMUTransmissionsTerminalComponent> ent, ref CMUTransmissionsUploadBuiMsg args)
    {
        if (ent.Comp.Case is { } transmission)
        {
            transmission.LastUser = args.Actor;
            Finish(ent, timedOut: false);
        }
    }

    /// <summary>
    ///     Intercepts a new signal at the given tier, replacing any open one.
    /// </summary>
    public bool TryStartCase(Entity<CMUTransmissionsTerminalComponent> ent, int tier, EntityUid user)
    {
        if (tier < 0 || tier >= ent.Comp.Tiers.Count || ent.Comp.Codes.Count == 0)
            return false;

        if (_timing.CurTime < ent.Comp.NextCaseAt)
        {
            _popup.PopupEntity(Loc.GetString("cmu-transmissions-cooldown"), ent, user);
            return false;
        }

        var info = ent.Comp.Tiers[tier];
        if (info.Sequences.Sum() > info.Buffer || Generate(info, ent.Comp.Codes.Count) is not { } transmission)
            return false;

        transmission.Tier = tier;
        transmission.LastUser = user;
        ent.Comp.Case = transmission;
        ent.Comp.LastResult = null;
        UpdateUi(ent);
        return true;
    }

    /// <summary>
    ///     Builds a grid around a random valid path, then takes the target sequences from the codes
    ///     along that path, one after another, so following it completes every one.
    /// </summary>
    private CMUTransmissionsCase? Generate(CMUTransmissionsTier tier, int codes)
    {
        for (var attempt = 0; attempt < PathAttempts; attempt++)
        {
            var path = new List<int>();
            while (path.Count < tier.Buffer)
            {
                var options = Enumerable.Range(0, tier.Size * tier.Size)
                    .Where(cell => CMUTransmissionsRules.CanPick(tier.Size, path, cell))
                    .ToList();
                if (options.Count == 0)
                    break;

                path.Add(_random.Pick(options));
            }

            if (path.Count < tier.Buffer)
                continue;

            var grid = new int[tier.Size * tier.Size];
            for (var i = 0; i < grid.Length; i++)
                grid[i] = _random.Next(codes);

            var sequences = new List<int[]>();
            var start = 0;
            foreach (var length in tier.Sequences)
            {
                sequences.Add(path.Skip(start).Take(length).Select(cell => grid[cell]).ToArray());
                start += length;
            }

            return new CMUTransmissionsCase { Tier = 0, Grid = grid, Sequences = sequences, Solution = path };
        }

        return null;
    }

    /// <summary>
    ///     Picks a cell. Returns false if the rules don't allow it. A full buffer closes the case.
    /// </summary>
    public bool Pick(Entity<CMUTransmissionsTerminalComponent> ent, int cell, EntityUid user)
    {
        if (ent.Comp.Case is not { } transmission)
            return false;

        var tier = ent.Comp.Tiers[transmission.Tier];
        if (!CMUTransmissionsRules.CanPick(tier.Size, transmission.Picks, cell))
            return false;

        transmission.Picks.Add(cell);
        transmission.LastUser = user;
        if (transmission.Picks.Count == 1 && tier.TimeLimit is { } limit)
            transmission.Deadline = _timing.CurTime + limit;

        if (transmission.Picks.Count >= tier.Buffer)
            Finish(ent, timedOut: false);
        else
            UpdateUi(ent);

        return true;
    }

    /// <summary>
    ///     Closes the case and pays for every target sequence in the buffer, by its share of the total
    ///     length. Running out of time uploads nothing.
    /// </summary>
    public void Finish(Entity<CMUTransmissionsTerminalComponent> ent, bool timedOut)
    {
        if (ent.Comp.Case is not { } transmission)
            return;

        var tier = ent.Comp.Tiers[transmission.Tier];
        var buffer = transmission.Picks.Select(cell => transmission.Grid[cell]).ToList();
        var completed = timedOut
            ? new List<int[]>()
            : transmission.Sequences.Where(s => CMUTransmissionsRules.Contains(buffer, s)).ToList();

        var total = transmission.Sequences.Sum(s => s.Length);
        var reward = total > 0 ? tier.Reward * completed.Sum(s => s.Length) / (float) total : 0f;
        var payout = transmission.LastUser is { } user && reward > 0 ? _work.Pay(user, Task, reward) : 0;

        ent.Comp.LastResult = new CMUTransmissionsResult(completed.Count, transmission.Sequences.Count, payout, timedOut);
        ent.Comp.Case = null;
        ent.Comp.NextCaseAt = _timing.CurTime + ent.Comp.Cooldown;
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<CMUTransmissionsTerminalComponent> ent)
    {
        var tiers = ent.Comp.Tiers
            .Select(t => new CMUTransmissionsTierInfo(t.Name, t.Size, t.Buffer, t.Sequences.Count, t.Reward,
                t.TimeLimit is { } limit ? (int) limit.TotalSeconds : null))
            .ToList();

        var transmission = ent.Comp.Case;
        var state = new CMUTransmissionsTerminalBuiState(
            ent.Comp.Codes.ToList(),
            tiers,
            transmission?.Tier,
            transmission?.Grid.ToArray() ?? Array.Empty<int>(),
            transmission?.Sequences.Select(s => s.ToArray()).ToList() ?? new List<int[]>(),
            transmission?.Picks.ToList() ?? new List<int>(),
            transmission?.Deadline,
            ent.Comp.NextCaseAt,
            ent.Comp.LastResult);

        _ui.SetUiState(ent.Owner, CMUTransmissionsTerminalUi.Key, state);
    }
}
