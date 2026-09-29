using Content.Server.GameTicking;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.GameTicking;
using Robust.Shared.Timing;
using Robust.Shared.Random;

namespace Content.Server.CMU14.ColonyEconomy;

/// <summary>
///     Central banking service. Handles account/PIN assignment, PIN checks and account lookups.
/// </summary>
public sealed partial class ColonyBankSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private GameTicker _ticker = default!;

    private const int PinMin = 1000;
    private const int PinMax = 9999;
    private const int AcctMin = 10000;
    private const int AcctMax = 99999;
    public const int MaxPinAttempts = 3;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);

    // How many entries an account keeps; the ATM pages through them.
    public const int MaxHistoryEntries = 30;

    // Every account number and PIN handed out this round, so no two cards ever share one.
    private readonly HashSet<int> _usedAccounts = new();
    private readonly HashSet<int> _usedPins = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IdCardComponent, ComponentStartup>(OnCardStartup);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    // The only place credentials are ever assigned: the moment an ID card is created, before
    // anyone can hold, swipe or read it. Nothing assigns them lazily later on.
    private void OnCardStartup(EntityUid uid, IdCardComponent card, ComponentStartup args)
    {
        AssignCredentials(card);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _usedAccounts.Clear();
        _usedPins.Clear();
    }

    /// <summary>
    ///     Gives a newly created ID card its own account number and PIN.
    /// </summary>
    private void AssignCredentials(IdCardComponent card)
    {
        if (card.AccountNumber == 0)
            card.AccountNumber = GenerateUnique(_usedAccounts, AcctMin, AcctMax);

        if (card.AtmPin == 0)
            card.AtmPin = GenerateUnique(_usedPins, PinMin, PinMax);
    }

    /// <summary>
    ///     Looks up an ID card by account number. Returns null if not found.
    /// </summary>
    public (EntityUid uid, IdCardComponent card)? FindAccount(int accountNumber)
    {
        if (accountNumber == 0)
            return null;

        var query = EntityQueryEnumerator<IdCardComponent>();
        while (query.MoveNext(out var uid, out var card))
        {
            if (card.AccountNumber == accountNumber)
                return (uid, card);
        }
        return null;
    }

    /// <summary>
    ///     Finds the ID card bound to <paramref name="owner"/>, wherever it currently is.
    /// </summary>
    public (EntityUid uid, IdCardComponent card)? FindOwnedCard(EntityUid owner)
    {
        var query = EntityQueryEnumerator<IdCardComponent>();
        while (query.MoveNext(out var uid, out var card))
        {
            if (card.OriginalOwner == owner)
                return (uid, card);
        }
        return null;
    }

    /// <summary>
    ///     Verifies PIN and applies lockout on failure.
    ///     Returns true if authenticated, false if wrong/locked.
    ///     Out parameter <paramref name="locked"/> is true when the card just got locked.
    /// </summary>
    public bool TryAuthenticatePin(EntityUid cardUid, IdCardComponent card, int enteredPin, out bool locked)
    {
        locked = false;

        // Check lockout
        if (card.PinLockedUntil.HasValue && _timing.CurTime < card.PinLockedUntil.Value)
            return false;

        if (card.PinLockedUntil.HasValue && _timing.CurTime >= card.PinLockedUntil.Value)
        {
            // Lock expired - reset
            card.PinLockedUntil = null;
            card.PinAttempts = 0;
        }

        if (card.AtmPin != 0 && card.AtmPin == enteredPin)
        {
            card.PinAttempts = 0;
            return true;
        }

        card.PinAttempts++;
        if (card.PinAttempts >= MaxPinAttempts)
        {
            card.PinLockedUntil = _timing.CurTime + LockDuration;
            locked = true;
        }

        return false;
    }

    /// <summary>
    ///     Whether a card is currently locked out.
    /// </summary>
    public bool IsLocked(IdCardComponent? card, out TimeSpan? unlockAt)
    {
        if (card?.PinLockedUntil is { } lockedUntil && _timing.CurTime < lockedUntil)
        {
            unlockAt = lockedUntil;
            return true;
        }
        unlockAt = null;
        return false;
    }

    /// <summary>
    ///     Adds a line to the card's account history, dropping the oldest once it is full.
    /// </summary>
    public void RecordTransaction(EntityUid cardUid, AtmHistoryKind kind, int amount, int otherAccount = 0)
    {
        var history = EnsureComp<ColonyAccountHistoryComponent>(cardUid);
        history.Entries.Add(new ColonyAccountHistoryEntry(_ticker.RoundDuration(), kind, amount, otherAccount));
        if (history.Entries.Count > MaxHistoryEntries)
            history.Entries.RemoveAt(0);
    }

    /// <summary>
    ///     The card's recent account history, oldest first.
    /// </summary>
    public IReadOnlyList<ColonyAccountHistoryEntry> GetHistory(EntityUid cardUid)
    {
        return TryComp<ColonyAccountHistoryComponent>(cardUid, out var history)
            ? history.Entries
            : Array.Empty<ColonyAccountHistoryEntry>();
    }

    // ── Internal helpers ─────────────────────────────────────────────────

    /// <summary>
    ///     Picks a random value in [min, max] that has not been handed out this round.
    ///     Only falls back to a repeat once every value in the range is taken.
    /// </summary>
    private int GenerateUnique(HashSet<int> used, int min, int max)
    {
        var range = max - min + 1;
        if (used.Count < range)
        {
            // Random probing is fast while the range is mostly free; walk forward if we keep colliding.
            var candidate = _random.Next(min, max + 1);
            for (var attempt = 0; attempt < 20 && used.Contains(candidate); attempt++)
                candidate = _random.Next(min, max + 1);

            while (used.Contains(candidate))
                candidate = candidate == max ? min : candidate + 1;

            used.Add(candidate);
            return candidate;
        }

        return _random.Next(min, max + 1);
    }
}
