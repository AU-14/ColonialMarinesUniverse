// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.CMU14.Government;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Government;

// Client side of the window of the city relations, which the console of the administration opens: opens the window, gives it the state, and sends what the administrator does.
public sealed class ColonyGovernmentBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private ColonyGovernmentWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ColonyGovernmentWindow>();
        _window.OnPropose += (treaty, faction) => SendMessage(new ProposeTreatyMessage(treaty, faction));
        _window.OnBreak += (treaty, faction) => SendMessage(new BreakTreatyMessage(treaty, faction));
        _window.OnAnswer += (petition, answer) => SendMessage(new AnswerPetitionMessage(petition, answer));
        _window.OnTakeLoan += (lender, amount) => SendMessage(new TakeLoanMessage(lender, amount));
        _window.OnRepayLoan += loan => SendMessage(new RepayLoanMessage(loan));
        _window.OnInvest += investment => SendMessage(new InvestMessage(investment));
        _window.OnGift += city => SendMessage(new CityGiftMessage(city));
        _window.OnAid += city => SendMessage(new CityAidMessage(city));
        _window.OnSignAccord += (accord, city) => SendMessage(new SignAccordMessage(accord, city));
        _window.OnBreakAccord += (accord, city) => SendMessage(new BreakAccordMessage(accord, city));
        _window.OnIssue += (target, license) => SendMessage(new IssueLicenseMessage(target, license));
        _window.OnRevoke += serial => SendMessage(new RevokeLicenseMessage(serial));
        _window.OnSetTax += tax => SendMessage(new SetLandTaxMessage(tax));
        _window.OnSetSalesTax += tax => SendMessage(new SetSalesTaxMessage(tax));
        _window.OnSetEnforce += enforce => SendMessage(new SetEnforceMessage(enforce));
        _window.OnIssueSurveyor += () => SendMessage(new IssueSurveyorMessage());
        _window.OnRemoveZone += zone => SendMessage(new RemoveZoneMessage(zone));
        _window.OnAllot += request => SendMessage(new AllotPlotMessage(request));
        _window.OnDeclineRequest += request => SendMessage(new DeclineRequestMessage(request));
        _window.OnRevokePlot += plot => SendMessage(new RevokePlotMessage(plot));
        _window.OnGrantAward += (target, award, reason) => SendMessage(new GrantAwardMessage(target, award, reason));
        _window.OnCreateOrder += (kind, amount, price) => SendMessage(new CreateOrderMessage(kind, amount, price));
        _window.OnCancelOrder += order => SendMessage(new CancelOrderMessage(order));
        _window.OnAnswerAppeal += (appeal, accept, reply) => SendMessage(new AnswerAppealMessage(appeal, accept, reply));
        _window.OnStartPoll += (question, options, minutes) => SendMessage(new StartPollMessage(question, options, minutes));
        _window.OnClosePoll += () => SendMessage(new ClosePollMessage());
        _window.OnStartAuction += (kind, price, minutes) => SendMessage(new StartAuctionMessage(kind, price, minutes));
        _window.OnCancelAuction += auction => SendMessage(new CancelAuctionMessage(auction));
        _window.OnStartTender += (kind, amount, price, minutes) => SendMessage(new StartTenderMessage(kind, amount, price, minutes));
        _window.OnCancelTender += tender => SendMessage(new CancelTenderMessage(tender));
        _window.OnOfferLoan += (target, principal, instalments, interest) => SendMessage(new OfferLoanMessage(target, principal, instalments, interest));
        _window.OnWithdrawLoanOffer += offer => SendMessage(new WithdrawLoanOfferMessage(offer));
        _window.OnForgiveLoan += loan => SendMessage(new ForgiveLoanMessage(loan));
        _window.OnStartProject += (project, target) => SendMessage(new StartProjectMessage(project, target));
        _window.OnCancelProject += project => SendMessage(new CancelProjectMessage(project));
        _window.OnRunCovert += (affair, target) => SendMessage(new RunCovertMessage(affair, target));
        _window.OnPayQuota += amount => SendMessage(new PayQuotaMessage(amount));
        _window.OnApproveSubsidy += application => SendMessage(new ApproveSubsidyMessage(application));
        _window.OnRejectSubsidy += application => SendMessage(new RejectSubsidyMessage(application));
        _window.OnCancelSubsidy += subsidy => SendMessage(new CancelSubsidyMessage(subsidy));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window != null && state is ColonyGovernmentState government)
            _window.SetState(government);
    }
}
