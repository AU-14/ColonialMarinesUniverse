using System.Linq;
using Content.Server.CMU14.Ambassador;
using Content.Server.CMU14.Chat;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.Ops.ThirdParty;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared.CMU14.Ambassador;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Threats;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using ThirdPartySystem = Content.Server.CMU14.Ops.ThirdParty.ThirdPartySystem;

namespace Content.Server.CMU14.ColonyEconomy;

public sealed partial class AdminConsoleSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private ColonyBudgetSystem _colonyBudget = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private ThirdPartySystem _thirdParty = default!;
    [Dependency] private AuRoundSystem _auRound = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private Robust.Shared.Timing.IGameTiming _timing = default!;
    [Dependency] private Content.Server.CMU14.Government.ColonyGovernmentSystem _government = default!; // RUMC: the reforms and the treaties make the calls cheaper
    [Dependency] private Content.Server.CMU14.WorldMarket.WorldMarketSystem _worldMarket = default!; // RUMC: the holidays from taxes that the ambassadors order

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AdminConsoleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AdminConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<AdminConsoleComponent, AdminConsoleSetTaxBuiMsg>(OnSetTax);
        SubscribeLocalEvent<AdminConsoleComponent, AdminConsoleSetIncomeTaxBuiMsg>(OnSetIncomeTax);
        SubscribeLocalEvent<AdminConsoleComponent, AdminConsoleSetTradeTaxBuiMsg>(OnSetTradeTax); // RUMC: the tax on the parcels sold through the trade platforms
        SubscribeLocalEvent<AdminConsoleComponent, AdminConsoleCallThirdPartyBuiMsg>(OnCallThirdParty);
        SubscribeLocalEvent<AdminConsoleComponent, AdminConsoleOpenThirdPartyBuiMsg>(OnOpenThirdParty);
        SubscribeLocalEvent<AdminConsoleComponent, AdminConsoleOpenGovernmentBuiMsg>(OnOpenGovernment); // RUMC: the window of the government of the colony
    }

    /// <summary>
    ///     When a new admin console spawns, copy the current sales-tax value from any
    ///     existing admin console so all terminals start in sync.
    /// </summary>
    private void OnMapInit(EntityUid uid, AdminConsoleComponent comp, MapInitEvent args)
    {
        var query = EntityQueryEnumerator<AdminConsoleComponent>();
        while (query.MoveNext(out var otherUid, out var other))
        {
            if (otherUid == uid)
                continue;

            comp.SalesTaxPercent = other.SalesTaxPercent;
            comp.IncomeTaxPercent = other.IncomeTaxPercent;
            comp.TradeTaxPercent = other.TradeTaxPercent; // RUMC: a new console takes the trade tax of the one that is already there
            return;
        }
    }


    public float GetSalesTax()
    {
        var query = EntityQueryEnumerator<AdminConsoleComponent>();
        return query.MoveNext(out _, out var comp) ? Math.Max(0f, comp.SalesTaxPercent - _worldMarket.TaxBreakPoints(Content.Shared.CMU14.WorldMarket.WorldTaxKind.Sales)) / 100f : 0f; // RUMC: minus the holiday
    }

    // RUMC: the trade tax as a share (0 to 0.5), for the platforms that take it from a sale
    public float GetTradeTax()
    {
        var query = EntityQueryEnumerator<AdminConsoleComponent>();
        return query.MoveNext(out _, out var comp) ? Math.Max(0f, comp.TradeTaxPercent - _worldMarket.TaxBreakPoints(Content.Shared.CMU14.WorldMarket.WorldTaxKind.Trade)) / 100f : 0f; // RUMC: minus the holiday
    }

    public float GetIncomeTax()
    {
        var query = EntityQueryEnumerator<AdminConsoleComponent>();
        return query.MoveNext(out _, out var comp) ? Math.Max(0f, comp.IncomeTaxPercent - _worldMarket.TaxBreakPoints(Content.Shared.CMU14.WorldMarket.WorldTaxKind.Income)) / 100f : 0f; // RUMC: minus the holiday
    }

    /// <summary>
    ///     Builds the shared economy status from all relevant systems.
    /// </summary>
    public EconomyStatusState BuildEconomyStatus()
    {
        float salesTax = 0f;
        float incomeTax = 0f;
        var adminQ = EntityQueryEnumerator<AdminConsoleComponent>();
        if (adminQ.MoveNext(out _, out var adminComp))
        {
            salesTax = adminComp.SalesTaxPercent;
            incomeTax = adminComp.IncomeTaxPercent;
        }

        float tariff = 0f;
        var corpQ = EntityQueryEnumerator<CorporateConsoleComponent>();
        if (corpQ.MoveNext(out _, out var corpComp))
            tariff = corpComp.TransitTariffPercent;

        var embargoes = new List<string>();
        var tradePacts = new List<string>();
        var ambQ = EntityQueryEnumerator<AmbassadorConsoleComponent>();
        while (ambQ.MoveNext(out _, out var amb))
        {
            if (amb.EmbargoActive)
                embargoes.Add(amb.FactionName ?? Loc.GetString("colony-economy-unknown-faction"));
            if (amb.TradePactActive)
                tradePacts.Add(amb.FactionName ?? Loc.GetString("colony-economy-unknown-faction"));
        }

        return new EconomyStatusState(salesTax, incomeTax, tariff, embargoes, tradePacts);
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void OnUiOpened(EntityUid uid, AdminConsoleComponent comp, BoundUIOpenedEvent args)
    {
        UpdateUiState(uid, comp);
    }

    private void OnOpenThirdParty(EntityUid uid, AdminConsoleComponent comp, AdminConsoleOpenThirdPartyBuiMsg msg)
    {
        _ui.TryToggleUi(uid, AdminConsoleThirdPartyUi.Key, msg.Actor);
    }

    // RUMC: the window of the government of the colony (the reforms and the foreign policy)
    private void OnOpenGovernment(EntityUid uid, AdminConsoleComponent comp, AdminConsoleOpenGovernmentBuiMsg msg)
    {
        _ui.TryToggleUi(uid, Content.Shared.CMU14.Government.ColonyGovernmentUi.Key, msg.Actor);
    }

    private void OnSetTax(EntityUid uid, AdminConsoleComponent comp, AdminConsoleSetTaxBuiMsg msg)
    {
        var clamped = Math.Clamp(msg.TaxPercent, 0f, 50f);
        var oldTax = comp.SalesTaxPercent;

        // Sync to ALL admin consoles so every terminal shows the same value
        var query = EntityQueryEnumerator<AdminConsoleComponent>();
        while (query.MoveNext(out _, out var c))
            c.SalesTaxPercent = clamped;

        if (Math.Abs(oldTax - clamped) > 0.01f)
        {
            var sound = new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Announcements/announce.ogg");
            _chat.DispatchFilteredAnnouncement(
                ColonyAnnouncements.Recipients(EntityManager),
                Loc.GetString("admin-console-sales-tax-announcement", ("percent", $"{clamped:F0}")),
                sender: Loc.GetString("admin-console-announcement-sender"),
                playSound: true,
                announcementSound: sound);
        }

        UpdateAllUi();
    }

    // RUMC: sets the trade tax on every console, so that all show one value, and announces a change to the colony
    private void OnSetTradeTax(EntityUid uid, AdminConsoleComponent comp, AdminConsoleSetTradeTaxBuiMsg msg)
    {
        var clamped = Math.Clamp(msg.TaxPercent, 0f, 50f);
        var oldTax = comp.TradeTaxPercent;

        // Sync to ALL admin consoles so every terminal shows the same value
        var query = EntityQueryEnumerator<AdminConsoleComponent>();
        while (query.MoveNext(out _, out var c))
            c.TradeTaxPercent = clamped;

        if (Math.Abs(oldTax - clamped) > 0.01f)
        {
            var sound = new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Announcements/announce.ogg");
            _chat.DispatchFilteredAnnouncement(
                ColonyAnnouncements.Recipients(EntityManager),
                Loc.GetString("admin-console-trade-tax-announcement", ("percent", $"{clamped:F0}")),
                sender: Loc.GetString("admin-console-announcement-sender"),
                playSound: true,
                announcementSound: sound);
        }

        UpdateAllUi();
    }

    private void OnSetIncomeTax(EntityUid uid, AdminConsoleComponent comp, AdminConsoleSetIncomeTaxBuiMsg msg)
    {
        var clamped = Math.Clamp(msg.TaxPercent, 0f, 50f);
        var oldTax = comp.IncomeTaxPercent;

        // Sync to ALL admin consoles so every terminal shows the same value
        var query = EntityQueryEnumerator<AdminConsoleComponent>();
        while (query.MoveNext(out _, out var c))
            c.IncomeTaxPercent = clamped;

        if (Math.Abs(oldTax - clamped) > 0.01f)
        {
            var sound = new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Announcements/announce.ogg");
            _chat.DispatchFilteredAnnouncement(
                ColonyAnnouncements.Recipients(EntityManager),
                Loc.GetString("admin-console-income-tax-announcement", ("percent", $"{clamped:F0}")),
                sender: Loc.GetString("admin-console-announcement-sender"),
                playSound: true,
                announcementSound: sound);
        }

        UpdateAllUi();
    }

    private void OnCallThirdParty(EntityUid uid, AdminConsoleComponent comp, AdminConsoleCallThirdPartyBuiMsg msg)
    {
        if (!comp.CallableParties.TryGetValue(msg.ThirdPartyId, out var cost))
            return;
        cost = cost * (100 - _government.CallDiscountPercent()) / 100f; // RUMC: the reforms and the treaties of the government of the colony make a call cheaper
        if (comp.CalledParties.Contains(msg.ThirdPartyId))
            return;
        if (comp.CalledParties.Count >= comp.MaxCallIns || _timing.CurTime < comp.NextCallInAt)
            return;
        if (_colonyBudget.GetBudget() < cost)
            return;
        if (!_proto.TryIndex<ThirdPartyPrototype>(msg.ThirdPartyId, out var partyProto))
            return;
        if (!_auRound.IsThirdPartyAllowedForCurrentContext(partyProto))
            return;
        if (!_proto.TryIndex(partyProto.PartySpawn, out var spawnProto))
            return;

        _thirdParty.SpawnThirdParty(partyProto, spawnProto, false);

        _colonyBudget.AddToBudget(-cost);

        // Mark as called on all admin consoles
        var nextCallIn = _timing.CurTime + comp.CallInCooldown;
        var q = EntityQueryEnumerator<AdminConsoleComponent>();
        while (q.MoveNext(out _, out var c))
        {
            c.CalledParties.Add(msg.ThirdPartyId);
            c.NextCallInAt = nextCallIn;
        }

        UpdateAllUi();
    }

    // ── UI helpers ───────────────────────────────────────────────────────────

    private void UpdateUiState(EntityUid uid, AdminConsoleComponent comp)
    {
        var econ = BuildEconomyStatus();
        _ui.SetUiState(uid, AdminConsoleUi.Key,
            // RUMC: the trade tax is given to the window too; the call that was here: new AdminConsoleBuiState(comp.SalesTaxPercent, comp.IncomeTaxPercent, _colonyBudget.GetBudget(), econ));
            new AdminConsoleBuiState(comp.SalesTaxPercent, comp.IncomeTaxPercent, _colonyBudget.GetBudget(), econ, comp.TradeTaxPercent));

        var thirdParties = new Dictionary<string, (string DisplayName, float Cost)>();
        foreach (var (id, cost) in comp.CallableParties)
        {
            if (_proto.TryIndex<ThirdPartyPrototype>(id, out var proto) &&
                _auRound.IsThirdPartyAllowedForCurrentContext(proto))
                thirdParties[id] = (proto.DisplayName ?? proto.ID, cost * (100 - _government.CallDiscountPercent()) / 100f); // RUMC: the same cheaper cost as the call itself
        }
        _ui.SetUiState(uid, AdminConsoleThirdPartyUi.Key,
            new AdminConsoleThirdPartyBuiState(_colonyBudget.GetBudget(), thirdParties, comp.CalledParties)
            {
                CallsLeft = Math.Max(0, comp.MaxCallIns - comp.CalledParties.Count),
                NextCallInAt = comp.NextCallInAt,
            });
    }

    public void UpdateAllUi()
    {
        var query = EntityQueryEnumerator<AdminConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
            UpdateUiState(uid, comp);
    }
}
