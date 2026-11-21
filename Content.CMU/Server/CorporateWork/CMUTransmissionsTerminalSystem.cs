using System.Linq;
using Content.Server.Popups;
using Content.Shared.CMU14.CorporateWork;
using Robust.Server.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.CorporateWork;

public sealed class CMUTransmissionsTerminalSystem : EntitySystem
{
    [Dependency] private CMUCorporateWorkSystem _work = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const string Task = "transmissions";

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUTransmissionsTerminalComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CMUTransmissionsTerminalComponent, CMUTransmissionsStartBuiMsg>(OnStart);
        SubscribeLocalEvent<CMUTransmissionsTerminalComponent, CMUTransmissionsGuessBuiMsg>(OnGuess);
    }

    private void OnUiOpened(Entity<CMUTransmissionsTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnStart(Entity<CMUTransmissionsTerminalComponent> ent, ref CMUTransmissionsStartBuiMsg args)
    {
        TryStartCase(ent, args.Tier, args.Actor);
    }

    private void OnGuess(Entity<CMUTransmissionsTerminalComponent> ent, ref CMUTransmissionsGuessBuiMsg args)
    {
        Guess(ent, args.Guess, args.Actor);
    }

    /// <summary>
    ///     Intercepts a new transmission at the given tier, replacing any open one.
    /// </summary>
    public bool TryStartCase(Entity<CMUTransmissionsTerminalComponent> ent, int tier, EntityUid user)
    {
        if (tier < 0 || tier >= ent.Comp.Tiers.Count)
            return false;

        if (_timing.CurTime < ent.Comp.NextCaseAt)
        {
            _popup.PopupEntity(Loc.GetString("cmu-transmissions-cooldown"), ent, user);
            return false;
        }

        var info = ent.Comp.Tiers[tier];
        var key = new int[info.Length];
        for (var i = 0; i < key.Length; i++)
        {
            key[i] = _random.Next(info.Symbols);
        }

        ent.Comp.Case = new CMUTransmissionsCase { Tier = tier, Key = key };
        ent.Comp.LastResult = null;
        UpdateUi(ent);
        return true;
    }

    /// <summary>
    ///     Tries a key. A correct key, or running out of attempts, closes the case.
    /// </summary>
    public CMUTransmissionsAttempt? Guess(Entity<CMUTransmissionsTerminalComponent> ent, int[] guess, EntityUid user)
    {
        if (ent.Comp.Case is not { } transmission)
            return null;

        var tier = ent.Comp.Tiers[transmission.Tier];
        if (guess.Length != tier.Length || guess.Any(s => s < 0 || s >= tier.Symbols))
            return null;

        var attempt = Score(transmission.Key, guess, tier.Symbols);
        transmission.Attempts.Add(attempt);

        var solved = attempt.Exact == tier.Length;
        if (solved || transmission.Attempts.Count >= tier.Attempts)
        {
            // First attempt pays the full reward, the last pays LastAttemptFraction of it.
            var reward = 0f;
            if (solved)
            {
                var spare = tier.Attempts > 1
                    ? (tier.Attempts - transmission.Attempts.Count) / (float) (tier.Attempts - 1)
                    : 1f;
                var fraction = ent.Comp.LastAttemptFraction + (1 - ent.Comp.LastAttemptFraction) * spare;
                reward = tier.Reward * fraction;
            }

            var payout = _work.Pay(user, Task, reward);
            ent.Comp.LastResult = new CMUTransmissionsResult(solved, transmission.Attempts.Count, payout, transmission.Key);
            ent.Comp.Case = null;
            ent.Comp.NextCaseAt = _timing.CurTime + ent.Comp.Cooldown;
        }

        UpdateUi(ent);
        return attempt;
    }

    public static CMUTransmissionsAttempt Score(int[] key, int[] guess, int symbols)
    {
        var exact = 0;
        var keyCounts = new int[symbols];
        var guessCounts = new int[symbols];
        for (var i = 0; i < key.Length; i++)
        {
            if (key[i] == guess[i])
            {
                exact++;
                continue;
            }

            keyCounts[key[i]]++;
            guessCounts[guess[i]]++;
        }

        var misplaced = 0;
        for (var s = 0; s < symbols; s++)
        {
            misplaced += Math.Min(keyCounts[s], guessCounts[s]);
        }

        return new CMUTransmissionsAttempt((int[]) guess.Clone(), exact, misplaced);
    }

    private void UpdateUi(Entity<CMUTransmissionsTerminalComponent> ent)
    {
        var tiers = ent.Comp.Tiers
            .Select(t => new CMUTransmissionsTierInfo(t.Name, t.Length, t.Symbols, t.Attempts, t.Reward))
            .ToList();

        var transmission = ent.Comp.Case;
        var state = new CMUTransmissionsTerminalBuiState(
            tiers,
            transmission?.Tier,
            transmission?.Attempts.ToList() ?? new List<CMUTransmissionsAttempt>(),
            ent.Comp.NextCaseAt,
            ent.Comp.LastResult);

        _ui.SetUiState(ent.Owner, CMUTransmissionsTerminalUi.Key, state);
    }
}
