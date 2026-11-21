using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.CorporateRelations;

[Serializable, NetSerializable]
public enum CMUContractsUi : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum CMUContractsSide : byte
{
    Corporate,
    Colony,
    Ambassador,
}

[Serializable, NetSerializable]
public enum CMULoanStage : byte
{
    /// <summary>Weyland-Yutani has to answer the current terms.</summary>
    AwaitingCorporate,

    /// <summary>The borrower has to answer the current terms.</summary>
    AwaitingBorrower,

    Active,
}

[Serializable, NetSerializable]
public readonly record struct CMUWorkOrderGoodInfo(string Id, string Name, int UnitValue, int MaxQuantity);

[Serializable, NetSerializable]
public readonly record struct CMUWorkOrderInfo(int Id, string Good, int Quantity, int Delivered, int PremiumPercent, TimeSpan ExpiresAt);

[Serializable, NetSerializable]
public readonly record struct CMULoanInfo(
    string Borrower,
    string BorrowerName,
    CMULoanStage Stage,
    int Amount,
    int InterestPercent,
    int Owed,
    int InstallmentsLeft,
    bool StartingDebt);

[Serializable, NetSerializable]
public sealed class CMUContractsBuiState : BoundUserInterfaceState
{
    public required CMUContractsSide Side;

    /// <summary>
    ///     The borrower this console speaks for (colony or an ambassador's faction), if any.
    /// </summary>
    public required string? Borrower;

    public required int CorporateBudget;
    public required int BorrowerBudget;
    public required TimeSpan NextCycleAt;
    public required int Rent;
    public required int RentCap;
    public required int CorporateArrears;
    public required bool ResearchLicense;
    public required bool TaxBreak;
    public required bool WorkOrdersFrozen;
    public required TimeSpan? WithdrawalAt;
    public required bool Withdrawn;
    public required int MaxPremiumPercent;
    public required int MaxWorkOrders;
    public required int MaxLoan;
    public required List<CMUWorkOrderGoodInfo> Goods;
    public required List<CMUWorkOrderInfo> WorkOrders;
    public required List<CMULoanInfo> Loans;
}

// ── Colony ──────────────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class CMUContractsSetRentBuiMsg(int rent) : BoundUserInterfaceMessage
{
    public readonly int Rent = rent;
}

[Serializable, NetSerializable]
public sealed class CMUContractsSetResearchLicenseBuiMsg(bool granted) : BoundUserInterfaceMessage
{
    public readonly bool Granted = granted;
}

[Serializable, NetSerializable]
public sealed class CMUContractsSetTaxBreakBuiMsg(bool granted) : BoundUserInterfaceMessage
{
    public readonly bool Granted = granted;
}

// ── Corporate ───────────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class CMUContractsPostWorkOrderBuiMsg(string good, int quantity, int premiumPercent) : BoundUserInterfaceMessage
{
    public readonly string Good = good;
    public readonly int Quantity = quantity;
    public readonly int PremiumPercent = premiumPercent;
}

[Serializable, NetSerializable]
public sealed class CMUContractsCancelWorkOrderBuiMsg(int id) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
}

[Serializable, NetSerializable]
public sealed class CMUContractsFreezeWorkOrdersBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUContractsCallInLoanBuiMsg(string borrower) : BoundUserInterfaceMessage
{
    public readonly string Borrower = borrower;
}

[Serializable, NetSerializable]
public sealed class CMUContractsWithdrawalBuiMsg(bool start) : BoundUserInterfaceMessage
{
    public readonly bool Start = start;
}

// ── Loans, either side. Borrower is ignored when a borrower console sends it. ─

[Serializable, NetSerializable]
public sealed class CMUContractsRequestLoanBuiMsg(int amount) : BoundUserInterfaceMessage
{
    public readonly int Amount = amount;
}

[Serializable, NetSerializable]
public sealed class CMUContractsCounterLoanBuiMsg(string borrower, int amount, int interestPercent) : BoundUserInterfaceMessage
{
    public readonly string Borrower = borrower;
    public readonly int Amount = amount;
    public readonly int InterestPercent = interestPercent;
}

[Serializable, NetSerializable]
public sealed class CMUContractsAnswerLoanBuiMsg(string borrower, bool accept) : BoundUserInterfaceMessage
{
    public readonly string Borrower = borrower;
    public readonly bool Accept = accept;
}

/// <summary>
///     Sent by a console's own window to open the contracts window.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUContractsOpenBuiMsg : BoundUserInterfaceMessage;
