using System.Linq;
using Content.Server.Stack;
using Content.Shared.Access.Systems;
using Content.Shared.AU14.ColonyEconomy;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.ColonyEconomy;

public sealed partial class AU14CashVendorSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private ColonyBudgetSystem _colonyBudget = default!;
    [Dependency] private AdminConsoleSystem _adminConsole = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AU14CashVendorComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<AU14CashVendorComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<AU14CashVendorComponent, EntInsertedIntoContainerMessage>(OnCashInserted);
        SubscribeLocalEvent<AU14CashVendorComponent, AU14CashVendorBuyBuiMsg>(OnBuy);
        SubscribeLocalEvent<AU14CashVendorComponent, AU14CashVendorReturnChangeBuiMsg>(OnReturnChange);
        SubscribeLocalEvent<AU14CashVendorComponent, AU14CashVendorScanIDBuiMsg>(OnScanId);
    }

    private void OnUiOpened(EntityUid uid, AU14CashVendorComponent comp, BoundUIOpenedEvent args)
    {
        comp.ScannedDepartmentConsole = null;
        UpdateUi(uid, comp);
    }

    private void OnUiClosed(EntityUid uid, AU14CashVendorComponent comp, BoundUIClosedEvent args)
    {
        comp.ScannedDepartmentConsole = null;
    }

    private void OnCashInserted(EntityUid uid, AU14CashVendorComponent comp, EntInsertedIntoContainerMessage args)
    {
        int count = 1;
        if (TryComp<StackComponent>(args.Entity, out var stack))
            count = stack.Count;

        comp.InsertedCash += count;
        QueueDel(args.Entity);
        UpdateUi(uid, comp);
    }

    private void OnScanId(EntityUid uid, AU14CashVendorComponent comp, AU14CashVendorScanIDBuiMsg msg)
    {
        // Toggle: if already linked, clear it
        if (comp.ScannedDepartmentConsole != null)
        {
            comp.ScannedDepartmentConsole = null;
            UpdateUi(uid, comp);
            return;
        }

        if (!comp.AllowDepartmentBudget)
            return;

        var mob = msg.Actor;

        if (!_idCard.TryFindIdCard(mob, out var idCard))
        {
            _popup.PopupCursor("No ID card found.", mob, PopupType.SmallCaution);
            return;
        }

        var query = EntityQueryEnumerator<DepartmentConsoleComponent>();
        while (query.MoveNext(out var consoleUid, out var console))
        {
            if (!console.Members.Contains(idCard.Owner))
                continue;

            comp.ScannedDepartmentConsole = consoleUid;
            UpdateUi(uid, comp);
            return;
        }

        _popup.PopupCursor("No department found for this ID card.", mob, PopupType.SmallCaution);
    }

    private void OnBuy(EntityUid uid, AU14CashVendorComponent comp, AU14CashVendorBuyBuiMsg msg)
    {
        if (msg.ItemIndex < 0 || msg.ItemIndex >= comp.Items.Count)
            return;

        var item = comp.Items[msg.ItemIndex];
        var tax = _adminConsole.GetSalesTax();
        var effectivePrice = (int) Math.Ceiling(item.BasePrice * (1f + tax));
        var taxRevenue = effectivePrice - item.BasePrice;

        if (comp.ScannedDepartmentConsole is { } consoleUid &&
            TryComp(consoleUid, out DepartmentConsoleComponent? deptConsole))
        {
            if (deptConsole.DepartmentBudget < effectivePrice)
                return;
            deptConsole.DepartmentBudget -= effectivePrice;
        }
        else
        {
            if (comp.InsertedCash < effectivePrice)
                return;
            comp.InsertedCash -= effectivePrice;
        }

        if (taxRevenue > 0)
            _colonyBudget.AddToBudget(taxRevenue);

        if (comp.ScannedDepartmentConsole == null && comp.PercentToColony > 0)
            _colonyBudget.AddToBudget(item.BasePrice * comp.PercentToColony);

        Spawn(item.ItemId, Transform(uid).Coordinates);
        UpdateUi(uid, comp);
    }

    private void OnReturnChange(EntityUid uid, AU14CashVendorComponent comp, AU14CashVendorReturnChangeBuiMsg msg)
    {
        if (comp.InsertedCash <= 0)
            return;

        _stack.SpawnMultiple("RMCSpaceCash", (int) comp.InsertedCash, uid);
        comp.InsertedCash = 0;
        UpdateUi(uid, comp);
    }

    private void UpdateUi(EntityUid uid, AU14CashVendorComponent comp)
    {
        var tax = _adminConsole.GetSalesTax();
        var items = comp.Items.Select((entry, idx) =>
        {
            var name = entry.Name
                       ?? (_proto.TryIndex<EntityPrototype>(entry.ItemId, out var ep) ? ep.Name : entry.ItemId.Id);
            var effectivePrice = (int) Math.Ceiling(entry.BasePrice * (1f + tax));
            return new AU14CashVendorItemState(idx, name, effectivePrice, entry.ItemId);
        }).ToList();

        bool hasDeptMode = false;
        float deptBudget = 0f;
        var deptName = string.Empty;

        if (comp.ScannedDepartmentConsole is { } consoleUid &&
            TryComp(consoleUid, out DepartmentConsoleComponent? deptConsole))
        {
            hasDeptMode = true;
            deptBudget = deptConsole.DepartmentBudget;
            deptName = deptConsole.DepartmentName;
        }

        _ui.SetUiState(uid, AU14CashVendorUi.Key, new AU14CashVendorBuiState(
            comp.InsertedCash, items, tax * 100f, comp.AllowDepartmentBudget, hasDeptMode, deptBudget, deptName));
    }
}

