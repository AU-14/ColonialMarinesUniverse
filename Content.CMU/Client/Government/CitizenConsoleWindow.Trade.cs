// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using Content.Client.CMU14.WorldMarket;
using Content.Shared.CMU14.Government;
using Robust.Client.UserInterface;
using static Content.Client.CMU14.WorldMarket.TermKit;

namespace Content.Client.CMU14.Government;

// The tab of the selling in the console of the people: a thing held in the hand is offered to a person who stands by the console, or put up for auction. (The third way, the market stall, has a window of its own.)
public sealed partial class CitizenConsoleWindow
{
    private NetEntity? _tradeBuyer;
    private string _tradePrice = string.Empty;
    private string _auctionStart = string.Empty;
    private int _auctionMinutes = 5;
    private readonly Dictionary<int, string> _itemBidDrafts = new();

    private void BuildTrade(CitizenConsoleState state)
    {
        var trade = state.Trade;
        var (me, myName) = Me();

        var head = Column(6);
        head.AddChild(Gold(Loc.GetString("rumc-trade-title")));
        head.AddChild(Wrapped(Loc.GetString("rumc-trade-hint", ("tax", trade.Tax)), TermPalette.Muted));
        _page.AddChild(Card(head));

        // From hand to hand.
        var hand = Column(6);
        hand.AddChild(Gold(Loc.GetString("rumc-trade-hand")));
        hand.AddChild(Wrapped(Loc.GetString("rumc-trade-hand-hint"), TermPalette.Muted));
        var others = state.Nearby.Where(n => n.Entity != me).ToList();
        if (others.Count == 0)
            hand.AddChild(Muted(Loc.GetString("rumc-citizen-notary-nobody")));

        foreach (var person in others)
        {
            var entity = person.Entity;
            var button = new TermButton { Text = person.Name, MinWidth = 220, Pressed = _tradeBuyer == entity };
            button.OnPressed += _ =>
            {
                _tradeBuyer = _tradeBuyer == entity ? null : entity;
                Rebuild();
            };
            hand.AddChild(button);
        }

        hand.AddChild(Edit(_tradePrice, Loc.GetString("rumc-trade-price-placeholder"), 6, text => _tradePrice = new string(text.Where(char.IsDigit).ToArray())));
        hand.AddChild(Action(Loc.GetString("rumc-trade-offer"), () =>
        {
            if (_tradeBuyer is { } buyer)
                OnOfferSale?.Invoke(buyer, Parse(_tradePrice, 0));
        }, _tradeBuyer != null && others.Any(o => o.Entity == _tradeBuyer), 260));
        _page.AddChild(Card(hand));

        // The offers: to me, and mine.
        var offers = Column(6);
        offers.AddChild(Gold(Loc.GetString("rumc-trade-offers")));
        var mine = trade.Offers.Where(o => o.Buyer == me || o.Seller == me).ToList();
        if (mine.Count == 0)
            offers.AddChild(Muted(Loc.GetString("rumc-trade-offers-none")));

        foreach (var offer in mine)
        {
            var id = offer.Id;
            offers.AddChild(Rule());
            offers.AddChild(Text(Loc.GetString("rumc-trade-offer-line", ("seller", offer.SellerName), ("buyer", offer.BuyerName), ("item", offer.Item), ("sum", Money(offer.Price))), TermPalette.Text));

            var buttons = Row(8);
            if (offer.Buyer == me)
                buttons.AddChild(Action(Loc.GetString("rumc-trade-accept", ("sum", Money(offer.Price))), () => OnAcceptSale?.Invoke(id), true, 220));

            buttons.AddChild(Danger(Loc.GetString(offer.Buyer == me ? "rumc-citizen-notary-decline" : "rumc-citizen-notary-withdraw"), () => OnDeclineSale?.Invoke(id), true, 150));
            offers.AddChild(buttons);
        }

        _page.AddChild(Card(offers));

        // The auctions of things.
        var auctions = Column(6);
        auctions.AddChild(Gold(Loc.GetString("rumc-trade-auctions")));
        auctions.AddChild(Wrapped(Loc.GetString("rumc-trade-auction-hint"), TermPalette.Muted));
        auctions.AddChild(Edit(_auctionStart, Loc.GetString("rumc-trade-auction-start-placeholder"), 6, text => _auctionStart = new string(text.Where(char.IsDigit).ToArray())));

        var times = Row(6);
        foreach (var minutes in new[] { 3, 5, 10 })
        {
            var picked = minutes;
            var button = new TermButton { Text = Loc.GetString("rumc-poll-minutes", ("value", minutes)), MinWidth = 90, Pressed = _auctionMinutes == minutes };
            button.OnPressed += _ =>
            {
                _auctionMinutes = picked;
                Rebuild();
            };
            times.AddChild(button);
        }

        auctions.AddChild(times);
        auctions.AddChild(Action(Loc.GetString("rumc-trade-auction-start"), () => OnStartItemAuction?.Invoke(Parse(_auctionStart, 0), _auctionMinutes), true, 280));

        if (trade.Auctions.Count == 0)
            auctions.AddChild(Muted(Loc.GetString("rumc-trade-auctions-none")));

        foreach (var auction in trade.Auctions)
        {
            var id = auction.Id;
            auctions.AddChild(Rule());
            auctions.AddChild(Text(Loc.GetString("rumc-trade-auction-line", ("item", auction.Item), ("seller", auction.Seller)), TermPalette.Text));
            auctions.AddChild(Live(s =>
            {
                var a = s.Trade.Auctions.FirstOrDefault(x => x.Id == id);
                return a == null ? string.Empty : a.Current == 0
                    ? Loc.GetString("rumc-auction-no-bids", ("start", Money(a.StartPrice)), ("time", Time(a.LeftSeconds)))
                    : Loc.GetString("rumc-auction-best", ("sum", Money(a.Current)), ("leader", a.Leader), ("time", Time(a.LeftSeconds)));
            }, TermPalette.Gold));

            if (auction.Seller == myName)
            {
                if (auction.Current == 0)
                    auctions.AddChild(Danger(Loc.GetString("rumc-trade-auction-cancel"), () => OnCancelItemAuction?.Invoke(id), true, 220));

                continue;
            }

            var minBid = auction.MinBid;
            var row = Row(8);
            row.AddChild(Edit(_itemBidDrafts.GetValueOrDefault(id, minBid.ToString()), "0", 6, text => _itemBidDrafts[id] = new string(text.Where(char.IsDigit).ToArray())));
            row.AddChild(Action(Loc.GetString("rumc-auction-bid"), () => OnBidItemAuction?.Invoke(id, Parse(_itemBidDrafts.GetValueOrDefault(id, minBid.ToString()), minBid)), true, 150));
            auctions.AddChild(row);
        }

        _page.AddChild(Card(auctions));

        var stall = Column(4);
        stall.AddChild(Gold(Loc.GetString("rumc-trade-stall")));
        stall.AddChild(Wrapped(Loc.GetString("rumc-trade-stall-hint"), TermPalette.Muted));
        _page.AddChild(Card(stall));
    }
}
