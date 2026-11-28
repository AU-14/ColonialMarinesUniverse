using System.Linq;
using Content.Shared.CMU14.CorporateRelations;

namespace Content.Server.CMU14.CorporateRelations;

public sealed partial class CMUCorporateRelationsSystem
{
    public const int MaxLoan = 10000;
    public const int MaxInterestPercent = 100;
    public const int Installments = 3;

    /// <summary>
    ///     Missed instalments in a row before WY takes the next one anyway, into the red.
    /// </summary>
    public const int MissesBeforeForcedCollection = 3;

    /// <summary>
    ///     What the colony already owes WY when the round starts, so it can't play dirty straight away.
    /// </summary>
    public const int StartingDebt = 1500;

    /// <summary>
    ///     Interest on the starting debt, charged up front like any loan's and again as a late fee.
    /// </summary>
    public const int StartingDebtInterest = 15;

    /// <summary>
    ///     What the colony owes at the start of the round: the debt plus its interest.
    /// </summary>
    public const int StartingDebtOwed = StartingDebt * (100 + StartingDebtInterest) / 100;

    private readonly Dictionary<string, Loan> _loans = new();

    private sealed class Loan
    {
        public required string Borrower;
        public CMULoanStage Stage;
        public int Amount;
        public int InterestPercent;
        public float Owed;
        public int InstallmentsLeft;
        public bool StartingDebt;
        public int MissedInARow;
    }

    private void InitializeLoans()
    {
        SubscribeLocalEvent<CMUContractsConsoleComponent, CMUContractsRequestLoanBuiMsg>(OnRequestLoan);
        SubscribeLocalEvent<CMUContractsConsoleComponent, CMUContractsCounterLoanBuiMsg>(OnCounterLoan);
        SubscribeLocalEvent<CMUContractsConsoleComponent, CMUContractsAnswerLoanBuiMsg>(OnAnswerLoan);
        SubscribeLocalEvent<CMUContractsConsoleComponent, CMUContractsCallInLoanBuiMsg>(OnCallInLoan);
    }

    private void StartLoans()
    {
        _loans[ColonyBorrower] = new Loan
        {
            Borrower = ColonyBorrower,
            Stage = CMULoanStage.Active,
            Amount = StartingDebt,
            InterestPercent = StartingDebtInterest,
            Owed = StartingDebtOwed,
            InstallmentsLeft = Installments,
            StartingDebt = true,
        };
    }

    /// <summary>
    ///     What <paramref name="borrower"/> still owes WY, or zero.
    /// </summary>
    public float GetOwed(string borrower)
    {
        return _loans.TryGetValue(borrower, out var loan) && loan.Stage == CMULoanStage.Active ? loan.Owed : 0f;
    }

    private CMULoanInfo ToInfo(Loan loan)
    {
        return new CMULoanInfo(
            loan.Borrower,
            BorrowerName(loan.Borrower),
            loan.Stage,
            loan.Amount,
            loan.InterestPercent,
            (int) MathF.Ceiling(loan.Owed),
            loan.InstallmentsLeft,
            loan.StartingDebt);
    }

    private void CollectInstallments()
    {
        foreach (var loan in _loans.Values.ToList())
        {
            if (loan.Stage != CMULoanStage.Active)
                continue;

            var installment = MathF.Min(loan.Owed, MathF.Ceiling(loan.Owed / Math.Max(1, loan.InstallmentsLeft)));
            if (GetBorrowerBudget(loan.Borrower) < installment)
            {
                loan.MissedInARow++;

                // A borrower that keeps its budget just short would never pay, so after enough
                // misses in a row the instalment is taken anyway, into the red.
                if (loan.MissedInARow >= MissesBeforeForcedCollection)
                {
                    loan.MissedInARow = 0;
                    Announce("cmu-contracts-announce-loan-forced", ("borrower", BorrowerName(loan.Borrower)), ("amount", (int) installment));
                }
                else
                {
                    // A missed instalment grows the debt by the loan's own rate; the next ones get bigger.
                    ApplyLateFee(loan);
                    Announce("cmu-contracts-announce-loan-missed",
                        ("borrower", BorrowerName(loan.Borrower)),
                        ("owed", (int) loan.Owed),
                        ("left", MissesBeforeForcedCollection - loan.MissedInARow));
                    continue;
                }
            }
            else
            {
                loan.MissedInARow = 0;
            }

            Repay(loan, installment);
            loan.InstallmentsLeft = Math.Max(1, loan.InstallmentsLeft - 1);
        }
    }

    private void Repay(Loan loan, float amount)
    {
        AdjustBorrowerBudget(loan.Borrower, -amount);
        _corporate.AddToCorporateBudget(amount);
        loan.Owed -= amount;

        if (loan.Owed > 0.5f)
            return;

        _loans.Remove(loan.Borrower);
        Announce("cmu-contracts-announce-loan-repaid", ("borrower", BorrowerName(loan.Borrower)));
    }

    private void OnRequestLoan(Entity<CMUContractsConsoleComponent> ent, ref CMUContractsRequestLoanBuiMsg args)
    {
        if (GetBorrower(ent) is not { } borrower)
            return;

        if (_withdrawn)
        {
            Refuse(ent, args.Actor, "cmu-contracts-refuse-withdrawn");
            return;
        }

        if (_loans.ContainsKey(borrower))
        {
            Refuse(ent, args.Actor, "cmu-contracts-refuse-loan-exists");
            return;
        }

        var amount = Math.Clamp(args.Amount, 1, MaxLoan);
        _loans[borrower] = new Loan
        {
            Borrower = borrower,
            Stage = CMULoanStage.AwaitingCorporate,
            Amount = amount,
        };

        Announce("cmu-contracts-announce-loan-requested", ("borrower", BorrowerName(borrower)), ("amount", amount));
        UpdateAllUi();
    }

    private void OnCounterLoan(Entity<CMUContractsConsoleComponent> ent, ref CMUContractsCounterLoanBuiMsg args)
    {
        if (!TryGetNegotiation(ent, args.Borrower, out var loan, out var corporate))
            return;

        loan.Amount = Math.Clamp(args.Amount, 1, MaxLoan);
        loan.InterestPercent = Math.Clamp(args.InterestPercent, 0, MaxInterestPercent);
        loan.Stage = corporate ? CMULoanStage.AwaitingBorrower : CMULoanStage.AwaitingCorporate;
        UpdateAllUi();
    }

    private void OnAnswerLoan(Entity<CMUContractsConsoleComponent> ent, ref CMUContractsAnswerLoanBuiMsg args)
    {
        if (!TryGetNegotiation(ent, args.Borrower, out var loan, out _))
            return;

        if (!args.Accept)
        {
            _loans.Remove(loan.Borrower);
            Announce("cmu-contracts-announce-loan-declined", ("borrower", BorrowerName(loan.Borrower)));
            UpdateAllUi();
            return;
        }

        if (_corporate.GetCorporateBudget() < loan.Amount)
        {
            Refuse(ent, args.Actor, "cmu-contracts-refuse-corporate-funds");
            return;
        }

        _corporate.AddToCorporateBudget(-loan.Amount);
        AdjustBorrowerBudget(loan.Borrower, loan.Amount);
        loan.Stage = CMULoanStage.Active;
        loan.Owed = MathF.Ceiling(loan.Amount * (1 + loan.InterestPercent / 100f));
        loan.InstallmentsLeft = Installments;

        Announce("cmu-contracts-announce-loan-agreed",
            ("borrower", BorrowerName(loan.Borrower)),
            ("amount", loan.Amount),
            ("interest", loan.InterestPercent));
        UpdateAllUi();
    }

    /// <summary>
    ///     Finds a loan still being negotiated that it is this console's turn to answer.
    /// </summary>
    private bool TryGetNegotiation(Entity<CMUContractsConsoleComponent> ent, string requested, out Loan loan, out bool corporate)
    {
        corporate = ent.Comp.Side == CMUContractsSide.Corporate;
        var borrower = corporate ? requested : GetBorrower(ent);
        loan = default!;

        if (borrower == null || !_loans.TryGetValue(borrower, out var found) || found.Stage == CMULoanStage.Active)
            return false;

        // A side can only answer when the other side made the last move.
        var turn = corporate ? CMULoanStage.AwaitingCorporate : CMULoanStage.AwaitingBorrower;
        if (found.Stage != turn)
            return false;

        loan = found;
        return true;
    }

    private void OnCallInLoan(Entity<CMUContractsConsoleComponent> ent, ref CMUContractsCallInLoanBuiMsg args)
    {
        if (ent.Comp.Side != CMUContractsSide.Corporate ||
            !_loans.TryGetValue(args.Borrower, out var loan) ||
            loan.Stage != CMULoanStage.Active)
        {
            return;
        }

        if (GetBorrowerBudget(loan.Borrower) >= loan.Owed)
        {
            Announce("cmu-contracts-announce-loan-called-paid", ("borrower", BorrowerName(loan.Borrower)), ("owed", (int) MathF.Ceiling(loan.Owed)));
            Repay(loan, loan.Owed);
        }
        else
        {
            // Can't pay it all now: everything falls due at the next cycle, with the late fee, and
            // the usual run of misses before it's forced.
            ApplyLateFee(loan);
            loan.InstallmentsLeft = 1;
            Announce("cmu-contracts-announce-loan-called-short", ("borrower", BorrowerName(loan.Borrower)), ("owed", (int) loan.Owed));
        }

        UpdateAllUi();
    }

    /// <summary>
    ///     Collects every outstanding loan in full on the spot, even from borrowers who can't cover
    ///     it: their budget goes negative. Loans still being negotiated are dropped.
    /// </summary>
    /// <summary>
    ///     Charges the loan's agreed interest again on what's still owed. A loan agreed at 0% has
    ///     no late fee, only the forced collection.
    /// </summary>
    private static void ApplyLateFee(Loan loan)
    {
        // Whole percentages, so $1600 at 20% is $1920 and not a float's $1921.
        loan.Owed = MathF.Ceiling(loan.Owed * (100 + loan.InterestPercent) / 100);
    }

    private void CollectAllLoans()
    {
        foreach (var loan in _loans.Values.ToList())
        {
            if (loan.Stage != CMULoanStage.Active)
            {
                _loans.Remove(loan.Borrower);
                continue;
            }

            Repay(loan, loan.Owed);
        }
    }
}
