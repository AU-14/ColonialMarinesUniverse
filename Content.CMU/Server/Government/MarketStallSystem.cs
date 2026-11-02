// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.Government;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server.CMU14.Government;

// The market stall: a table with a place for the goods. The owner (who needs a trade licence) puts a thing on it by clicking the stall with the thing in his hand, and sets a price for it in the window of the stall. Whoever
// comes to the stall sees what is for sale and buys: the price is taken from his account, the owner is paid at once (less the sales tax, which goes to the budget), and the thing is put in the hands of the buyer, all in one
// step. The owner takes back what has not been sold.
public sealed class MarketStallSystem : EntitySystem
{
    [Dependency] private readonly ColonyGovernmentSystem _government = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MarketStallComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MarketStallComponent, InteractUsingEvent>(OnUsing);
        SubscribeLocalEvent<MarketStallComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<MarketStallComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<MarketStallComponent, SetStallPriceMessage>(OnSetPrice);
        SubscribeLocalEvent<MarketStallComponent, TakeStallItemMessage>(OnTake);
        SubscribeLocalEvent<MarketStallComponent, BuyStallItemMessage>(OnBuy);
        SubscribeLocalEvent<MarketStallComponent, ReleaseStallMessage>(OnRelease);
    }

    private void OnInit(Entity<MarketStallComponent> stall, ref ComponentInit args)
    {
        _container.EnsureContainer<Container>(stall, MarketStallComponent.ContainerId);
    }

    private BaseContainer Items(EntityUid stall)
    {
        return _container.EnsureContainer<Container>(stall, MarketStallComponent.ContainerId);
    }

    private StallState Build(Entity<MarketStallComponent> stall)
    {
        var items = Items(stall).ContainedEntities
            .Select(e => new StallItemEntry(GetNetEntity(e), MetaData(e).EntityName, stall.Comp.Prices.GetValueOrDefault(e)))
            .ToList();

        return new StallState(stall.Comp.OwnerName, items, _government.SalesTaxPercent(), stall.Comp.MaxItems);
    }

    private void Refresh(Entity<MarketStallComponent> stall)
    {
        if (_ui.IsUiOpen(stall.Owner, MarketStallUi.Key))
            _ui.SetUiState(stall.Owner, MarketStallUi.Key, Build(stall));
    }

    private void OnOpened(Entity<MarketStallComponent> stall, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is MarketStallUi)
            _ui.SetUiState(stall.Owner, MarketStallUi.Key, Build(stall));
    }

    private void OnExamined(Entity<MarketStallComponent> stall, ref ExaminedEvent args)
    {
        var text = stall.Comp.Owner == null
            ? Loc.GetString("rumc-stall-examine-free")
            : Loc.GetString("rumc-stall-examine-owner", ("owner", stall.Comp.OwnerName), ("count", Items(stall).ContainedEntities.Count));

        args.PushMarkup(text);
    }

    // The owner puts a thing on the stall: he must hold a trade licence, and the stall must be free or his.
    private void OnUsing(Entity<MarketStallComponent> stall, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        var item = args.Used;
        if (HasComp<MobStateComponent>(item))
            return;

        args.Handled = true;

        var name = MetaData(user).EntityName;
        if (stall.Comp.Owner != null && stall.Comp.Owner != user)
        {
            _popup.PopupEntity(Loc.GetString("rumc-stall-not-yours", ("owner", stall.Comp.OwnerName)), stall, user, PopupType.SmallCaution);
            return;
        }

        if (!_government.HasTradeLicense(name))
        {
            _popup.PopupEntity(Loc.GetString("rumc-stall-need-license"), stall, user, PopupType.SmallCaution);
            return;
        }

        var items = Items(stall);
        if (items.ContainedEntities.Count >= stall.Comp.MaxItems)
        {
            _popup.PopupEntity(Loc.GetString("rumc-stall-full"), stall, user, PopupType.SmallCaution);
            return;
        }

        if (!_hands.TryDrop(user, item, checkActionBlocker: false, doDropInteraction: false) || !_container.Insert(item, items))
        {
            _popup.PopupEntity(Loc.GetString("rumc-stall-cannot"), stall, user, PopupType.SmallCaution);
            return;
        }

        stall.Comp.Owner = user;
        stall.Comp.OwnerName = name;
        _popup.PopupEntity(Loc.GetString("rumc-stall-placed", ("item", MetaData(item).EntityName)), stall, user);
        Refresh(stall);
    }

    private void OnSetPrice(Entity<MarketStallComponent> stall, ref SetStallPriceMessage msg)
    {
        var item = GetEntity(msg.Item);
        if (stall.Comp.Owner != msg.Actor || !Items(stall).Contains(item) || msg.Price < 0 || msg.Price > ColonyGovernmentSystem.MaxSalePrice)
            return;

        if (msg.Price == 0)
            stall.Comp.Prices.Remove(item);
        else
            stall.Comp.Prices[item] = msg.Price;

        Refresh(stall);
    }

    // The owner takes a thing back into his hands.
    private void OnTake(Entity<MarketStallComponent> stall, ref TakeStallItemMessage msg)
    {
        var item = GetEntity(msg.Item);
        var items = Items(stall);
        if (stall.Comp.Owner != msg.Actor || !items.Contains(item))
            return;

        if (_container.Remove(item, items, destination: Transform(msg.Actor).Coordinates))
        {
            stall.Comp.Prices.Remove(item);
            _hands.TryPickupAnyHand(msg.Actor, item);
        }

        Refresh(stall);
    }

    // A buyer takes a thing: the money goes from him to the owner (less the sales tax), and the thing into his hands, in one step.
    private void OnBuy(Entity<MarketStallComponent> stall, ref BuyStallItemMessage msg)
    {
        var item = GetEntity(msg.Item);
        var items = Items(stall);
        var buyer = msg.Actor;
        if (stall.Comp.Owner is not { } owner || owner == buyer || !items.Contains(item) || !stall.Comp.Prices.TryGetValue(item, out var price))
            return;

        if (!Exists(owner) || !_government.Settle(owner, buyer, price))
        {
            _popup.PopupEntity(Loc.GetString("rumc-stall-cannot-pay", ("sum", price)), stall, buyer, PopupType.SmallCaution);
            return;
        }

        if (_container.Remove(item, items, destination: Transform(buyer).Coordinates))
            _hands.TryPickupAnyHand(buyer, item);

        stall.Comp.Prices.Remove(item);
        _popup.PopupEntity(Loc.GetString("rumc-stall-bought", ("item", MetaData(item).EntityName), ("sum", price)), stall, buyer);
        _popup.PopupEntity(Loc.GetString("rumc-stall-sold", ("item", MetaData(item).EntityName), ("sum", price)), stall, owner);
        Refresh(stall);
    }

    // The owner lets the stall go when it is empty.
    private void OnRelease(Entity<MarketStallComponent> stall, ref ReleaseStallMessage msg)
    {
        if (stall.Comp.Owner != msg.Actor || Items(stall).ContainedEntities.Count > 0)
            return;

        stall.Comp.Owner = null;
        stall.Comp.OwnerName = string.Empty;
        Refresh(stall);
    }
}
