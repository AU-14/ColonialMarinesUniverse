// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Server.CMU14.ColonyEconomy;
using Content.Server.CMU14.WorldMarket;
using Content.Server.Shuttles.Events;
using Content.Shared._RMC14.Construction;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Interaction;
using Content.Shared.CMU14.Government;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Government;

// The foreign policy of the colony: the treaties with the factions of the ambassadors, and the petitions and the ultimatums that the ambassadors send, answered by the administrator. What the
// treaties do reaches the rest of the game by a few numbers that others ask of this system: the points off the import duty (the market of the world), the share off the calls of the third
// parties (the console of the administration), and the money that comes in every minute (here). The treaties give the ambassadors their points of influence, and a sanction
// shakes the trust of an ambassador in his own mother faction.
public sealed partial class ColonyGovernmentSystem : EntitySystem
{
    [Dependency] private readonly ColonyBankSystem _bank = default!;
    [Dependency] private readonly ColonyBudgetSystem _budget = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly WorldMarketSystem _market = default!;

    // How many treaties may be in force, and how many petitions wait at once.
    public const int BaseTreatySlots = 2;
    public const int MaxPetitions = 3;
    public const int MaxCallDiscount = 50;

    private TimeSpan _nextSecond;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, ProposeTreatyMessage>(OnPropose);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, BreakTreatyMessage>(OnBreak);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, AnswerPetitionMessage>(OnAnswer);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, TakeLoanMessage>(OnTakeLoan);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, RepayLoanMessage>(OnRepayLoan);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, InvestMessage>(OnInvest);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, CityGiftMessage>(OnCityGift);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, CityAidMessage>(OnCityAid);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, SignAccordMessage>(OnSignAccord);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, BreakAccordMessage>(OnBreakAccord);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, IssueLicenseMessage>(OnIssueLicense);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, RevokeLicenseMessage>(OnRevokeLicense);

        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, GetVerbsEvent<AlternativeVerb>>(OnConsoleVerbs);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, SetLandTaxMessage>(OnSetLandTax);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, SetEnforceMessage>(OnSetEnforce);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, IssueSurveyorMessage>(OnIssueSurveyor);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, RemoveZoneMessage>(OnRemoveZone);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, AllotPlotMessage>(OnAllotPlot);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, DeclineRequestMessage>(OnDeclineRequest);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, RevokePlotMessage>(OnRevokePlot);

        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, GrantAwardMessage>(OnGrantAward);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, CreateOrderMessage>(OnCreateOrder);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, CancelOrderMessage>(OnCancelOrder);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, AnswerAppealMessage>(OnAnswerAppeal);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, StartPollMessage>(OnStartPoll);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, ClosePollMessage>(OnClosePoll);

        SubscribeLocalEvent<CitizenConsoleComponent, BoundUIOpenedEvent>(OnCitizenOpened);
        SubscribeLocalEvent<CitizenConsoleComponent, SubmitAppealMessage>(OnSubmitAppeal);
        SubscribeLocalEvent<CitizenConsoleComponent, VoteMessage>(OnVote);
        SubscribeLocalEvent<CitizenConsoleComponent, DeliverOrderMessage>(OnDeliverOrder);
        SubscribeLocalEvent<CitizenConsoleComponent, ProposeContractMessage>(OnProposeContract);
        SubscribeLocalEvent<CitizenConsoleComponent, SignContractMessage>(OnSignContract);
        SubscribeLocalEvent<CitizenConsoleComponent, CancelContractMessage>(OnCancelContract);

        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, StartAuctionMessage>(OnStartAuction);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, CancelAuctionMessage>(OnCancelAuction);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, StartTenderMessage>(OnStartTender);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, CancelTenderMessage>(OnCancelTender);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, OfferLoanMessage>(OnOfferLoan);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, WithdrawLoanOfferMessage>(OnWithdrawLoanOffer);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, ForgiveLoanMessage>(OnForgiveLoan);

        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, StartProjectMessage>(OnStartProject);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, CancelProjectMessage>(OnCancelProject);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, RunCovertMessage>(OnRunCovert);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, PayQuotaMessage>(OnPayQuota);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, ApproveSubsidyMessage>(OnApproveSubsidy);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, RejectSubsidyMessage>(OnRejectSubsidy);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, CancelSubsidyMessage>(OnCancelSubsidy);
        SubscribeLocalEvent<CitizenConsoleComponent, ApplySubsidyMessage>(OnApplySubsidy);
        SubscribeLocalEvent<CitizenConsoleComponent, WithdrawApplicationMessage>(OnWithdrawApplication);
        SubscribeLocalEvent<ColonyGovernmentConsoleComponent, SetSalesTaxMessage>(OnSetSalesTax);
        SubscribeLocalEvent<CitizenConsoleComponent, OfferSaleMessage>(OnOfferSale);
        SubscribeLocalEvent<CitizenConsoleComponent, AcceptSaleMessage>(OnAcceptSale);
        SubscribeLocalEvent<CitizenConsoleComponent, DeclineSaleMessage>(OnDeclineSale);
        SubscribeLocalEvent<CitizenConsoleComponent, StartItemAuctionMessage>(OnStartItemAuction);
        SubscribeLocalEvent<CitizenConsoleComponent, CancelItemAuctionMessage>(OnCancelItemAuction);
        SubscribeLocalEvent<CitizenConsoleComponent, BidItemAuctionMessage>(OnBidItemAuction);

        SubscribeLocalEvent<ForemanConsoleComponent, BoundUIOpenedEvent>(OnForemanOpened);
        SubscribeLocalEvent<ForemanConsoleComponent, SelectCityMessage>(OnSelectCity);
        SubscribeLocalEvent<ForemanConsoleComponent, SelectZoneMessage>(OnSelectZone);
        SubscribeLocalEvent<ForemanConsoleComponent, OrderWorldGoodMessage>(OnOrderColony);
        SubscribeLocalEvent<ForemanConsoleComponent, CancelWorldOrderMessage>(OnCancelWorldOrder);
        SubscribeLocalEvent<ForemanConsoleComponent, CallShuttleMessage>(OnCallShuttle);
        SubscribeLocalEvent<ForemanConsoleComponent, DepartShuttleMessage>(OnDepartShuttle);
        SubscribeLocalEvent<CitizenConsoleComponent, OrderWorldGoodMessage>(OnOrderPersonal);
        SubscribeLocalEvent<CitizenConsoleComponent, CancelWorldOrderMessage>(OnCancelPersonalOrder);
        SubscribeLocalEvent<DropshipNavigationComputerComponent, InteractUsingEvent>(OnShuttleConsoleUsing);
        SubscribeLocalEvent<FTLCompletedEvent>(OnFtlCompleted);

        SubscribeLocalEvent<CitizenConsoleComponent, BidAuctionMessage>(OnBidAuction);
        SubscribeLocalEvent<CitizenConsoleComponent, BidTenderMessage>(OnBidTender);
        SubscribeLocalEvent<CitizenConsoleComponent, AcceptLoanMessage>(OnAcceptLoan);
        SubscribeLocalEvent<CitizenConsoleComponent, DeclineLoanMessage>(OnDeclineLoan);
        SubscribeLocalEvent<CitizenConsoleComponent, RepayLoanEarlyMessage>(OnRepayLoanEarly);

        SubscribeLocalEvent<SurveyorTabletComponent, GetVerbsEvent<AlternativeVerb>>(OnTabletVerbs);
        SubscribeLocalEvent<SurveyorTabletComponent, ExaminedEvent>(OnTabletExamined);
        SubscribeLocalEvent<SurveyorTabletComponent, MapInitEvent>(OnTabletInit);
        SubscribeLocalEvent<RMCConstructionAttemptEvent>(OnConstructionAttempt);
    }

    #region The state

    public bool TryGovernment(out Entity<ColonyGovernmentComponent> government)
    {
        var query = EntityQueryEnumerator<ColonyGovernmentComponent>();
        if (query.MoveNext(out var uid, out var comp))
        {
            government = (uid, comp);
            return true;
        }

        government = default;
        return false;
    }

    private Entity<ColonyGovernmentComponent> EnsureGovernment()
    {
        if (TryGovernment(out var known))
            return known;

        var uid = Spawn(null, MapCoordinates.Nullspace);
        var comp = AddComp<ColonyGovernmentComponent>(uid);
        var now = _timing.CurTime;
        comp.Started = true;
        comp.NextMinute = now + TimeSpan.FromMinutes(1);
        comp.NextDrift = now + TimeSpan.FromMinutes(3);
        comp.NextPetition = now + TimeSpan.FromSeconds(_random.Next(300, 480));
        return (uid, comp);
    }

    private static int AttitudeOf(ColonyGovernmentComponent comp, string faction)
    {
        return comp.Attitude.GetValueOrDefault(faction);
    }

    private static void ShiftAttitude(ColonyGovernmentComponent comp, string faction, int amount)
    {
        comp.Attitude[faction] = Math.Clamp(AttitudeOf(comp, faction) + amount, -100, 100);
    }

    // What the treaties in force give together.
    private readonly record struct Totals(float Income, int DutyCut, int CallDiscount, int TreatySlots);

    private Totals TotalsOf(ColonyGovernmentComponent comp)
    {
        var income = 0f;
        var duty = 0;
        var discount = 0;
        var slots = BaseTreatySlots;

        foreach (var treaty in comp.Treaties)
        {
            if (!_proto.TryIndex<ColonyTreatyPrototype>(treaty.Id, out var proto))
                continue;

            income += proto.IncomePerMinute;
            duty += proto.DutyCut;
            discount += proto.CallDiscountPercent;
        }

        // The projects with the outside that stand.
        foreach (var project in ActiveProjects(comp))
        {
            income += project.IncomePerMinute;
            duty += project.DutyCut;
            discount += project.CallDiscountPercent;
        }

        // The agreements with the cities of the map, and the things that have paid.
        foreach (var accord in comp.Accords)
        {
            if (!_proto.TryIndex<ColonyAccordPrototype>(accord.Id, out var proto))
                continue;

            income += proto.IncomePerMinute;
            duty += proto.DutyCut;
            discount += proto.CallDiscountPercent;
        }

        foreach (var investment in comp.Investments)
        {
            if (investment.Done && investment.Success && _proto.TryIndex<ColonyInvestmentPrototype>(investment.Proto, out var proto))
                income += proto.IncomePerMinute;
        }

        return new Totals(income, duty, Math.Min(discount, MaxCallDiscount), slots);
    }

    // What the others ask: the points that are taken off the import duty of the market, and the share that is taken off the cost of a third party that the console calls.
    public int DutyCut()
    {
        return TryGovernment(out var government) ? TotalsOf(government.Comp).DutyCut : 0;
    }

    public int CallDiscountPercent()
    {
        return TryGovernment(out var government) ? TotalsOf(government.Comp).CallDiscount : 0;
    }

    #endregion

    #region The time

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextSecond)
            return;

        _nextSecond = now + TimeSpan.FromSeconds(1);

        // The government is made when the first ambassador comes on the line (the console of the administration makes it when it is opened).
        if (!TryGovernment(out var government))
        {
            if (_market.KnownFactions().Count == 0)
                return;

            government = EnsureGovernment();
        }

        var comp = government.Comp;

        if (now >= comp.NextMinute)
        {
            comp.NextMinute += TimeSpan.FromMinutes(1);
            Minute(comp);
        }

        if (now >= comp.NextDrift)
        {
            comp.NextDrift += TimeSpan.FromMinutes(3);
            foreach (var faction in comp.Attitude.Keys.ToList())
            {
                var value = comp.Attitude[faction];
                comp.Attitude[faction] = value > 0 ? value - 1 : value < 0 ? value + 1 : 0;
            }

            // The good will of a city is kept up, or it fades.
            foreach (var city in comp.CityRelation.Keys.ToList())
                comp.CityRelation[city] = Math.Max(0, comp.CityRelation[city] - 1);
        }

        UpdateTreaties(comp, now);
        UpdatePetitions(comp, now);
        UpdateCredit(comp, now);
        UpdateAccords(comp, now);
        UpdateLand(comp, now);
        UpdateOrders(comp, now);
        UpdatePolls(comp, now);
        UpdateProposals(comp, now);
        UpdateAuctions(comp, now);
        UpdateTenders(comp, now);
        UpdatePlayerLoans(comp, now);
        UpdateProjects(comp, now);
        UpdateQuota(comp, now);
        UpdateSubsidies(comp, now);
        UpdateSaleOffers(comp, now);
        UpdateItemAuctions(comp, now);
        UpdateWorldOrders(comp, now);
        UpdateShuttle(comp, now);

        RefreshUis(government);
    }

    // The money that what is in force brings, and the points that the treaties have earned for the ambassadors of the partners.
    private void Minute(ColonyGovernmentComponent comp)
    {
        var totals = TotalsOf(comp);
        if (totals.Income != 0f)
            _budget.AddToBudget(totals.Income);

        ProjectMinute(comp);
        WorldMinute(comp);
        CovertMinute(comp);

        foreach (var treaty in comp.Treaties)
        {
            if (!_proto.TryIndex<ColonyTreatyPrototype>(treaty.Id, out var proto) || proto.PartnerPointsPerMinute <= 0f)
                continue;

            comp.PointsOwed[treaty.Faction] = comp.PointsOwed.GetValueOrDefault(treaty.Faction) + proto.PartnerPointsPerMinute;
        }

        foreach (var faction in comp.PointsOwed.Keys.ToList())
        {
            var whole = (int) MathF.Floor(comp.PointsOwed[faction]);
            if (whole <= 0)
                continue;

            comp.PointsOwed[faction] -= whole;
            _market.GrantPoints(faction, whole);
        }
    }

    private void UpdateTreaties(ColonyGovernmentComponent comp, TimeSpan now)
    {
        foreach (var treaty in comp.Treaties.ToList())
        {
            _proto.TryIndex<ColonyTreatyPrototype>(treaty.Id, out var proto);
            var name = proto != null ? Loc.GetString(proto.Name) : treaty.Id;

            if (treaty.Until <= now)
            {
                comp.Treaties.Remove(treaty);
                continue;
            }

            // A faction that has come to think ill of the colony does not keep a treaty (a sanction is not a treaty that anyone keeps: it stands for its time).
            if (proto != null && proto.Kind != TreatyKind.Sanction && AttitudeOf(comp, treaty.Faction) < -10)
            {
                comp.Treaties.Remove(treaty);
            }
        }
    }

    #endregion
}
