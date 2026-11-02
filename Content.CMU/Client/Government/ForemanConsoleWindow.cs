// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.WorldMarket;
using Content.Shared.CMU14.Government;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using static Content.Client.CMU14.WorldMarket.TermKit;

namespace Content.Client.CMU14.Government;

// The window of the console of the foreman, for the heads: the trade shuttle. Where it is and the buttons to call it and to send it away, the landing zones and the cities of the map to pick, the goods that can be
// bought with the money of the colony and what the city pays for what the colony sells, the orders that wait for the next trip, the orders of the cities, and what is on the shuttle to be sold.
public sealed class ForemanConsoleWindow : BaseWindow
{
    private const float WindowWidth = 760f;

    private readonly BoxContainer _page = Column(6);
    private readonly ScrollContainer _scroll;
    private readonly List<Action<ForemanState>> _live = new();

    private ForemanState? _state;
    private string _signature = string.Empty;
    private int _lots = 1;

    public event Action<NetEntity>? OnSelectZone;
    public event Action<string>? OnSelectCity;
    public event Action<string, int>? OnOrder;
    public event Action<int>? OnCancelOrder;
    public event Action? OnCall;
    public event Action? OnDepart;

    public ForemanConsoleWindow()
    {
        Resizable = false;
        MinSize = new Vector2(WindowWidth, 0);
        MouseFilter = MouseFilterMode.Stop;

        var close = new TermButton { Text = "✕", MinWidth = 28 };
        close.OnPressed += _ => Close();

        var header = Row(10);
        header.AddChild(Gold(Loc.GetString("rumc-foreman-title"), expand: true));
        header.AddChild(close);

        _scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, MinSize = new Vector2(0, 620), HorizontalExpand = true };
        _scroll.AddChild(_page);

        var root = Column(6);
        root.Margin = new Thickness(8, 6, 8, 8);
        root.AddChild(header);
        root.AddChild(Muted(Loc.GetString("rumc-foreman-subtitle")));
        root.AddChild(Rule());
        root.AddChild(_scroll);

        AddChild(new TermPanel(scanlines: true) { MouseFilter = MouseFilterMode.Pass, Children = { root } });
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos) => relativeMousePos.Y < 34 ? DragMode.Move : DragMode.None;

    private static Control Wrapped(string text, Color color)
    {
        var label = new RichTextLabel { MaxWidth = WindowWidth - 70f, HorizontalExpand = true };
        label.SetMessage(FormattedMessage.FromUnformatted(text), null, color);
        return label;
    }

    private Label Live(Func<ForemanState, string> text, Color? color = null)
    {
        var label = Text(_state == null ? string.Empty : text(_state), color);
        _live.Add(state => label.Text = text(state));
        return label;
    }

    private void Rebuild()
    {
        _signature = string.Empty;
        if (_state != null)
            SetState(_state);
    }

    public void SetState(ForemanState state)
    {
        _state = state;
        var signature = Signature(state);
        if (signature == _signature)
        {
            foreach (var update in _live)
                update(state);

            return;
        }

        _signature = signature;
        var scroll = _scroll.GetScrollValue();
        _page.RemoveAllChildren();
        _live.Clear();
        Build(state);
        _scroll.SetScrollValue(scroll);
    }

    private string Signature(ForemanState state)
    {
        return state.Phase + "|" + state.Zone + "|" + state.City + "|" + state.Failure + "|" + _lots
               + "|" + string.Join(";", state.Zones.Select(z => $"{z.Zone}:{z.Occupied}"))
               + "|" + string.Join(";", state.Cities.Select(c => c.Id))
               + "|" + string.Join(";", state.Goods.Select(g => $"{g.Id}:{g.BuyPrice}:{g.SellPrice}:{state.Budget >= g.BuyPrice * g.Lot}"))
               + "|" + string.Join(";", state.Cart.Select(c => c.Id))
               + "|" + string.Join(";", state.WorldOrders.Select(o => $"{o.Id}:{o.Filled}"))
               + "|" + string.Join(";", state.Shipment.Select(s => $"{s.Good}:{s.Owner}:{s.Amount}"));
    }

    private void Build(ForemanState state)
    {
        // The shuttle: where it is, and what the heads can do.
        var shuttle = Column(6);
        shuttle.AddChild(Gold(Loc.GetString("rumc-foreman-shuttle")));
        shuttle.AddChild(Text(Loc.GetString("rumc-foreman-phase-" + state.Phase.ToString().ToLowerInvariant()), PhaseColor(state.Phase)));
        if (state.Phase is ShuttlePhase.Landed or ShuttlePhase.Away)
            shuttle.AddChild(Live(s => Loc.GetString("rumc-foreman-phase-left", ("time", Time(s.PhaseSeconds))), TermPalette.Muted));

        shuttle.AddChild(Muted(Loc.GetString("rumc-foreman-picked", ("zone", state.Zone.Length > 0 ? state.Zone : "—"), ("city", state.City.Length > 0 ? state.City : "—"))));
        if (state.Failure.Length > 0)
            shuttle.AddChild(Text(state.Failure, TermPalette.Bad));

        var buttons = Row(8);
        buttons.AddChild(Action(Loc.GetString("rumc-foreman-call"), () => OnCall?.Invoke(), state.Phase == ShuttlePhase.Idle && state.Zone.Length > 0 && state.City.Length > 0, 260));
        buttons.AddChild(Danger(Loc.GetString("rumc-foreman-depart"), () => OnDepart?.Invoke(), state.Phase == ShuttlePhase.Landed, 260));
        shuttle.AddChild(buttons);
        shuttle.AddChild(Wrapped(Loc.GetString("rumc-foreman-hint"), TermPalette.Muted));
        _page.AddChild(Card(shuttle));

        var free = state.Phase is ShuttlePhase.Idle or ShuttlePhase.Away;

        // The landing zones.
        var zones = Column(4);
        zones.AddChild(Gold(Loc.GetString("rumc-foreman-zones")));
        zones.AddChild(Wrapped(Loc.GetString("rumc-foreman-zones-hint"), TermPalette.Muted));
        if (state.Zones.Count == 0)
            zones.AddChild(Muted(Loc.GetString("rumc-foreman-zones-none")));

        foreach (var zone in state.Zones)
        {
            var id = zone.Zone;
            var button = new TermButton
            {
                Text = zone.Occupied ? Loc.GetString("rumc-foreman-zone-busy", ("name", zone.Name)) : zone.Name,
                MinWidth = 300,
                Pressed = state.Zone == zone.Name && !zone.Occupied,
                Disabled = zone.Occupied || !free,
            };
            button.OnPressed += _ => OnSelectZone?.Invoke(id);
            zones.AddChild(button);
        }

        _page.AddChild(Card(zones));

        // The cities.
        var cities = Column(4);
        cities.AddChild(Gold(Loc.GetString("rumc-foreman-cities")));
        if (state.Cities.Count == 0)
            cities.AddChild(Muted(Loc.GetString("rumc-foreman-cities-none")));

        foreach (var city in state.Cities)
        {
            var id = city.Id;
            var button = new TermButton
            {
                Text = Loc.GetString("rumc-foreman-city-line", ("name", city.Name), ("caps", city.Capabilities)),
                MinWidth = 420,
                Pressed = state.City == city.Name,
                Disabled = !free,
            };
            button.OnPressed += _ => OnSelectCity?.Invoke(id);
            cities.AddChild(button);
            if (city.Crisis.Length > 0)
                cities.AddChild(Text(Loc.GetString("rumc-foreman-city-crisis", ("crisis", city.Crisis)), TermPalette.Bad));
        }

        _page.AddChild(Card(cities));

        // The goods: what the colony pays to buy, and what the city pays for what it sells.
        var goods = Column(6);
        goods.AddChild(Gold(Loc.GetString("rumc-foreman-goods", ("city", state.City.Length > 0 ? state.City : "—"))));
        goods.AddChild(Wrapped(Loc.GetString("rumc-foreman-goods-hint"), TermPalette.Muted));

        var lots = Row(6);
        foreach (var count in new[] { 1, 3, 5, 10 })
        {
            var picked = count;
            var button = new TermButton { Text = Loc.GetString("rumc-foreman-lots", ("value", count)), MinWidth = 90, Pressed = _lots == count };
            button.OnPressed += _ =>
            {
                _lots = picked;
                Rebuild();
            };
            lots.AddChild(button);
        }

        goods.AddChild(lots);
        if (state.City.Length == 0)
            goods.AddChild(Muted(Loc.GetString("rumc-foreman-pick-city")));

        foreach (var good in state.Goods)
        {
            var id = good.Id;
            goods.AddChild(Rule());
            goods.AddChild(Text(good.Name, TermPalette.Text));
            goods.AddChild(Muted(Loc.GetString("rumc-foreman-good-line", ("sell", Money(good.SellPrice)), ("percent", good.Percent), ("lot", good.Lot))));
            if (good.BuyPrice > 0)
            {
                var cost = good.BuyPrice * good.Lot * _lots;
                goods.AddChild(Action(Loc.GetString("rumc-foreman-buy", ("amount", good.Lot * _lots), ("price", Money(good.BuyPrice)), ("cost", Money(cost))),
                    () => OnOrder?.Invoke(id, _lots), free && state.City.Length > 0 && state.Budget >= cost, 420));
            }
            else
            {
                goods.AddChild(Muted(Loc.GetString("rumc-foreman-not-sold")));
            }
        }

        _page.AddChild(Card(goods));

        // The orders that wait for the next trip.
        var cart = Column(4);
        cart.AddChild(Gold(Loc.GetString("rumc-foreman-cart")));
        if (state.Cart.Count == 0)
            cart.AddChild(Muted(Loc.GetString("rumc-foreman-cart-none")));

        foreach (var line in state.Cart)
        {
            var id = line.Id;
            cart.AddChild(Rule());
            cart.AddChild(Text(Loc.GetString("rumc-foreman-cart-line", ("good", line.Good), ("amount", line.Amount), ("payer", line.Payer), ("cost", Money(line.Cost))), TermPalette.Text));
            cart.AddChild(Danger(Loc.GetString("rumc-foreman-cart-cancel"), () => OnCancelOrder?.Invoke(id), free, 190));
        }

        _page.AddChild(Card(cart));

        // The orders of the cities.
        var orders = Column(4);
        orders.AddChild(Gold(Loc.GetString("rumc-foreman-orders")));
        orders.AddChild(Wrapped(Loc.GetString("rumc-foreman-orders-hint"), TermPalette.Muted));
        if (state.WorldOrders.Count == 0)
            orders.AddChild(Muted(Loc.GetString("rumc-foreman-orders-none")));

        foreach (var order in state.WorldOrders)
        {
            var id = order.Id;
            orders.AddChild(Rule());
            orders.AddChild(Text(Loc.GetString("rumc-foreman-order-line", ("city", order.City), ("good", order.Good), ("filled", order.Filled), ("amount", order.Amount),
                ("percent", order.Percent)), TermPalette.Text));
            orders.AddChild(Live(s => Loc.GetString("rumc-foreman-order-left", ("time", Time(s.WorldOrders.FirstOrDefault(o => o.Id == id)?.LeftSeconds ?? 0))), TermPalette.Muted));
        }

        _page.AddChild(Card(orders));

        // What is on the shuttle to be sold.
        var shipment = Column(4);
        shipment.AddChild(Gold(Loc.GetString("rumc-foreman-shipment")));
        shipment.AddChild(Wrapped(Loc.GetString("rumc-foreman-shipment-hint"), TermPalette.Muted));
        if (state.Shipment.Count == 0)
            shipment.AddChild(Muted(Loc.GetString("rumc-foreman-shipment-none")));

        foreach (var line in state.Shipment)
            shipment.AddChild(Text(Loc.GetString("rumc-foreman-shipment-line", ("good", line.Good), ("amount", line.Amount), ("owner", line.Owner)), TermPalette.Text));

        if (state.Shipment.Count > 0)
            shipment.AddChild(Text(Loc.GetString("rumc-foreman-expected", ("sum", Money(state.ExpectedPayout))), TermPalette.Good));

        _page.AddChild(Card(shipment));
    }

    private static Color PhaseColor(ShuttlePhase phase) => phase switch
    {
        ShuttlePhase.Landed => TermPalette.Good,
        ShuttlePhase.Idle => TermPalette.Muted,
        _ => TermPalette.Gold,
    };
}
