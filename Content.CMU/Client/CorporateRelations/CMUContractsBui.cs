using Content.Shared.CMU14.CorporateRelations;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.CorporateRelations;

public sealed class CMUContractsBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUContractsWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUContractsWindow>();
        _window.OnSetRent += rent => SendMessage(new CMUContractsSetRentBuiMsg(rent));
        _window.OnSetResearchLicense += granted => SendMessage(new CMUContractsSetResearchLicenseBuiMsg(granted));
        _window.OnSetTaxBreak += granted => SendMessage(new CMUContractsSetTaxBreakBuiMsg(granted));
        _window.OnPostOrder += (good, quantity, premium) => SendMessage(new CMUContractsPostWorkOrderBuiMsg(good, quantity, premium));
        _window.OnCancelOrder += id => SendMessage(new CMUContractsCancelWorkOrderBuiMsg(id));
        _window.OnFreezeOrders += () => SendMessage(new CMUContractsFreezeWorkOrdersBuiMsg());
        _window.OnRequestLoan += amount => SendMessage(new CMUContractsRequestLoanBuiMsg(amount));
        _window.OnCounterLoan += (borrower, amount, interest) => SendMessage(new CMUContractsCounterLoanBuiMsg(borrower, amount, interest));
        _window.OnAnswerLoan += (borrower, accept) => SendMessage(new CMUContractsAnswerLoanBuiMsg(borrower, accept));
        _window.OnCallInLoan += borrower => SendMessage(new CMUContractsCallInLoanBuiMsg(borrower));
        _window.OnWithdrawal += start => SendMessage(new CMUContractsWithdrawalBuiMsg(start));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not CMUContractsBuiState s)
            return;

        _window.UpdateState(s);
    }
}
