using Content.Server.AU14.ThirdParty;
using Content.Server.Chat.Systems;
using Content.Server.Stack;
using Content.Shared.AU14.ColonyEconomy;
using Content.Shared.AU14.Threats;
using Content.Shared.Stacks;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.ColonyEconomy;

public sealed class CorporateConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly AuThirdPartySystem _thirdParty = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly AdminConsoleSystem _adminConsole = default!;
    [Dependency] private readonly ColonyBudgetSystem _colonyBudget = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CorporateConsoleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CorporateConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CorporateConsoleComponent, CorporateConsoleSetTariffBuiMsg>(OnSetTariff);
        SubscribeLocalEvent<CorporateConsoleComponent, CorporateConsoleCallThirdPartyBuiMsg>(OnCallThirdParty);
        SubscribeLocalEvent<CorporateConsoleComponent, CorporateConsoleWithdrawBuiMsg>(OnWithdraw);
        SubscribeLocalEvent<CorporateConsoleComponent, CorporateConsoleOpenThirdPartyBuiMsg>(OnOpenThirdParty);
        SubscribeLocalEvent<CorporateConsoleComponent, EntInsertedIntoContainerMessage>(OnCashInserted);
        SubscribeLocalEvent<CorporateConsoleComponent, InteractUsingEvent>(OnInteractUsing);
    }

    /// <summary>
    ///     When a new corporate console spawns, copy tariff and budget from any existing
    ///     corporate console so all terminals start in sync.
    /// </summary>
    private void OnMapInit(EntityUid uid, CorporateConsoleComponent comp, MapInitEvent args)
    {
        var query = EntityQueryEnumerator<CorporateConsoleComponent>();
        while (query.MoveNext(out var otherUid, out var other))
        {
            if (otherUid == uid)
                continue;

            comp.TransitTariffPercent = other.TransitTariffPercent;
            comp.CorporateBudget = other.CorporateBudget;
            return;
        }
    }

    // ── Public query / mutation API ──────────────────────────────────────────

    /// <summary>
    ///     Returns the current transit tariff as a fraction (e.g. 0.15 = 15%).
    ///     Reads from the first CorporateConsoleComponent found.
    /// </summary>
    public float GetTariff()
    {
        var q = EntityQueryEnumerator<CorporateConsoleComponent>();
        return q.MoveNext(out _, out var comp) ? comp.TransitTariffPercent / 100f : 0f;
    }

    /// <summary>
    ///     Adds <paramref name="amount"/> to the corporate budget on every
    ///     CorporateConsoleComponent so all terminals stay in sync.
    /// </summary>
    public void AddToCorporateBudget(float amount)
    {
        var q = EntityQueryEnumerator<CorporateConsoleComponent>();
        while (q.MoveNext(out _, out var comp))
            comp.CorporateBudget += amount;

        UpdateAllUi();
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void OnUiOpened(EntityUid uid, CorporateConsoleComponent comp, BoundUIOpenedEvent args)
    {
        UpdateUiState(uid, comp);
    }

    private void OnOpenThirdParty(EntityUid uid, CorporateConsoleComponent comp, CorporateConsoleOpenThirdPartyBuiMsg msg)
    {
        _ui.TryToggleUi(uid, CorporateConsoleThirdPartyUi.Key, msg.Actor);
    }

    private void OnSetTariff(EntityUid uid, CorporateConsoleComponent comp, CorporateConsoleSetTariffBuiMsg msg)
    {
        var clamped = Math.Clamp(msg.TariffPercent, 0f, 50f);
        var oldTariff = comp.TransitTariffPercent;

        // Sync tariff to ALL corporate consoles
        var query = EntityQueryEnumerator<CorporateConsoleComponent>();
        while (query.MoveNext(out _, out var c))
            c.TransitTariffPercent = clamped;

        if (Math.Abs(oldTariff - clamped) > 0.01f)
        {
            var sound = new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Announcements/announce.ogg");
            _chat.DispatchGlobalAnnouncement(
                $"Corporate transit tariff has been set to {clamped:F0}%. Submission payouts to the colony have been adjusted.",
                "Corporate Affairs",
                playSound: true,
                announcementSound: sound);
        }

        UpdateAllUi();
    }

    private void OnCallThirdParty(EntityUid uid, CorporateConsoleComponent comp, CorporateConsoleCallThirdPartyBuiMsg msg)
    {
        if (!comp.CallableParties.TryGetValue(msg.ThirdPartyId, out var cost))
            return;
        if (comp.CalledParties.Contains(msg.ThirdPartyId))
            return;
        if (comp.CorporateBudget < cost)
            return;
        if (!_proto.TryIndex<AuThirdPartyPrototype>(msg.ThirdPartyId, out var partyProto))
            return;
        if (!_proto.TryIndex(partyProto.PartySpawn, out var spawnProto))
            return;

        // Deduct from ALL corporate consoles (they share one budget)
        var q = EntityQueryEnumerator<CorporateConsoleComponent>();
        while (q.MoveNext(out _, out var c))
        {
            c.CorporateBudget -= cost;
            c.CalledParties.Add(msg.ThirdPartyId);
        }

        _thirdParty.SpawnThirdParty(partyProto, spawnProto, false);
        UpdateAllUi();
    }

    private void OnWithdraw(EntityUid uid, CorporateConsoleComponent comp, CorporateConsoleWithdrawBuiMsg msg)
    {
        if (msg.Amount <= 0 || msg.Amount > comp.CorporateBudget)
            return;

        // Deduct from ALL corporate consoles
        var q = EntityQueryEnumerator<CorporateConsoleComponent>();
        while (q.MoveNext(out _, out var c))
            c.CorporateBudget -= msg.Amount;

        // Apply income tax — tax goes to colony budget, remainder dispensed as cash
        var incomeTax = _adminConsole.GetIncomeTax();
        var taxAmount = (int) Math.Floor(msg.Amount * incomeTax);
        var netAmount = (int) msg.Amount - taxAmount;

        if (netAmount > 0)
            _stack.SpawnMultiple("RMCSpaceCash", netAmount, uid);
        if (taxAmount > 0)
            _colonyBudget.AddToBudget(taxAmount);

        UpdateAllUi();
    }

    private void OnCashInserted(EntityUid uid, CorporateConsoleComponent comp, EntInsertedIntoContainerMessage args)
    {
        var stackCount = 1;
        if (TryComp<StackComponent>(args.Entity, out var stack))
            stackCount = stack.Count;

        // Add to ALL corporate consoles (shared budget)
        var q = EntityQueryEnumerator<CorporateConsoleComponent>();
        while (q.MoveNext(out _, out var c))
            c.CorporateBudget += stackCount;

        EntityManager.QueueDeleteEntity(args.Entity);
        UpdateAllUi();
    }

    /// <summary>
    ///     Fallback: if ItemSlots didn't catch the cash, handle it directly.
    /// </summary>
    private void OnInteractUsing(EntityUid uid, CorporateConsoleComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!_tag.HasTag(args.Used, "Currency"))
            return;

        args.Handled = true;

        var stackCount = 1;
        if (TryComp<StackComponent>(args.Used, out var stack))
            stackCount = stack.Count;

        var q = EntityQueryEnumerator<CorporateConsoleComponent>();
        while (q.MoveNext(out _, out var c))
            c.CorporateBudget += stackCount;

        EntityManager.QueueDeleteEntity(args.Used);
        UpdateAllUi();
    }

    // ── UI helpers ───────────────────────────────────────────────────────────

    private void UpdateUiState(EntityUid uid, CorporateConsoleComponent comp)
    {
        var econ = _adminConsole.BuildEconomyStatus();
        _ui.SetUiState(uid, CorporateConsoleUi.Key,
            new CorporateConsoleBuiState(comp.TransitTariffPercent, comp.CorporateBudget, econ));

        var thirdParties = new Dictionary<string, (string DisplayName, float Cost)>();
        foreach (var (id, cost) in comp.CallableParties)
        {
            if (_proto.TryIndex<AuThirdPartyPrototype>(id, out var proto))
                thirdParties[id] = (proto.DisplayName ?? proto.ID, cost);
        }
        _ui.SetUiState(uid, CorporateConsoleThirdPartyUi.Key,
            new CorporateConsoleThirdPartyBuiState(comp.CorporateBudget, thirdParties, comp.CalledParties));
    }

    private void UpdateAllUi()
    {
        var q = EntityQueryEnumerator<CorporateConsoleComponent>();
        while (q.MoveNext(out var uid, out var comp))
            UpdateUiState(uid, comp);
    }
}

