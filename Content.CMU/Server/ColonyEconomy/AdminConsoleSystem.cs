using System.Linq;
using Content.Server.AU14.Ambassador;
using Content.Server.AU14.ThirdParty;
using Content.Server.Chat.Systems;
using Content.Shared.AU14.Ambassador;
using Content.Shared.AU14.ColonyEconomy;
using Content.Shared.AU14.Threats;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.ColonyEconomy;

public sealed class AdminConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ColonyBudgetSystem _colonyBudget = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly AuThirdPartySystem _thirdParty = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AdminConsoleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AdminConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<AdminConsoleComponent, AdminConsoleSetTaxBuiMsg>(OnSetTax);
        SubscribeLocalEvent<AdminConsoleComponent, AdminConsoleSetIncomeTaxBuiMsg>(OnSetIncomeTax);
        SubscribeLocalEvent<AdminConsoleComponent, AdminConsoleCallThirdPartyBuiMsg>(OnCallThirdParty);
        SubscribeLocalEvent<AdminConsoleComponent, AdminConsoleOpenThirdPartyBuiMsg>(OnOpenThirdParty);
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
            return;
        }
    }


    public float GetSalesTax()
    {
        var query = EntityQueryEnumerator<AdminConsoleComponent>();
        return query.MoveNext(out _, out var comp) ? comp.SalesTaxPercent / 100f : 0f;
    }

    public float GetIncomeTax()
    {
        var query = EntityQueryEnumerator<AdminConsoleComponent>();
        return query.MoveNext(out _, out var comp) ? comp.IncomeTaxPercent / 100f : 0f;
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
                embargoes.Add(amb.FactionName ?? "Unknown Faction");
            if (amb.TradePactActive)
                tradePacts.Add(amb.FactionName ?? "Unknown Faction");
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
            _chat.DispatchGlobalAnnouncement(
                $"Colony sales tax has been set to {clamped:F0}%.",
                "Administration",
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
            _chat.DispatchGlobalAnnouncement(
                $"Colony income tax has been set to {clamped:F0}%. This affects salary payouts and corporate withdrawals.",
                "Administration",
                playSound: true,
                announcementSound: sound);
        }

        UpdateAllUi();
    }

    private void OnCallThirdParty(EntityUid uid, AdminConsoleComponent comp, AdminConsoleCallThirdPartyBuiMsg msg)
    {
        if (!comp.CallableParties.TryGetValue(msg.ThirdPartyId, out var cost))
            return;
        if (comp.CalledParties.Contains(msg.ThirdPartyId))
            return;
        if (_colonyBudget.GetBudget() < cost)
            return;
        if (!_proto.TryIndex<AuThirdPartyPrototype>(msg.ThirdPartyId, out var partyProto))
            return;
        if (!_proto.TryIndex(partyProto.PartySpawn, out var spawnProto))
            return;

        _colonyBudget.AddToBudget(-cost);

        // Mark as called on all admin consoles
        var q = EntityQueryEnumerator<AdminConsoleComponent>();
        while (q.MoveNext(out _, out var c))
            c.CalledParties.Add(msg.ThirdPartyId);

        _thirdParty.SpawnThirdParty(partyProto, spawnProto, false);
        UpdateAllUi();
    }

    // ── UI helpers ───────────────────────────────────────────────────────────

    private void UpdateUiState(EntityUid uid, AdminConsoleComponent comp)
    {
        var econ = BuildEconomyStatus();
        _ui.SetUiState(uid, AdminConsoleUi.Key,
            new AdminConsoleBuiState(comp.SalesTaxPercent, comp.IncomeTaxPercent, _colonyBudget.GetBudget(), econ));

        var thirdParties = new Dictionary<string, (string DisplayName, float Cost)>();
        foreach (var (id, cost) in comp.CallableParties)
        {
            if (_proto.TryIndex<AuThirdPartyPrototype>(id, out var proto))
                thirdParties[id] = (proto.DisplayName ?? proto.ID, cost);
        }
        _ui.SetUiState(uid, AdminConsoleThirdPartyUi.Key,
            new AdminConsoleThirdPartyBuiState(_colonyBudget.GetBudget(), thirdParties, comp.CalledParties));
    }

    public void UpdateAllUi()
    {
        var query = EntityQueryEnumerator<AdminConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
            UpdateUiState(uid, comp);
    }
}

