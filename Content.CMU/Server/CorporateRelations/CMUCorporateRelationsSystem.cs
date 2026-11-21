using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.CMU14.Ambassador;
using Content.Server.CMU14.Chat;
using Content.Server.CMU14.ColonyEconomy;
using Content.Server.CMU14.CorporateWork;
using Content.Server.Popups;
using Content.Shared.CMU14.Ambassador;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.CorporateRelations;
using Content.Shared.GameTicking;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.CorporateRelations;

/// <summary>
///     The money relationship between Weyland-Yutani and the colony. Every <see cref="Cycle"/> the
///     colony collects rent from WY and loan instalments fall due. The colony holds WY's research
///     licence and tax break over its head; WY holds loans, work orders and the withdrawal over the
///     colony's. Every move either side makes is announced.
/// </summary>
public sealed partial class CMUCorporateRelationsSystem : EntitySystem
{
    [Dependency] private AmbassadorConsoleSystem _ambassador = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private ColonyBudgetSystem _colonyBudget = default!;
    [Dependency] private CorporateConsoleSystem _corporate = default!;
    [Dependency] private CMUMarketsSystem _markets = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public static readonly TimeSpan Cycle = TimeSpan.FromMinutes(15);
    public const int RentCap = 1500;
    public const string ColonyBorrower = "colony";

    /// <summary>
    ///     The round force whose company stock moves with how WY is doing in the colony.
    /// </summary>
    public const string CompanyForce = "WY";

    /// <summary>
    ///     Weyland-Yutani's own stock.
    /// </summary>
    public const string CompanyStock = "CMUStockWeylandYutani";

    public const float LicenceLossShock = -0.15f;
    public const float WithdrawalShock = -0.5f;

    private static readonly SoundSpecifier AnnouncementSound = new SoundPathSpecifier("/Audio/Announcements/announce.ogg");
    private static readonly TimeSpan UiRefresh = TimeSpan.FromSeconds(5);

    private bool _started;
    private TimeSpan _nextCycleAt;
    private TimeSpan _nextUiRefresh;

    private int _rent;
    private float _arrears;
    private bool _researchLicense = true;
    private bool _taxBreak = true;

    /// <summary>
    ///     Whether WY's research earns money. The colony can revoke this licence.
    /// </summary>
    public bool CorporateResearchLicensed => _researchLicense;

    /// <summary>
    ///     Extra charge on WY's corporate ASRS orders, on top of sales tax, once the colony revokes
    ///     WY's tax break. WY buys little, so plain sales tax alone would barely be felt.
    /// </summary>
    public const float RevokedTaxBreakLevy = 0.5f;

    /// <summary>
    ///     Whether WY's own corporate ASRS orders skip colony sales tax. The colony can revoke this.
    /// </summary>
    public bool CorporateTaxExempt => _taxBreak;

    /// <summary>
    ///     What WY's corporate ASRS orders pay the colony on top of their price, as a fraction:
    ///     nothing with a tax break, sales tax plus <see cref="RevokedTaxBreakLevy"/> without one.
    /// </summary>
    public float CorporateOrderSurcharge(float salesTax) => _taxBreak ? 0f : salesTax + RevokedTaxBreakLevy;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<CMUContractsConsoleComponent, CMUContractsOpenBuiMsg>(OnOpen);
        SubscribeLocalEvent<CMUContractsConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CMUContractsConsoleComponent, CMUContractsSetRentBuiMsg>(OnSetRent);
        SubscribeLocalEvent<CMUContractsConsoleComponent, CMUContractsSetResearchLicenseBuiMsg>(OnSetResearchLicense);
        SubscribeLocalEvent<CMUContractsConsoleComponent, CMUContractsSetTaxBreakBuiMsg>(OnSetTaxBreak);
        InitializeLoans();
        InitializeWorkOrders();
        InitializeWithdrawal();
        InitializeResearch();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _started = false;
        _rent = 0;
        _arrears = 0;
        _researchLicense = true;
        _taxBreak = true;
        _loans.Clear();
        _workOrders.Clear();
        _workOrdersFrozen = false;
        _withdrawalAt = null;
        _withdrawn = false;
        _withdrawalTeamCalled = false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // An ordered withdrawal always runs to the end, colony or not.
        var now = _timing.CurTime;
        UpdateWithdrawal(now);

        if (!_started)
        {
            // Wait for a colony to exist; there is nothing to rent or owe without one.
            var colony = EntityQueryEnumerator<AdminConsoleComponent>();
            if (!colony.MoveNext(out _, out _))
                return;

            Start();
        }

        UpdateWorkOrders(now);

        if (now >= _nextCycleAt)
        {
            _nextCycleAt += Cycle;
            RunCycle();
        }

        if (now >= _nextUiRefresh)
        {
            _nextUiRefresh = now + UiRefresh;
            UpdateAllUi();
        }
    }

    private void Start()
    {
        _started = true;
        _nextCycleAt = _timing.CurTime + Cycle;
        StartLoans();
    }

    /// <summary>
    ///     Collects rent and instalments now. Runs on its own every <see cref="Cycle"/>.
    /// </summary>
    internal void RunCycle()
    {
        CollectRent();
        CollectInstallments();
        _workOrdersFrozen = false;
        UpdateAllUi();
    }

    // ── Rent and licences (colony's levers) ───────────────────────────────────

    private void CollectRent()
    {
        // A withdrawn WY has no site to rent.
        if (_withdrawn || (_rent <= 0 && _arrears <= 0))
            return;

        var due = _rent + _arrears;
        var paid = Math.Clamp(_corporate.GetCorporateBudget(), 0f, due);
        _corporate.AddToCorporateBudget(-paid);
        _colonyBudget.AddToBudget(paid);
        _arrears = due - paid;

        if (_arrears > 0)
            Announce("cmu-contracts-announce-rent-arrears", ("paid", (int) paid), ("arrears", (int) _arrears));
        else
            Announce("cmu-contracts-announce-rent-paid", ("paid", (int) paid));
    }

    private void OnSetRent(Entity<CMUContractsConsoleComponent> ent, ref CMUContractsSetRentBuiMsg args)
    {
        if (ent.Comp.Side != CMUContractsSide.Colony)
            return;

        var rent = Math.Clamp(args.Rent, 0, RentCap);
        if (rent == _rent)
            return;

        _rent = rent;
        Announce("cmu-contracts-announce-rent-set", ("rent", rent));
        UpdateAllUi();
    }

    private void OnSetResearchLicense(Entity<CMUContractsConsoleComponent> ent, ref CMUContractsSetResearchLicenseBuiMsg args)
    {
        if (ent.Comp.Side != CMUContractsSide.Colony || args.Granted == _researchLicense)
            return;

        _researchLicense = args.Granted;
        if (args.Granted)
            _markets.Shock(CompanyStock, 0.04f);
        else
            _markets.ReportSetback(CompanyForce, LicenceLossShock, "Research", "cmu-markets-setback-licence");
        Announce(args.Granted ? "cmu-contracts-announce-research-granted" : "cmu-contracts-announce-research-revoked");
        UpdateAllUi();
    }

    private void OnSetTaxBreak(Entity<CMUContractsConsoleComponent> ent, ref CMUContractsSetTaxBreakBuiMsg args)
    {
        if (ent.Comp.Side != CMUContractsSide.Colony || args.Granted == _taxBreak)
            return;

        _taxBreak = args.Granted;
        _markets.Shock(CompanyStock, args.Granted ? 0.02f : -0.03f);
        Announce(args.Granted ? "cmu-contracts-announce-tax-granted" : "cmu-contracts-announce-tax-revoked");
        UpdateAllUi();
    }

    // ── Budgets ───────────────────────────────────────────────────────────────

    private float GetBorrowerBudget(string borrower)
    {
        return borrower == ColonyBorrower
            ? _colonyBudget.GetBudget()
            : _ambassador.GetFactionBudget(borrower);
    }

    private void AdjustBorrowerBudget(string borrower, float amount)
    {
        if (borrower == ColonyBorrower)
            _colonyBudget.AddToBudget(amount);
        else
            _ambassador.AdjustFactionBudget(borrower, amount);
    }

    private string BorrowerName(string borrower)
    {
        return borrower == ColonyBorrower ? Loc.GetString("cmu-contracts-borrower-colony") : borrower;
    }

    /// <summary>
    ///     The borrower a console speaks for: the colony for the administrator, or the ambassador's faction.
    /// </summary>
    private string? GetBorrower(Entity<CMUContractsConsoleComponent> ent)
    {
        return ent.Comp.Side switch
        {
            CMUContractsSide.Colony => ColonyBorrower,
            CMUContractsSide.Ambassador => CompOrNull<AmbassadorConsoleComponent>(ent)?.FactionName,
            _ => null,
        };
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    private void OnOpen(Entity<CMUContractsConsoleComponent> ent, ref CMUContractsOpenBuiMsg args)
    {
        _ui.TryToggleUi(ent.Owner, CMUContractsUi.Key, args.Actor);
    }

    private void OnUiOpened(Entity<CMUContractsConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is CMUContractsUi.Key)
            UpdateUi(ent);
    }

    private void UpdateAllUi()
    {
        var query = EntityQueryEnumerator<CMUContractsConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_ui.IsUiOpen(uid, CMUContractsUi.Key))
                UpdateUi((uid, comp));
        }
    }

    private void UpdateUi(Entity<CMUContractsConsoleComponent> ent)
    {
        var borrower = GetBorrower(ent);
        var goods = _prototypes.EnumeratePrototypes<CMUWorkOrderGoodPrototype>()
            .OrderBy(g => g.ID)
            .Select(g => new CMUWorkOrderGoodInfo(g.ID, g.Name, g.UnitValue, g.MaxQuantity))
            .ToList();

        var orders = _workOrders
            .Select(o => new CMUWorkOrderInfo(o.Id, _prototypes.Index(o.Good).Name, o.Quantity, o.Delivered, o.PremiumPercent, o.ExpiresAt))
            .ToList();

        // Borrowers only see their own loan; WY sees every loan.
        var loans = _loans.Values
            .Where(l => ent.Comp.Side == CMUContractsSide.Corporate || l.Borrower == borrower)
            .Select(ToInfo)
            .ToList();

        var state = new CMUContractsBuiState
        {
            Side = ent.Comp.Side,
            Borrower = borrower,
            CorporateBudget = (int) _corporate.GetCorporateBudget(),
            BorrowerBudget = borrower != null ? (int) GetBorrowerBudget(borrower) : 0,
            NextCycleAt = _nextCycleAt,
            Rent = _rent,
            RentCap = RentCap,
            CorporateArrears = (int) _arrears,
            ResearchLicense = _researchLicense,
            TaxBreak = _taxBreak,
            WorkOrdersFrozen = _workOrdersFrozen,
            WithdrawalAt = _withdrawalAt,
            Withdrawn = _withdrawn,
            MaxPremiumPercent = MaxPremiumPercent,
            MaxWorkOrders = MaxWorkOrders,
            MaxLoan = MaxLoan,
            Goods = goods,
            WorkOrders = orders,
            Loans = loans,
        };

        _ui.SetUiState(ent.Owner, CMUContractsUi.Key, state);
    }

    private void Announce(string message, params (string, object)[] args)
    {
        _chat.DispatchFilteredAnnouncement(
            ColonyAnnouncements.Recipients(EntityManager),
            Loc.GetString(message, args),
            sender: Loc.GetString("cmu-contracts-announcement-sender"),
            playSound: true,
            announcementSound: AnnouncementSound);
    }

    private void Refuse(EntityUid console, EntityUid user, string message, params (string, object)[] args)
    {
        _popup.PopupEntity(Loc.GetString(message, args), console, user);
    }
}
