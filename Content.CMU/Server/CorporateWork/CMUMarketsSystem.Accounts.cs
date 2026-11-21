using Content.Server.CMU14.ColonyEconomy;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.CorporateWork;

namespace Content.Server.CMU14.CorporateWork;

/// <summary>
///     Where trading money comes from: the corporate budget for the corporate terminal, the
///     user's own bank account (on their ID card) for the civilian app.
/// </summary>
public sealed partial class CMUMarketsSystem
{
    [Dependency] private ColonyBankSystem _bank = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;

    private const string BankPrefix = "bank:";

    /// <summary>
    ///     Who <paramref name="user"/> trades as at <paramref name="terminal"/>, or null if they have
    ///     no usable account.
    /// </summary>
    public Trader? GetTrader(Entity<CMUMarketsTerminalComponent> terminal, EntityUid user)
    {
        if (terminal.Comp.Corporate)
            return new Trader(CorporateAccount, Loc.GetString("cmu-markets-account-corporate"), CorporateHoldingsCap, true);

        if (!_idCard.TryFindIdCard(user, out var card) || card.Comp.AccountNumber == 0 || _bank.IsLocked(card.Comp, out _))
            return null;

        return new Trader(
            BankPrefix + card.Comp.AccountNumber,
            Loc.GetString("cmu-markets-account-personal", ("number", card.Comp.AccountNumber)),
            CivilianHoldingsCap,
            false);
    }

    public int GetBalance(Trader trader)
    {
        if (trader.Corporate)
            return (int) _corporate.GetCorporateBudget();

        return FindCard(trader.Key) is { } card ? card.card.AccountBalance : 0;
    }

    private bool TryDebit(Trader trader, int amount)
    {
        if (trader.Corporate)
        {
            if (_corporate.GetCorporateBudget() < amount)
                return false;

            _corporate.AddToCorporateBudget(-amount);
            _corporateMarketFlow -= amount;
            return true;
        }

        if (FindCard(trader.Key) is not { } found || found.card.AccountBalance < amount)
            return false;

        found.card.AccountBalance -= amount;
        Dirty(found.uid, found.card);
        _bank.RecordTransaction(found.uid, AtmHistoryKind.MarketBuy, amount);
        return true;
    }

    private void Credit(Trader trader, int amount)
    {
        CreditAccount(trader.Key, amount, AtmHistoryKind.MarketSell);
    }

    private bool CreditAccount(string key, int amount, AtmHistoryKind kind = AtmHistoryKind.MarketDividend)
    {
        if (key == CorporateAccount)
        {
            _corporate.AddToCorporateBudget(amount);
            _corporateMarketFlow += amount;
            return true;
        }

        if (FindCard(key) is not { } found)
            return false;

        found.card.AccountBalance += amount;
        Dirty(found.uid, found.card);
        _bank.RecordTransaction(found.uid, kind, amount);
        return true;
    }

    private (EntityUid uid, Content.Shared.Access.Components.IdCardComponent card)? FindCard(string key)
    {
        if (!key.StartsWith(BankPrefix) || !int.TryParse(key.AsSpan(BankPrefix.Length), out var number))
            return null;

        return _bank.FindAccount(number);
    }
}
