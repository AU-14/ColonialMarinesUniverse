// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.CMU14.Government;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Government;

// Client side of the console of the people: opens the window, gives it the state, and sends what the player does.
public sealed class CitizenConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private CitizenConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CitizenConsoleWindow>();
        _window.OnSubmitAppeal += text => SendMessage(new SubmitAppealMessage(text));
        _window.OnVote += (poll, option) => SendMessage(new VoteMessage(poll, option));
        _window.OnDeliver += order => SendMessage(new DeliverOrderMessage(order));
        _window.OnPropose += (kind, partner, sum, subject, plot) => SendMessage(new ProposeContractMessage(kind, partner, sum, subject, plot));
        _window.OnSign += proposal => SendMessage(new SignContractMessage(proposal));
        _window.OnCancel += proposal => SendMessage(new CancelContractMessage(proposal));
        _window.OnBidAuction += (auction, amount) => SendMessage(new BidAuctionMessage(auction, amount));
        _window.OnBidTender += (tender, price) => SendMessage(new BidTenderMessage(tender, price));
        _window.OnAcceptLoan += offer => SendMessage(new AcceptLoanMessage(offer));
        _window.OnDeclineLoan += offer => SendMessage(new DeclineLoanMessage(offer));
        _window.OnRepayLoan += loan => SendMessage(new RepayLoanEarlyMessage(loan));
        _window.OnApplySubsidy += (business, form) => SendMessage(new ApplySubsidyMessage(business, form));
        _window.OnWithdrawApplication += application => SendMessage(new WithdrawApplicationMessage(application));
        _window.OnOfferSale += (buyer, price) => SendMessage(new OfferSaleMessage(buyer, price));
        _window.OnAcceptSale += offer => SendMessage(new AcceptSaleMessage(offer));
        _window.OnDeclineSale += offer => SendMessage(new DeclineSaleMessage(offer));
        _window.OnStartItemAuction += (price, minutes) => SendMessage(new StartItemAuctionMessage(price, minutes));
        _window.OnCancelItemAuction += auction => SendMessage(new CancelItemAuctionMessage(auction));
        _window.OnBidItemAuction += (auction, amount) => SendMessage(new BidItemAuctionMessage(auction, amount));
        _window.OnOrderWorld += (good, lots) => SendMessage(new OrderWorldGoodMessage(good, lots));
        _window.OnCancelWorld += order => SendMessage(new CancelWorldOrderMessage(order));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window != null && state is CitizenConsoleState citizen)
            _window.SetState(citizen);
    }
}
