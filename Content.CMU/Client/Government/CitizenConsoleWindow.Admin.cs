// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using Content.Client.CMU14.WorldMarket;
using Content.Shared.CMU14.Government;
using static Content.Client.CMU14.WorldMarket.TermKit;

namespace Content.Client.CMU14.Government;

// The three last tabs of the window of the console of the people: the auctions and the tenders (the players bid against each other), the loans that the mayor offers and the loans that are held, and the decrees
// that stand (the laws of the city, which the BKM keep).
public sealed partial class CitizenConsoleWindow
{
    private readonly Dictionary<int, string> _bidDrafts = new();
    private readonly Dictionary<int, string> _tenderDrafts = new();

    private static int Parse(string text, int fallback)
    {
        return int.TryParse(text, out var value) ? value : fallback;
    }

    // ---------------------------------------------------------------- the auctions and the tenders

    private void BuildAuctions(CitizenConsoleState state)
    {
        var admin = state.Admin;

        var plots = Column(6);
        plots.AddChild(Gold(Loc.GetString("rumc-auction-title")));
        plots.AddChild(Wrapped(Loc.GetString("rumc-citizen-auction-hint"), TermPalette.Muted));
        if (admin.Auctions.Count == 0)
            plots.AddChild(Muted(Loc.GetString("rumc-citizen-auction-none")));

        foreach (var auction in admin.Auctions)
        {
            var id = auction.Id;
            plots.AddChild(Rule());
            plots.AddChild(Text(Loc.GetString("rumc-auction-line", ("zone", auction.Zone), ("place", auction.Place)), TermPalette.Text));
            plots.AddChild(Live(s =>
            {
                var a = s.Admin.Auctions.FirstOrDefault(x => x.Id == id);
                return a == null ? string.Empty : a.Current == 0
                    ? Loc.GetString("rumc-auction-no-bids", ("start", Money(a.StartPrice)), ("time", Time(a.LeftSeconds)))
                    : Loc.GetString("rumc-auction-best", ("sum", Money(a.Current)), ("leader", a.Leader), ("time", Time(a.LeftSeconds)));
            }, TermPalette.Gold));
            plots.AddChild(Live(s => Loc.GetString("rumc-auction-min", ("min", Money(s.Admin.Auctions.FirstOrDefault(x => x.Id == id)?.MinBid ?? 0))), TermPalette.Muted));

            var minBid = auction.MinBid;
            var row = Row(8);
            row.AddChild(Edit(_bidDrafts.GetValueOrDefault(id, minBid.ToString()), "0", 6, text => _bidDrafts[id] = new string(text.Where(char.IsDigit).ToArray())));
            row.AddChild(Action(Loc.GetString("rumc-auction-bid"), () => OnBidAuction?.Invoke(id, Parse(_bidDrafts.GetValueOrDefault(id, minBid.ToString()), minBid)), true, 150));
            plots.AddChild(row);
        }

        _page.AddChild(Card(plots));

        var tenders = Column(6);
        tenders.AddChild(Gold(Loc.GetString("rumc-tender-title")));
        tenders.AddChild(Wrapped(Loc.GetString("rumc-citizen-tender-hint"), TermPalette.Muted));
        if (admin.Tenders.Count == 0)
            tenders.AddChild(Muted(Loc.GetString("rumc-citizen-tender-none")));

        foreach (var tender in admin.Tenders)
        {
            var id = tender.Id;
            tenders.AddChild(Rule());
            tenders.AddChild(Text(Loc.GetString("rumc-tender-line", ("kind", tender.Kind), ("amount", tender.Amount), ("max", Money(tender.MaxPrice))), TermPalette.Text));
            tenders.AddChild(Wrapped(tender.Description, TermPalette.Muted));
            tenders.AddChild(Live(s =>
            {
                var t = s.Admin.Tenders.FirstOrDefault(x => x.Id == id);
                return t == null ? string.Empty : t.Best == 0
                    ? Loc.GetString("rumc-tender-no-bids", ("time", Time(t.LeftSeconds)))
                    : Loc.GetString("rumc-tender-best", ("price", Money(t.Best)), ("leader", t.Leader), ("time", Time(t.LeftSeconds)));
            }, TermPalette.Gold));

            var start = tender.Best == 0 ? tender.MaxPrice : tender.Best - 1;
            var row = Row(8);
            row.AddChild(Edit(_tenderDrafts.GetValueOrDefault(id, start.ToString()), "0", 6, text => _tenderDrafts[id] = new string(text.Where(char.IsDigit).ToArray())));
            row.AddChild(Action(Loc.GetString("rumc-tender-offer"), () => OnBidTender?.Invoke(id, Parse(_tenderDrafts.GetValueOrDefault(id, start.ToString()), start)), true, 150));
            tenders.AddChild(row);
        }

        _page.AddChild(Card(tenders));
    }

    // ---------------------------------------------------------------- the loans

    private void BuildLoans(CitizenConsoleState state)
    {
        var admin = state.Admin;
        var (me, myName) = Me();

        var offers = Column(6);
        offers.AddChild(Gold(Loc.GetString("rumc-playerloan-offers-yours")));
        var mine = admin.Offers.Where(o => o.Borrower == me).ToList();
        if (mine.Count == 0)
            offers.AddChild(Muted(Loc.GetString("rumc-playerloan-offers-yours-none")));

        foreach (var offer in mine)
        {
            var id = offer.Id;
            offers.AddChild(Rule());
            offers.AddChild(Text(Loc.GetString("rumc-playerloan-your-offer", ("sum", Money(offer.Principal)), ("count", offer.Instalments), ("instalment", Money(offer.Instalment)),
                ("interest", offer.Interest)), TermPalette.Text));
            offers.AddChild(Wrapped(Loc.GetString("rumc-playerloan-warning"), TermPalette.Bad));

            var buttons = Row(8);
            buttons.AddChild(Action(Loc.GetString("rumc-playerloan-accept"), () => OnAcceptLoan?.Invoke(id), true, 150));
            buttons.AddChild(Danger(Loc.GetString("rumc-playerloan-decline"), () => OnDeclineLoan?.Invoke(id), true, 150));
            offers.AddChild(buttons);
        }

        _page.AddChild(Card(offers));

        var loans = Column(6);
        loans.AddChild(Gold(Loc.GetString("rumc-playerloan-loans-yours")));
        var held = admin.Loans.Where(l => l.Borrower == myName).ToList();
        if (held.Count == 0)
            loans.AddChild(Muted(Loc.GetString("rumc-playerloan-loans-yours-none")));

        foreach (var loan in held)
        {
            var id = loan.Id;
            loans.AddChild(Rule());
            loans.AddChild(Text(Loc.GetString("rumc-playerloan-your-loan", ("left", loan.Left), ("instalment", Money(loan.Instalment)), ("owed", Money(loan.Left * loan.Instalment))), TermPalette.Text));
            if (loan.Missed > 0)
                loans.AddChild(Text(Loc.GetString("rumc-playerloan-missed-line", ("count", loan.Missed)), TermPalette.Bad));

            loans.AddChild(Live(s => Loc.GetString("rumc-playerloan-next", ("time", Time(s.Admin.Loans.FirstOrDefault(l => l.Id == id)?.NextSeconds ?? 0))), TermPalette.Muted));
            loans.AddChild(Action(Loc.GetString("rumc-playerloan-repay"), () => OnRepayLoan?.Invoke(id), true, 220));
        }

        _page.AddChild(Card(loans));
    }

    // ---------------------------------------------------------------- the decrees

}
