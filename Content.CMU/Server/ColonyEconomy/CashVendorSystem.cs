using System.Linq;
using Content.Server.Stack;
using Content.Shared.AU14.ColonyEconomy;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.ColonyEconomy;

public sealed class AU14CashVendorSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ColonyBudgetSystem _colonyBudget = default!;
    [Dependency] private readonly AdminConsoleSystem _adminConsole = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AU14CashVendorComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<AU14CashVendorComponent, EntInsertedIntoContainerMessage>(OnCashInserted);
        SubscribeLocalEvent<AU14CashVendorComponent, AU14CashVendorBuyBuiMsg>(OnBuy);
        SubscribeLocalEvent<AU14CashVendorComponent, AU14CashVendorReturnChangeBuiMsg>(OnReturnChange);
    }

    private void OnUiOpened(EntityUid uid, AU14CashVendorComponent comp, BoundUIOpenedEvent args)
    {
        UpdateUi(uid, comp);
    }

    private void OnCashInserted(EntityUid uid, AU14CashVendorComponent comp, EntInsertedIntoContainerMessage args)
    {
        int count = 1;
        if (TryComp<StackComponent>(args.Entity, out var stack))
            count = stack.Count;

        comp.InsertedCash += count;
        EntityManager.QueueDeleteEntity(args.Entity);
        UpdateUi(uid, comp);
    }

    private void OnBuy(EntityUid uid, AU14CashVendorComponent comp, AU14CashVendorBuyBuiMsg msg)
    {
        if (msg.ItemIndex < 0 || msg.ItemIndex >= comp.Items.Count)
            return;

        var item = comp.Items[msg.ItemIndex];
        var tax = _adminConsole.GetSalesTax();
        var effectivePrice = (int) Math.Ceiling(item.BasePrice * (1f + tax));

        if (comp.InsertedCash < effectivePrice)
            return;

        comp.InsertedCash -= effectivePrice;

        // Tax revenue goes to colony budget
        var taxRevenue = effectivePrice - item.BasePrice;
        if (taxRevenue > 0)
            _colonyBudget.AddToBudget(taxRevenue);

        var coords = Transform(uid).Coordinates;
        EntityManager.SpawnEntity(item.ItemId, coords);
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

        _ui.SetUiState(uid, AU14CashVendorUi.Key, new AU14CashVendorBuiState(comp.InsertedCash, items, tax * 100f));
    }
}

