using System.Linq;
using Content.Server.Stack;
using Content.Shared.Access.Systems;
using Content.Shared.AU14.ColonyEconomy;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
namespace Content.Server.AU14.ColonyEconomy;
public sealed class AU14ShopkeeperVendorSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly AdminConsoleSystem _adminConsole = default!;
    [Dependency] private readonly ColonyBudgetSystem _colonyBudget = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AU14ShopkeeperVendorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AU14ShopkeeperVendorComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<AU14ShopkeeperVendorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<AU14ShopkeeperVendorComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
        SubscribeLocalEvent<AU14ShopkeeperVendorComponent, AU14ShopkeeperBuyBuiMsg>(OnBuy);
        SubscribeLocalEvent<AU14ShopkeeperVendorComponent, AU14ShopkeeperReturnChangeBuiMsg>(OnReturnChange);
        SubscribeLocalEvent<AU14ShopkeeperVendorComponent, AU14ShopkeeperEditListingBuiMsg>(OnEditListing);
        SubscribeLocalEvent<AU14ShopkeeperVendorComponent, AU14ShopkeeperRemoveListingBuiMsg>(OnRemoveListing);
    }
    private void OnMapInit(EntityUid uid, AU14ShopkeeperVendorComponent comp, MapInitEvent args)
    {
        _containers.EnsureContainer<Container>(uid, AU14ShopkeeperVendorComponent.StockContainerName);
    }
    private void OnUiOpened(EntityUid uid, AU14ShopkeeperVendorComponent comp, BoundUIOpenedEvent args)
    {
        UpdateShopUi(uid, comp);
    }
    /// <summary>
    ///     When an authorized user uses a non-currency item on this machine,
    ///     take the item from their hand and add it to the stock with a default listing.
    /// </summary>
    private void OnInteractUsing(EntityUid uid, AU14ShopkeeperVendorComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;
        // Currency items go to the cash slot via ItemSlots - let them pass
        if (_tag.HasTag(args.Used, "Currency"))
            return;
        if (!_accessReader.IsAllowed(args.User, uid))
            return;
        if (!_containers.TryGetContainer(uid, AU14ShopkeeperVendorComponent.StockContainerName, out var stockContainer))
            return;
        args.Handled = true;
        if (!_hands.TryDrop(args.User, args.Used, checkActionBlocker: false))
            return;
        if (!_containers.Insert(args.Used, stockContainer))
            _hands.TryPickupAnyHand(args.User, args.Used);
    }
    private void OnItemInserted(EntityUid uid, AU14ShopkeeperVendorComponent comp, EntInsertedIntoContainerMessage args)
    {
        // Cash slot - add to customer's inserted cash total
        if (args.Container.ID == AU14ShopkeeperVendorComponent.CashSlotName)
        {
            var count = TryComp<StackComponent>(args.Entity, out var stack) ? stack.Count : 1;
            comp.InsertedCash += count;
            EntityManager.QueueDeleteEntity(args.Entity);
            UpdateShopUi(uid, comp);
            return;
        }
        // Stock container - create a new listing with the item's prototype name and a default price
        if (args.Container.ID == AU14ShopkeeperVendorComponent.StockContainerName)
        {
            var meta = MetaData(args.Entity);
            var displayName = meta.EntityName;
            var protoId = meta.EntityPrototype?.ID;
            comp.Listings.Add(new AU14ShopkeeperListing
            {
                ItemNet = GetNetEntity(args.Entity),
                DisplayName = displayName,
                Price = 10,
                ProtoId = protoId,
            });
            UpdateShopUi(uid, comp);
        }
    }
    private void OnBuy(EntityUid uid, AU14ShopkeeperVendorComponent comp, AU14ShopkeeperBuyBuiMsg msg)
    {
        if (msg.Index < 0 || msg.Index >= comp.Listings.Count)
            return;
        var listing = comp.Listings[msg.Index];
        var tax = _adminConsole.GetSalesTax();
        var effectivePrice = (int) Math.Ceiling(listing.Price * (1f + tax));
        if (comp.InsertedCash < effectivePrice)
            return;
        var itemEntity = GetEntity(listing.ItemNet);
        if (!EntityManager.EntityExists(itemEntity))
        {
            comp.Listings.RemoveAt(msg.Index);
            UpdateShopUi(uid, comp);
            return;
        }
        comp.InsertedCash -= effectivePrice;
        // Tax delta goes to the colony budget
        var taxRevenue = effectivePrice - listing.Price;
        if (taxRevenue > 0)
            _colonyBudget.AddToBudget(taxRevenue);
        // Remove from stock container and place at vendor's location
        if (_containers.TryGetContainer(uid, AU14ShopkeeperVendorComponent.StockContainerName, out var container))
            _containers.Remove(itemEntity, container);
        Transform(itemEntity).Coordinates = Transform(uid).Coordinates;
        comp.Listings.RemoveAt(msg.Index);
        UpdateShopUi(uid, comp);
    }
    private void OnReturnChange(EntityUid uid, AU14ShopkeeperVendorComponent comp, AU14ShopkeeperReturnChangeBuiMsg msg)
    {
        if (comp.InsertedCash <= 0)
            return;
        _stack.SpawnMultiple("RMCSpaceCash", (int) comp.InsertedCash, uid);
        comp.InsertedCash = 0;
        UpdateShopUi(uid, comp);
    }
    private void OnEditListing(EntityUid uid, AU14ShopkeeperVendorComponent comp, AU14ShopkeeperEditListingBuiMsg msg)
    {
        // Server-side access check: only shopkeepers can edit
        if (msg.Actor is not { Valid: true } actor || !_accessReader.IsAllowed(actor, uid))
            return;
        if (msg.Index < 0 || msg.Index >= comp.Listings.Count)
            return;
        comp.Listings[msg.Index].DisplayName = msg.DisplayName.Trim();
        comp.Listings[msg.Index].Price = Math.Max(1, msg.Price);
        UpdateShopUi(uid, comp);
    }
    private void OnRemoveListing(EntityUid uid, AU14ShopkeeperVendorComponent comp, AU14ShopkeeperRemoveListingBuiMsg msg)
    {
        // Server-side access check: only shopkeepers can remove
        if (msg.Actor is not { Valid: true } actor || !_accessReader.IsAllowed(actor, uid))
            return;
        if (msg.Index < 0 || msg.Index >= comp.Listings.Count)
            return;
        var listing = comp.Listings[msg.Index];
        var itemEntity = GetEntity(listing.ItemNet);
        if (EntityManager.EntityExists(itemEntity))
        {
            if (_containers.TryGetContainer(uid, AU14ShopkeeperVendorComponent.StockContainerName, out var container))
                _containers.Remove(itemEntity, container);
            Transform(itemEntity).Coordinates = Transform(uid).Coordinates;
        }
        comp.Listings.RemoveAt(msg.Index);
        UpdateShopUi(uid, comp);
    }
    // -- UI helpers -----------------------------------------------------------
    private void UpdateShopUi(EntityUid uid, AU14ShopkeeperVendorComponent comp)
    {
        var tax = _adminConsole.GetSalesTax();

        // Group listings by (ProtoId, DisplayName, Price) for stacking in buyer view
        var grouped = new Dictionary<(string?, string, int), (int FirstIndex, int Count, string? ProtoId)>();
        for (var i = 0; i < comp.Listings.Count; i++)
        {
            var l = comp.Listings[i];
            var key = (l.ProtoId, l.DisplayName, l.Price);
            if (grouped.TryGetValue(key, out var existing))
                grouped[key] = (existing.FirstIndex, existing.Count + 1, l.ProtoId);
            else
                grouped[key] = (i, 1, l.ProtoId);
        }

        var items = grouped.Select(kvp =>
        {
            var (protoId, displayName, price) = kvp.Key;
            var (firstIndex, count, proto) = kvp.Value;
            var effectivePrice = (int) Math.Ceiling(price * (1f + tax));
            return new AU14ShopkeeperListingState(firstIndex, displayName, effectivePrice, price, count, proto);
        }).ToList();

        _ui.SetUiState(uid, AU14ShopkeeperVendorUi.Shop,
            new AU14ShopkeeperVendorShopState(comp.InsertedCash, items, tax * 100f));
    }
}
