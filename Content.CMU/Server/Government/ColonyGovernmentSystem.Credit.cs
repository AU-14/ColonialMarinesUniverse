// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.Government;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Government;

public sealed partial class ColonyGovernmentSystem
{
    // How many loans the city can have at once, how many things it can have money in at once, the step of a loan, and how many instalments missed make a default.
    public const int MaxLoans = 3;
    public const int MaxInvestments = 2;
    public const int LoanStep = 500;
    public const int DefaultAfterMissed = 3;

    #region Credit

    private static void ShiftRating(ColonyGovernmentComponent comp, int amount)
    {
        comp.CreditRating = Math.Clamp(comp.CreditRating + amount, 0, 100);
    }

    // How much more a lender will lend: its limit at the rating (half of its sum at nought, one and a half at a hundred), less what is owed to it, in whole steps.
    private static int LenderAvailable(ColonyGovernmentComponent comp, ColonyLenderPrototype lender)
    {
        var limit = (int) (lender.MaxAmount * (0.5f + comp.CreditRating / 100f));
        var owed = comp.Loans.Where(l => l.Lender == lender.ID).Sum(l => l.Instalment * l.Left);
        return Math.Max(0, limit - owed) / LoanStep * LoanStep;
    }

    // Why a lender will not lend now (null if it will).
    private string? LoanBlocked(ColonyGovernmentComponent comp, ColonyLenderPrototype lender, TimeSpan now)
    {
        if (comp.CreditRating < lender.MinRating)
            return Loc.GetString("rumc-government-loan-blocked-rating", ("rating", lender.MinRating));

        if (comp.LenderBlockedUntil.TryGetValue(lender.ID, out var until) && until > now)
            return Loc.GetString("rumc-government-loan-blocked-default", ("minutes", (int) Math.Ceiling((until - now).TotalMinutes)));

        if (comp.Loans.Count >= MaxLoans)
            return Loc.GetString("rumc-government-loan-blocked-many", ("max", MaxLoans));

        if (LenderAvailable(comp, lender) < LoanStep)
            return Loc.GetString("rumc-government-loan-blocked-limit");

        return null;
    }

    private void OnTakeLoan(Entity<ColonyGovernmentConsoleComponent> console, ref TakeLoanMessage msg)
    {
        var government = EnsureGovernment();
        var comp = government.Comp;
        var now = _timing.CurTime;
        if (!_proto.TryIndex<ColonyLenderPrototype>(msg.Lender, out var lender) || LoanBlocked(comp, lender, now) != null)
            return;

        var amount = Math.Clamp(msg.Amount / LoanStep * LoanStep, 0, LenderAvailable(comp, lender));
        if (amount < LoanStep)
            return;

        var instalments = Math.Max(1, lender.Instalments);
        var total = (int) MathF.Ceiling(amount * (1f + lender.InterestPercent / 100f));
        var instalment = (int) MathF.Ceiling(total / (float) instalments);
        comp.Loans.Add(new Loan(comp.NextLoanId++, lender.ID, instalment, instalments, now + TimeSpan.FromSeconds(lender.CycleSeconds)));
        _budget.AddToBudget(amount);

        Refresh();
    }

    // The loan is paid off at once: what is left of it, with no more interest.
    private void OnRepayLoan(Entity<ColonyGovernmentConsoleComponent> console, ref RepayLoanMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var comp = government.Comp;
        var loanId = msg.Loan;
        var loan = comp.Loans.FirstOrDefault(l => l.Id == loanId);
        if (loan == null)
            return;

        var remaining = loan.Instalment * loan.Left;
        if (_budget.GetBudget() < remaining)
            return;

        _budget.AddToBudget(-remaining);
        comp.Loans.Remove(loan);
        ShiftRating(comp, 3);

        Refresh();
    }

    private string LenderName(string id)
    {
        return _proto.TryIndex<ColonyLenderPrototype>(id, out var lender) ? Loc.GetString(lender.Name) : id;
    }

    private void UpdateCredit(ColonyGovernmentComponent comp, TimeSpan now)
    {
        foreach (var loan in comp.Loans.ToList())
        {
            // A default earlier in the loop may have taken this one away.
            if (!comp.Loans.Contains(loan) || now < loan.NextDue)
                continue;

            if (!_proto.TryIndex<ColonyLenderPrototype>(loan.Lender, out var lender))
            {
                comp.Loans.Remove(loan);
                continue;
            }

            var cycle = TimeSpan.FromSeconds(lender.CycleSeconds);
            if (_budget.GetBudget() >= loan.Instalment)
            {
                _budget.AddToBudget(-loan.Instalment);
                loan.Left--;
                ShiftRating(comp, 2);

                if (loan.Left <= 0)
                {
                    comp.Loans.Remove(loan);
                    ShiftRating(comp, 5);
                }
                else
                {
                    loan.NextDue += cycle;
                }

                continue;
            }

            // An instalment that cannot be paid: the rating falls, the instalment grows by a twentieth, and three such are a default.
            loan.Missed++;
            ShiftRating(comp, -10);
            loan.Instalment += (int) MathF.Ceiling(loan.Instalment * 0.05f);
            loan.NextDue += cycle;

            if (loan.Missed >= DefaultAfterMissed)
                Default(comp, lender, now);
        }

        foreach (var investment in comp.Investments)
        {
            if (investment.Done || now < investment.ReadyAt || !_proto.TryIndex<ColonyInvestmentPrototype>(investment.Proto, out var proto))
                continue;

            investment.Done = true;
            investment.Success = _random.Prob(InvestmentChance(comp, proto));

            if (investment.Success)
            {
                _budget.AddToBudget(proto.Cost * proto.PayoutMultiplier);
                ShiftRating(comp, proto.RatingOnSuccess);
            }
            else
            {
                _budget.AddToBudget(proto.Cost * proto.SalvageMultiplier);
                ShiftRating(comp, proto.RatingOnFailure);
            }
        }
    }

    // A default: the lender takes a half of what is owed to it out of the treasury (or the treasury, if it is less), takes the loans away, does not lend again for twenty minutes, and the rating falls hard.
    private void Default(ColonyGovernmentComponent comp, ColonyLenderPrototype lender, TimeSpan now)
    {
        var owed = comp.Loans.Where(l => l.Lender == lender.ID).Sum(l => l.Instalment * l.Left);
        var seized = Math.Min(_budget.GetBudget(), owed * 0.5f);
        if (seized > 0f)
            _budget.AddToBudget(-seized);

        comp.Loans.RemoveAll(l => l.Lender == lender.ID);
        comp.LenderBlockedUntil[lender.ID] = now + TimeSpan.FromMinutes(20);
        ShiftRating(comp, -30);
    }

    #endregion

    #region Investments

    // The chance that a thing pays: its own, moved a little by the credit rating.
    private static float InvestmentChance(ColonyGovernmentComponent comp, ColonyInvestmentPrototype proto)
    {
        return Math.Clamp(proto.SuccessChance + (comp.CreditRating - 50) / 400f, 0.1f, 0.95f);
    }

    private string? InvestmentBlocked(ColonyGovernmentComponent comp, ColonyInvestmentPrototype proto)
    {
        if (comp.Investments.Count(i => !i.Done) >= MaxInvestments)
            return Loc.GetString("rumc-government-investment-blocked-many", ("max", MaxInvestments));

        if (_budget.GetBudget() < proto.Cost)
            return Loc.GetString("rumc-government-blocked-money");

        return null;
    }

    private void OnInvest(Entity<ColonyGovernmentConsoleComponent> console, ref InvestMessage msg)
    {
        var government = EnsureGovernment();
        var comp = government.Comp;
        if (!_proto.TryIndex<ColonyInvestmentPrototype>(msg.Investment, out var proto) || InvestmentBlocked(comp, proto) != null)
            return;

        _budget.AddToBudget(-proto.Cost);
        comp.Investments.Add(new Investment(comp.NextInvestmentId++, proto.ID, _timing.CurTime + TimeSpan.FromSeconds(proto.DurationSeconds)));

        Refresh();
    }

    #endregion
}
