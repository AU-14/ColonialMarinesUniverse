// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using Content.Client.CMU14.WorldMarket;
using Content.Shared.CMU14.Government;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.CMU14.WorldMarket.TermKit;

namespace Content.Client.CMU14.Government;

// The four last tabs of the window of the mayor: the auctions and the tenders, the loans to the players, the posts, and the decrees. Here too is what the one at the console may see: the tabs that his rights do
// not give are hidden (at the console of the deputy a person has the rights of his post).
public sealed partial class ColonyGovernmentWindow
{
    private static readonly int[] LoanPrincipals = { 500, 1000, 2000, 5000, 10000 };
    private static readonly int[] LoanInstalments = { 3, 5, 8 };
    private static readonly int[] LoanInterests = { 5, 10, 15, 20 };
    private static readonly int[] AuctionMinutes = { 3, 5, 10 };

    private ZoneKind _auctionKind = ZoneKind.Housing;
    private int _auctionPrice = 300;
    private int _auctionTime = 5;
    private string? _tenderKind;
    private int _tenderAmountIndex = 1;
    private int _tenderPrice;
    private int _tenderTime = 5;
    private NetEntity? _loanTarget;
    private int _loanPrincipal = 1000;
    private int _loanInstalments = 5;
    private int _loanInterest = 10;

    private static string TabKey(Tab tab)
    {
        return tab switch
        {
            Tab.Factions => "rumc-government-tab-relations",
            Tab.Cities => "rumc-government-tab-cities",
            Tab.Credit => "rumc-government-tab-credit",
            Tab.Licenses => "rumc-government-tab-licenses",
            Tab.Land => "rumc-government-tab-land",
            Tab.Awards => "rumc-government-tab-awards",
            Tab.Orders => "rumc-government-tab-orders",
            Tab.Appeals => "rumc-government-tab-appeals",
            Tab.Notary => "rumc-government-tab-notary",
            Tab.Auctions => "rumc-government-tab-auctions",
            Tab.Loans => "rumc-government-tab-loans",
            Tab.Subsidies => "rumc-government-tab-subsidies",
            Tab.Projects => "rumc-government-tab-projects",
            _ => "rumc-government-tab-quotas",
        };
    }

    // ---------------------------------------------------------------- the auctions and the tenders

    private static string AuctionsSignature(ColonyGovernmentState state)
    {
        var admin = state.Admin;
        return "u|" + string.Join(";", admin.Auctions.Select(a => a.Id)) + "|" + string.Join(";", admin.Tenders.Select(t => t.Id));
    }

    private void BuildAuctions(ColonyGovernmentState state)
    {
        var admin = state.Admin;

        // A plot to the highest bid.
        var plots = Column(6);
        plots.AddChild(Gold(Loc.GetString("rumc-auction-title")));
        plots.AddChild(Wrapped(Loc.GetString("rumc-auction-hint"), TermPalette.Muted));
        var kinds = Row(6);
        foreach (var kind in state.Land.Kinds)
        {
            var picked = kind.Kind;
            var button = new TermButton { Text = kind.Name, MinWidth = 120, Pressed = _auctionKind == picked };
            button.OnPressed += _ =>
            {
                _auctionKind = picked;
                Rebuild();
            };
            kinds.AddChild(button);
        }

        plots.AddChild(kinds);

        var priceLabel = Text(string.Empty, TermPalette.Text);
        void UpdatePrice() => priceLabel.Text = Loc.GetString("rumc-auction-start-price", ("value", Money(_auctionPrice)));
        var priceRow = Row(6);
        priceRow.AddChild(Action("−", () => { _auctionPrice = Math.Max(50, _auctionPrice - 50); UpdatePrice(); }, true, 36));
        priceRow.AddChild(priceLabel);
        priceRow.AddChild(Action("+", () => { _auctionPrice = Math.Min(20000, _auctionPrice + 50); UpdatePrice(); }, true, 36));
        UpdatePrice();
        plots.AddChild(priceRow);

        plots.AddChild(MinutesRow(AuctionMinutes, _auctionTime, minutes => _auctionTime = minutes));
        plots.AddChild(Action(Loc.GetString("rumc-auction-start"), () => OnStartAuction?.Invoke(_auctionKind, _auctionPrice, _auctionTime), admin.Auctions.Count < 3, 220));
        _page.AddChild(Card(plots));

        var running = Column(6);
        running.AddChild(Gold(Loc.GetString("rumc-auction-running")));
        if (admin.Auctions.Count == 0)
            running.AddChild(Muted(Loc.GetString("rumc-auction-none")));

        foreach (var auction in admin.Auctions)
        {
            var id = auction.Id;
            running.AddChild(Rule());
            running.AddChild(Text(Loc.GetString("rumc-auction-line", ("zone", auction.Zone), ("place", auction.Place)), TermPalette.Text));
            running.AddChild(Live(s =>
            {
                var a = s.Admin.Auctions.FirstOrDefault(x => x.Id == id);
                return a == null ? string.Empty : a.Current == 0
                    ? Loc.GetString("rumc-auction-no-bids", ("start", Money(a.StartPrice)), ("time", Time(a.LeftSeconds)))
                    : Loc.GetString("rumc-auction-best", ("sum", Money(a.Current)), ("leader", a.Leader), ("time", Time(a.LeftSeconds)));
            }, TermPalette.Gold));
            running.AddChild(Danger(Loc.GetString("rumc-auction-cancel"), () => OnCancelAuction?.Invoke(id), true, 190));
        }

        _page.AddChild(Card(running));

        // An order to the lowest offer.
        var tenders = Column(6);
        tenders.AddChild(Gold(Loc.GetString("rumc-tender-title")));
        tenders.AddChild(Wrapped(Loc.GetString("rumc-tender-hint"), TermPalette.Muted));

        var kindRow = Column(4);
        foreach (var kind in state.People.OrderKinds)
        {
            var id = kind.Id;
            var button = new TermButton { Text = kind.Name, MinWidth = 220, Pressed = _tenderKind == id };
            button.OnPressed += _ =>
            {
                _tenderKind = id;
                _tenderPrice = kind.UnitPrice;
                _tenderAmountIndex = Math.Min(_tenderAmountIndex, Math.Max(0, OrderAmounts.Count(a => a <= kind.MaxAmount) - 1));
                Rebuild();
            };
            kindRow.AddChild(button);
        }

        tenders.AddChild(kindRow);

        var chosen = state.People.OrderKinds.FirstOrDefault(k => k.Id == _tenderKind);
        if (chosen != null)
        {
            var maxIndex = Math.Max(0, OrderAmounts.Count(a => a <= chosen.MaxAmount) - 1);
            var maxPrice = chosen.UnitPrice * 3;
            if (_tenderPrice <= 0)
                _tenderPrice = chosen.UnitPrice;

            var amountLabel = Text(string.Empty, TermPalette.Text);
            var ceilingLabel = Text(string.Empty, TermPalette.Text);

            void UpdateTender()
            {
                _tenderAmountIndex = Math.Clamp(_tenderAmountIndex, 0, maxIndex);
                amountLabel.Text = Loc.GetString("rumc-order-amount", ("value", OrderAmounts[_tenderAmountIndex]));
                ceilingLabel.Text = Loc.GetString("rumc-tender-ceiling", ("value", Money(_tenderPrice)));
            }

            var amountRow = Row(6);
            amountRow.AddChild(Action("−", () => { _tenderAmountIndex--; UpdateTender(); }, true, 36));
            amountRow.AddChild(amountLabel);
            amountRow.AddChild(Action("+", () => { _tenderAmountIndex++; UpdateTender(); }, true, 36));
            tenders.AddChild(amountRow);

            var step = Math.Max(1, chosen.UnitPrice / 10);
            var ceilingRow = Row(6);
            ceilingRow.AddChild(Action("−", () => { _tenderPrice = Math.Max(1, _tenderPrice - step); UpdateTender(); }, true, 36));
            ceilingRow.AddChild(ceilingLabel);
            ceilingRow.AddChild(Action("+", () => { _tenderPrice = Math.Min(maxPrice, _tenderPrice + step); UpdateTender(); }, true, 36));
            tenders.AddChild(ceilingRow);
            UpdateTender();

            tenders.AddChild(MinutesRow(AuctionMinutes, _tenderTime, minutes => _tenderTime = minutes));
            var kindId = chosen.Id;
            tenders.AddChild(Action(Loc.GetString("rumc-tender-start"), () => OnStartTender?.Invoke(kindId, OrderAmounts[_tenderAmountIndex], _tenderPrice, _tenderTime),
                admin.Tenders.Count < 3, 220));
        }

        _page.AddChild(Card(tenders));

        var active = Column(6);
        active.AddChild(Gold(Loc.GetString("rumc-tender-running")));
        if (admin.Tenders.Count == 0)
            active.AddChild(Muted(Loc.GetString("rumc-tender-none")));

        foreach (var tender in admin.Tenders)
        {
            var id = tender.Id;
            active.AddChild(Rule());
            active.AddChild(Text(Loc.GetString("rumc-tender-line", ("kind", tender.Kind), ("amount", tender.Amount), ("max", Money(tender.MaxPrice))), TermPalette.Text));
            active.AddChild(Live(s =>
            {
                var t = s.Admin.Tenders.FirstOrDefault(x => x.Id == id);
                return t == null ? string.Empty : t.Best == 0
                    ? Loc.GetString("rumc-tender-no-bids", ("time", Time(t.LeftSeconds)))
                    : Loc.GetString("rumc-tender-best", ("price", Money(t.Best)), ("leader", t.Leader), ("time", Time(t.LeftSeconds)));
            }, TermPalette.Gold));
            active.AddChild(Danger(Loc.GetString("rumc-tender-cancel"), () => OnCancelTender?.Invoke(id), true, 190));
        }

        _page.AddChild(Card(active));
    }

    // A row of buttons for a time in minutes: the one that is pressed is kept in a field of the window.
    private Control MinutesRow(int[] choices, int current, Action<int> set)
    {
        var row = Row(6);
        foreach (var minutes in choices)
        {
            var picked = minutes;
            var button = new TermButton { Text = Loc.GetString("rumc-poll-minutes", ("value", minutes)), MinWidth = 90, Pressed = current == minutes };
            button.OnPressed += _ =>
            {
                set(picked);
                Rebuild();
            };
            row.AddChild(button);
        }

        return row;
    }

    // ---------------------------------------------------------------- the loans to the players

    private static string LoansSignature(ColonyGovernmentState state)
    {
        var admin = state.Admin;
        return "l|" + string.Join(";", state.People.Nearby.Select(n => n.Entity))
               + "|" + string.Join(";", admin.Offers.Select(o => o.Id))
               + "|" + string.Join(";", admin.Loans.Select(l => $"{l.Id}:{l.Left}:{l.Missed}"));
    }

    private void BuildLoans(ColonyGovernmentState state)
    {
        var admin = state.Admin;

        var form = Column(6);
        form.AddChild(Gold(Loc.GetString("rumc-playerloan-title")));
        form.AddChild(Wrapped(Loc.GetString("rumc-playerloan-hint"), TermPalette.Muted));
        form.AddChild(Rule());
        form.AddChild(Text(Loc.GetString("rumc-government-license-nearby"), TermPalette.Text));
        if (state.People.Nearby.Count == 0)
            form.AddChild(Muted(Loc.GetString("rumc-government-license-no-nearby")));

        foreach (var person in state.People.Nearby)
        {
            var entity = person.Entity;
            var button = new TermButton { Text = person.Name, MinWidth = 220, Pressed = _loanTarget == entity };
            button.OnPressed += _ =>
            {
                _loanTarget = _loanTarget == entity ? null : entity;
                Rebuild();
            };
            form.AddChild(button);
        }

        form.AddChild(Rule());
        form.AddChild(ChoiceRow(Loc.GetString("rumc-playerloan-principal"), LoanPrincipals, _loanPrincipal, value => Money(value), value => _loanPrincipal = value));
        form.AddChild(ChoiceRow(Loc.GetString("rumc-playerloan-instalments"), LoanInstalments, _loanInstalments, value => value.ToString(), value => _loanInstalments = value));
        form.AddChild(ChoiceRow(Loc.GetString("rumc-playerloan-interest"), LoanInterests, _loanInterest, value => value + "%", value => _loanInterest = value));

        var instalment = (int) Math.Ceiling(_loanPrincipal * (100 + _loanInterest) / 100f / _loanInstalments);
        form.AddChild(Text(Loc.GetString("rumc-playerloan-terms", ("instalment", Money(instalment)), ("total", Money(instalment * _loanInstalments))), TermPalette.Gold));
        form.AddChild(Action(Loc.GetString("rumc-playerloan-offer"), () =>
        {
            if (_loanTarget is { } target)
                OnOfferLoan?.Invoke(target, _loanPrincipal, _loanInstalments, _loanInterest);
        }, _loanTarget != null && state.People.Nearby.Any(n => n.Entity == _loanTarget) && state.Budget >= _loanPrincipal, 220));
        _page.AddChild(Card(form));

        var offers = Column(6);
        offers.AddChild(Gold(Loc.GetString("rumc-playerloan-offers")));
        if (admin.Offers.Count == 0)
            offers.AddChild(Muted(Loc.GetString("rumc-playerloan-offers-none")));

        foreach (var offer in admin.Offers)
        {
            var id = offer.Id;
            offers.AddChild(Rule());
            offers.AddChild(Text(Loc.GetString("rumc-playerloan-offer-line", ("name", offer.Name), ("sum", Money(offer.Principal)), ("count", offer.Instalments), ("instalment", Money(offer.Instalment))), TermPalette.Text));
            offers.AddChild(Danger(Loc.GetString("rumc-playerloan-withdraw"), () => OnWithdrawLoanOffer?.Invoke(id), true, 190));
        }

        _page.AddChild(Card(offers));

        var loans = Column(6);
        loans.AddChild(Gold(Loc.GetString("rumc-playerloan-loans")));
        if (admin.Loans.Count == 0)
            loans.AddChild(Muted(Loc.GetString("rumc-playerloan-loans-none")));

        foreach (var loan in admin.Loans)
        {
            var id = loan.Id;
            loans.AddChild(Rule());
            loans.AddChild(Text(Loc.GetString("rumc-playerloan-loan-line", ("name", loan.Borrower), ("left", loan.Left), ("instalment", Money(loan.Instalment))), TermPalette.Text));
            if (loan.Missed > 0)
                loans.AddChild(Text(Loc.GetString("rumc-playerloan-missed-line", ("count", loan.Missed)), TermPalette.Bad));

            loans.AddChild(Live(s => Loc.GetString("rumc-playerloan-next", ("time", Time(s.Admin.Loans.FirstOrDefault(l => l.Id == id)?.NextSeconds ?? 0))), TermPalette.Muted));
            loans.AddChild(Danger(Loc.GetString("rumc-playerloan-forgive"), () => OnForgiveLoan?.Invoke(id), true, 190));
        }

        _page.AddChild(Card(loans));
    }

    // A label and a row of buttons for a choice among numbers.
    private Control ChoiceRow(string label, int[] choices, int current, Func<int, string> format, Action<int> set)
    {
        var column = Column(4);
        column.AddChild(Text(label, TermPalette.Text));
        var row = Row(6);
        foreach (var choice in choices)
        {
            var picked = choice;
            var button = new TermButton { Text = format(choice), MinWidth = 90, Pressed = current == choice };
            button.OnPressed += _ =>
            {
                set(picked);
                Rebuild();
            };
            row.AddChild(button);
        }

        column.AddChild(row);
        return column;
    }

    // ---------------------------------------------------------------- the posts

    // ---------------------------------------------------------------- the decrees

}
