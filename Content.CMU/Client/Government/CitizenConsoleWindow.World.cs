// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using Content.Client.CMU14.WorldMarket;
using Content.Shared.CMU14.Government;
using static Content.Client.CMU14.WorldMarket.TermKit;

namespace Content.Client.CMU14.Government;

// The tab of the world trade in the console of the people: a player orders goods of the cities with his own money. They come on the next trip of the trade shuttle, which the heads call, and he takes them off it when
// it is on the landing zone. He may also put his goods on the shuttle to sell (at the console of the shuttle); 30% of the pay for them is his.
public sealed partial class CitizenConsoleWindow
{
    private int _worldLots = 1;

    private void BuildWorld(CitizenConsoleState state)
    {
        var world = state.World;
        var (_, myName) = Me();

        var head = Column(6);
        head.AddChild(Gold(Loc.GetString("rumc-world-title")));
        head.AddChild(Wrapped(Loc.GetString("rumc-world-hint"), TermPalette.Muted));
        head.AddChild(Text(Loc.GetString("rumc-foreman-phase-" + world.Phase.ToString().ToLowerInvariant()), world.Phase == ShuttlePhase.Landed ? TermPalette.Good : TermPalette.Gold));
        if (world.Phase is ShuttlePhase.Landed or ShuttlePhase.Away)
            head.AddChild(Live(s => Loc.GetString("rumc-foreman-phase-left", ("time", Time(s.World.PhaseSeconds))), TermPalette.Muted));

        if (world.City.Length > 0)
            head.AddChild(Muted(Loc.GetString("rumc-world-city", ("city", world.City))));

        _page.AddChild(Card(head));

        var free = world.Phase is ShuttlePhase.Idle or ShuttlePhase.Away;

        var lots = Row(6);
        foreach (var count in new[] { 1, 3, 5, 10 })
        {
            var picked = count;
            var button = new TermButton { Text = Loc.GetString("rumc-foreman-lots", ("value", count)), MinWidth = 90, Pressed = _worldLots == count };
            button.OnPressed += _ =>
            {
                _worldLots = picked;
                Rebuild();
            };
            lots.AddChild(button);
        }

        var goods = Column(6);
        goods.AddChild(Gold(Loc.GetString("rumc-world-goods")));
        goods.AddChild(lots);
        foreach (var good in world.Goods)
        {
            var id = good.Id;
            var cost = good.BuyPrice * good.Lot * _worldLots;
            goods.AddChild(Rule());
            goods.AddChild(Text(good.Name, TermPalette.Text));
            goods.AddChild(Action(Loc.GetString("rumc-world-buy", ("amount", good.Lot * _worldLots), ("price", Money(good.BuyPrice)), ("cost", Money(cost))),
                () => OnOrderWorld?.Invoke(id, _worldLots), free, 420));
        }

        _page.AddChild(Card(goods));

        var mine = Column(4);
        mine.AddChild(Gold(Loc.GetString("rumc-world-orders")));
        var yours = world.Orders.Where(o => o.Payer == myName).ToList();
        if (yours.Count == 0)
            mine.AddChild(Muted(Loc.GetString("rumc-world-orders-none")));

        foreach (var order in yours)
        {
            var id = order.Id;
            mine.AddChild(Rule());
            mine.AddChild(Text(Loc.GetString("rumc-world-order-line", ("good", order.Good), ("amount", order.Amount), ("cost", Money(order.Cost))), TermPalette.Text));
            mine.AddChild(Danger(Loc.GetString("rumc-foreman-cart-cancel"), () => OnCancelWorld?.Invoke(id), free, 190));
        }

        _page.AddChild(Card(mine));
    }
}
