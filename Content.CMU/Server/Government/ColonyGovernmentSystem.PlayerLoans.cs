// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.Government;

namespace Content.Server.CMU14.Government;

// The loans of the city to the players. The mayor offers a sum to a player who stands by the console (with the number of the instalments and the interest of the whole term), and the player takes it at
// his own console or not. The sum comes out of the budget, so the mayor risks the money of the city. The instalments are taken from the account of the borrower, one for a cycle. A borrower who misses two
// is in default: his licences are taken back, the latest of his plots is taken back, the city does not deal with him for half an hour (no loans, no bids), and the rest of the debt is the loss of the city.
public sealed partial class ColonyGovernmentSystem
{
    public const int PlayerLoanCycleSeconds = 300;
    public const int PlayerLoanMissedLimit = 2;
    public const int LoanOfferMinutes = 3;
    public const int MaxLoanOffers = 5;
    public const int DefaulterMinutes = 30;

    private static readonly int[] LoanPrincipals = { 500, 1000, 2000, 5000, 10000 };
    private static readonly int[] LoanInstalments = { 3, 5, 8 };
    private static readonly int[] LoanInterests = { 5, 10, 15, 20 };

    private static int InstalmentOf(int principal, int instalments, int interest)
    {
        return (int) Math.Ceiling(principal * (100 + interest) / 100f / instalments);
    }

    #region The offers

    private void OnOfferLoan(Entity<ColonyGovernmentConsoleComponent> console, ref OfferLoanMessage msg)
    {
        var comp = EnsureGovernment().Comp;
        var target = GetEntity(msg.Target);
        var now = _timing.CurTime;
        if (target == msg.Actor || !Near(console.Owner, target) || comp.LoanOffers.Count >= MaxLoanOffers
            || !LoanPrincipals.Contains(msg.Principal) || !LoanInstalments.Contains(msg.Instalments) || !LoanInterests.Contains(msg.Interest))
            return;

        var name = MetaData(target).EntityName;
        if (IsDefaulter(comp, name, now) || _bank.FindOwnedCard(target) == null || _budget.GetBudget() < msg.Principal
            || comp.PlayerLoans.Any(l => l.Borrower == target) || comp.LoanOffers.Any(o => o.Borrower == target))
            return;

        comp.LoanOffers.Add(new LoanOffer(comp.NextLoanOfferId++, target, name, msg.Principal, msg.Instalments, msg.Interest, now + TimeSpan.FromMinutes(LoanOfferMinutes)));
        _popup.PopupEntity(Loc.GetString("rumc-playerloan-offered-to-you"), console.Owner, target);
        Refresh();
    }

    private void OnWithdrawLoanOffer(Entity<ColonyGovernmentConsoleComponent> console, ref WithdrawLoanOfferMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var offerId = msg.Offer;
        government.Comp.LoanOffers.RemoveAll(o => o.Id == offerId);
        Refresh();
    }

    // The player takes the loan: the sum comes out of the budget onto his account.
    private void OnAcceptLoan(Entity<CitizenConsoleComponent> console, ref AcceptLoanMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var comp = government.Comp;
        var actor = msg.Actor;
        var offerId = msg.Offer;
        if (comp.LoanOffers.FirstOrDefault(o => o.Id == offerId) is not { } offer || offer.Borrower != actor)
            return;

        if (_budget.GetBudget() < offer.Principal || _bank.FindOwnedCard(actor) == null || comp.PlayerLoans.Any(l => l.Borrower == actor))
        {
            _popup.PopupEntity(Loc.GetString("rumc-playerloan-cannot"), console.Owner, actor);
            return;
        }

        comp.LoanOffers.Remove(offer);
        _budget.AddToBudget(-offer.Principal);
        Pay(actor, offer.Principal);

        var instalment = InstalmentOf(offer.Principal, offer.Instalments, offer.Interest);
        comp.PlayerLoans.Add(new PlayerLoan(comp.NextPlayerLoanId++, actor, offer.Name, offer.Principal, instalment, offer.Instalments,
            _timing.CurTime + TimeSpan.FromSeconds(PlayerLoanCycleSeconds)));

        _popup.PopupEntity(Loc.GetString("rumc-playerloan-taken", ("sum", offer.Principal)), console.Owner, actor);
        Refresh();
    }

    private void OnDeclineLoan(Entity<CitizenConsoleComponent> console, ref DeclineLoanMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var actor = msg.Actor;
        var offerId = msg.Offer;
        government.Comp.LoanOffers.RemoveAll(o => o.Id == offerId && o.Borrower == actor);
        Refresh();
    }

    #endregion

    #region The loans

    // The borrower pays all that is left at once.
    private void OnRepayLoanEarly(Entity<CitizenConsoleComponent> console, ref RepayLoanEarlyMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var comp = government.Comp;
        var actor = msg.Actor;
        var loanId = msg.Loan;
        if (comp.PlayerLoans.FirstOrDefault(l => l.Id == loanId) is not { } loan || loan.Borrower != actor)
            return;

        var owed = loan.Left * loan.Instalment;
        if (_bank.FindOwnedCard(actor) == null || BalanceOf(actor) < owed)
        {
            _popup.PopupEntity(Loc.GetString("rumc-playerloan-no-money", ("sum", owed)), console.Owner, actor);
            return;
        }

        Pay(actor, -owed);
        _budget.AddToBudget(owed);
        comp.PlayerLoans.Remove(loan);
        _popup.PopupEntity(Loc.GetString("rumc-playerloan-repaid", ("sum", owed)), console.Owner, actor);
        Refresh();
    }

    // The mayor forgives a loan: what is left is not asked.
    private void OnForgiveLoan(Entity<ColonyGovernmentConsoleComponent> console, ref ForgiveLoanMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var loanId = msg.Loan;
        government.Comp.PlayerLoans.RemoveAll(l => l.Id == loanId);
        Refresh();
    }

    private void UpdatePlayerLoans(ColonyGovernmentComponent comp, TimeSpan now)
    {
        comp.LoanOffers.RemoveAll(o => o.Until <= now || !Exists(o.Borrower));

        foreach (var name in comp.Defaulters.Where(d => d.Value <= now).Select(d => d.Key).ToList())
            comp.Defaulters.Remove(name);

        foreach (var loan in comp.PlayerLoans.ToList())
        {
            if (now < loan.NextDue)
                continue;

            loan.NextDue += TimeSpan.FromSeconds(PlayerLoanCycleSeconds);

            if (Exists(loan.Borrower) && _bank.FindOwnedCard(loan.Borrower) != null && BalanceOf(loan.Borrower) >= loan.Instalment)
            {
                Pay(loan.Borrower, -loan.Instalment);
                _budget.AddToBudget(loan.Instalment);
                loan.Left--;
                loan.Missed = 0;
                if (loan.Left <= 0)
                {
                    comp.PlayerLoans.Remove(loan);
                    if (Exists(loan.Borrower))
                        _popup.PopupEntity(Loc.GetString("rumc-playerloan-paid-off"), loan.Borrower, loan.Borrower);
                }

                continue;
            }

            loan.Missed++;
            if (Exists(loan.Borrower))
                _popup.PopupEntity(Loc.GetString("rumc-playerloan-missed", ("sum", loan.Instalment)), loan.Borrower, loan.Borrower);

            if (loan.Missed >= PlayerLoanMissedLimit)
                DefaultPlayerLoan(comp, loan, now);
        }
    }

    // A default: the licences of the borrower are taken back, so is the latest of his plots, and the city does not deal with him for a time.
    private void DefaultPlayerLoan(ColonyGovernmentComponent comp, PlayerLoan loan, TimeSpan now)
    {
        comp.PlayerLoans.Remove(loan);
        comp.Defaulters[loan.Name] = now + TimeSpan.FromMinutes(DefaulterMinutes);

        foreach (var record in comp.Licenses.Values.Where(r => r.Holder == loan.Name && !r.Revoked))
        {
            record.Revoked = true;
            if (record.Card is { } card && TryComp(card, out CityLicenseCardComponent? cardComp))
                cardComp.Revoked = true;
        }

        var name = loan.Name;
        var plot = comp.Plots.Where(p => p.Holder == name).OrderByDescending(p => p.Id).FirstOrDefault();
        if (plot != null)
            TakePlotBack(comp, plot, true);
    }

    #endregion

    #region The state

    private List<LoanOfferEntry> LoanOfferEntries(ColonyGovernmentComponent comp)
    {
        return comp.LoanOffers
            .Select(o => new LoanOfferEntry(o.Id, GetNetEntity(o.Borrower), o.Name, o.Principal, o.Instalments, o.Interest, InstalmentOf(o.Principal, o.Instalments, o.Interest)))
            .ToList();
    }

    private List<PlayerLoanEntry> PlayerLoanEntries(ColonyGovernmentComponent comp, TimeSpan now)
    {
        return comp.PlayerLoans
            .Select(l => new PlayerLoanEntry(l.Id, l.Name, l.Principal, l.Instalment, l.Left, l.Missed, Math.Max(0, (int) Math.Ceiling((l.NextDue - now).TotalSeconds))))
            .ToList();
    }

    #endregion
}
