// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using Content.Client.CMU14.WorldMarket;
using Content.Shared.CMU14.Government;
using Robust.Client.UserInterface.Controls;
using static Content.Client.CMU14.WorldMarket.TermKit;

namespace Content.Client.CMU14.Government;

// The four last tabs of the window of the mayor: the rewards, the orders, the appeals and the votes of the players, and the register of the notary.
public sealed partial class ColonyGovernmentWindow
{
    private static readonly int[] OrderAmounts = { 5, 10, 25, 50, 100 };
    private static readonly int[] PollMinutes = { 3, 5, 10, 20 };

    // What is typed or picked and not yet sent: it is held here, so that a page made again does not take it away.
    private NetEntity? _awardTarget;
    private string _reasonDraft = string.Empty;
    private readonly Dictionary<string, int> _orderAmountIndex = new();
    private readonly Dictionary<string, int> _orderPrice = new();
    private string _questionDraft = string.Empty;
    private string _optionsDraft = string.Empty;
    private int _pollMinutes = 5;
    private readonly Dictionary<int, string> _replyDrafts = new();

    private static LineEdit Edit(string text, string placeholder, int max, Action<string> changed)
    {
        var edit = new LineEdit { HorizontalExpand = true, PlaceHolder = placeholder, Text = text, IsValid = value => value.Length <= max };
        edit.OnTextChanged += args => changed(args.Text);
        return edit;
    }

    private void Rebuild()
    {
        _signature = string.Empty;
        if (_state != null)
            SetState(_state);
    }

    // ---------------------------------------------------------------- the rewards

    private static string AwardsSignature(ColonyGovernmentState state)
    {
        var people = state.People;
        return "a|" + string.Join(";", people.Nearby.Select(n => n.Entity))
               + "|" + string.Join(";", people.Awards.Select(a => $"{a.Id}:{state.Budget >= a.Cost + a.Cash}"))
               + "|" + people.History.Count;
    }

    private void BuildAwards(ColonyGovernmentState state)
    {
        var people = state.People;

        var head = Column(6);
        head.AddChild(Gold(Loc.GetString("rumc-award-title")));
        head.AddChild(Wrapped(Loc.GetString("rumc-award-hint"), TermPalette.Muted));
        head.AddChild(Rule());
        head.AddChild(Text(Loc.GetString("rumc-government-license-nearby"), TermPalette.Text));
        if (people.Nearby.Count == 0)
            head.AddChild(Muted(Loc.GetString("rumc-government-license-no-nearby")));

        foreach (var person in people.Nearby)
        {
            var entity = person.Entity;
            var button = new TermButton { Text = person.Name, MinWidth = 220, Pressed = _awardTarget == entity };
            button.OnPressed += _ =>
            {
                _awardTarget = _awardTarget == entity ? null : entity;
                Rebuild();
            };
            head.AddChild(button);
        }

        head.AddChild(Rule());
        head.AddChild(Text(Loc.GetString("rumc-award-reason"), TermPalette.Text));
        head.AddChild(Edit(_reasonDraft, Loc.GetString("rumc-award-reason-placeholder"), 80, text => _reasonDraft = text));
        _page.AddChild(Card(head));

        var canTarget = _awardTarget != null && people.Nearby.Any(n => n.Entity == _awardTarget);
        foreach (var category in new[] { AwardCategory.Guard, AwardCategory.Build, AwardCategory.Trade, AwardCategory.Other })
        {
            var column = Column(4);
            column.AddChild(Gold(Loc.GetString("rumc-award-category-" + category.ToString().ToLowerInvariant())));
            foreach (var award in people.Awards.Where(a => a.Category == category))
            {
                var id = award.Id;
                column.AddChild(Rule());
                column.AddChild(Text(award.Name, TermPalette.Text));
                column.AddChild(Wrapped(award.Description, TermPalette.Muted));
                column.AddChild(Muted(Loc.GetString("rumc-award-cost", ("cost", Money(award.Cost)), ("cash", Money(award.Cash)), ("medal", award.Medal ? Loc.GetString("rumc-award-medal-yes") : "—"))));
                column.AddChild(Action(Loc.GetString("rumc-award-give"), () =>
                {
                    if (_awardTarget is { } target)
                        OnGrantAward?.Invoke(target, id, _reasonDraft);
                }, canTarget && state.Budget >= award.Cost + award.Cash, 150));
            }

            _page.AddChild(Card(column));
        }

        var history = Column(4);
        history.AddChild(Gold(Loc.GetString("rumc-award-history")));
        if (people.History.Count == 0)
            history.AddChild(Muted(Loc.GetString("rumc-award-history-none")));

        foreach (var entry in people.History)
        {
            history.AddChild(Rule());
            history.AddChild(Text(Loc.GetString("rumc-award-history-line", ("holder", entry.Holder), ("award", entry.Award)), TermPalette.Text));
            history.AddChild(Muted(Loc.GetString("rumc-award-history-reason", ("category", entry.Category), ("reason", entry.Reason))));
        }

        _page.AddChild(Card(history));
    }

    // ---------------------------------------------------------------- the orders

    private static string OrdersSignature(ColonyGovernmentState state)
    {
        var people = state.People;
        return "o|" + people.Orders.Count + ":" + string.Join(";", people.Orders.Select(o => $"{o.Id}:{o.Left}"))
               + "|" + string.Join(";", people.OrderKinds.Select(k => $"{k.Id}:{state.Budget >= k.UnitPrice}"));
    }

    private void BuildOrders(ColonyGovernmentState state)
    {
        var people = state.People;

        var head = Column(6);
        head.AddChild(Gold(Loc.GetString("rumc-order-title")));
        head.AddChild(Wrapped(Loc.GetString("rumc-order-hint"), TermPalette.Muted));
        _page.AddChild(Card(head));

        foreach (var kind in people.OrderKinds)
        {
            var id = kind.Id;
            var unit = kind.UnitPrice;
            var minPrice = Math.Max(1, unit / 2);
            var maxPrice = unit * 3;
            var maxIndex = OrderAmounts.Count(a => a <= kind.MaxAmount) - 1;

            var amountIndex = Math.Clamp(_orderAmountIndex.GetValueOrDefault(id, 1), 0, Math.Max(0, maxIndex));
            var price = Math.Clamp(_orderPrice.GetValueOrDefault(id, unit), minPrice, maxPrice);

            var column = Column(4);
            column.AddChild(Text(kind.Name, TermPalette.Text));
            column.AddChild(Wrapped(kind.Description, TermPalette.Muted));
            column.AddChild(Muted(Loc.GetString("rumc-order-fair", ("price", Money(unit)))));

            var total = Text(string.Empty, TermPalette.Gold);
            var amountLabel = Text(string.Empty, TermPalette.Text);
            var priceLabel = Text(string.Empty, TermPalette.Text);

            void Update()
            {
                _orderAmountIndex[id] = amountIndex;
                _orderPrice[id] = price;
                amountLabel.Text = Loc.GetString("rumc-order-amount", ("value", OrderAmounts[amountIndex]));
                priceLabel.Text = Loc.GetString("rumc-order-price", ("value", Money(price)));
                total.Text = Loc.GetString("rumc-order-total", ("sum", Money(OrderAmounts[amountIndex] * price)));
            }

            var amountRow = Row(6);
            amountRow.AddChild(Action("−", () => { amountIndex = Math.Max(0, amountIndex - 1); Update(); }, true, 36));
            amountRow.AddChild(amountLabel);
            amountRow.AddChild(Action("+", () => { amountIndex = Math.Min(maxIndex, amountIndex + 1); Update(); }, true, 36));
            column.AddChild(amountRow);

            var step = Math.Max(1, unit / 10);
            var priceRow = Row(6);
            priceRow.AddChild(Action("−", () => { price = Math.Max(minPrice, price - step); Update(); }, true, 36));
            priceRow.AddChild(priceLabel);
            priceRow.AddChild(Action("+", () => { price = Math.Min(maxPrice, price + step); Update(); }, true, 36));
            column.AddChild(priceRow);

            Update();
            column.AddChild(total);
            column.AddChild(Action(Loc.GetString("rumc-order-create"), () => OnCreateOrder?.Invoke(id, OrderAmounts[amountIndex], price),
                people.Orders.Count < 5 && state.Budget >= OrderAmounts[amountIndex] * price, 170));
            _page.AddChild(Card(column));
        }

        var active = Column(6);
        active.AddChild(Gold(Loc.GetString("rumc-order-active")));
        if (people.Orders.Count == 0)
            active.AddChild(Muted(Loc.GetString("rumc-order-active-none")));

        foreach (var order in people.Orders)
        {
            var id = order.Id;
            active.AddChild(Rule());
            active.AddChild(Text(Loc.GetString("rumc-order-line", ("kind", order.Kind), ("left", order.Left), ("total", order.Total), ("price", Money(order.Price))), TermPalette.Text));
            active.AddChild(Live(s => Loc.GetString("rumc-order-time", ("time", Time(s.People.Orders.FirstOrDefault(o => o.Id == id)?.LeftSeconds ?? 0))), TermPalette.Muted));
            active.AddChild(Danger(Loc.GetString("rumc-order-cancel"), () => OnCancelOrder?.Invoke(id), true, 170));
        }

        _page.AddChild(Card(active));
    }

    // ---------------------------------------------------------------- the appeals and the votes

    private static string AppealsSignature(ColonyGovernmentState state)
    {
        var people = state.People;
        return "p|" + string.Join(";", people.Appeals.Select(a => $"{a.Id}:{a.Status}"))
               + "|" + (people.Poll?.Id ?? 0)
               + "|" + string.Join(";", people.PastPolls.Select(p => p.Id));
    }

    private void BuildAppeals(ColonyGovernmentState state)
    {
        var people = state.People;

        // The vote.
        var poll = Column(6);
        poll.AddChild(Gold(Loc.GetString("rumc-poll-title")));
        if (people.Poll is { } active)
        {
            poll.AddChild(Text(active.Question, TermPalette.Text));
            for (var i = 0; i < active.Options.Count; i++)
            {
                var index = i;
                poll.AddChild(Live(s => Loc.GetString("rumc-poll-option-line", ("option", s.People.Poll?.Options.ElementAtOrDefault(index) ?? string.Empty),
                    ("votes", s.People.Poll?.Counts.ElementAtOrDefault(index) ?? 0)), TermPalette.Text));
            }

            poll.AddChild(Live(s => Loc.GetString("rumc-poll-left", ("time", Time(s.People.Poll?.LeftSeconds ?? 0)), ("voted", s.People.Poll?.Voted.Count ?? 0)), TermPalette.Muted));
            poll.AddChild(Danger(Loc.GetString("rumc-poll-close"), () => OnClosePoll?.Invoke(), true, 190));
        }
        else
        {
            poll.AddChild(Wrapped(Loc.GetString("rumc-poll-hint"), TermPalette.Muted));
            poll.AddChild(Edit(_questionDraft, Loc.GetString("rumc-poll-question"), 100, text => _questionDraft = text));
            poll.AddChild(Edit(_optionsDraft, Loc.GetString("rumc-poll-options"), 170, text => _optionsDraft = text));

            var times = Row(6);
            foreach (var minutes in PollMinutes)
            {
                var picked = minutes;
                var button = new TermButton { Text = Loc.GetString("rumc-poll-minutes", ("value", minutes)), MinWidth = 90, Pressed = _pollMinutes == minutes };
                button.OnPressed += _ =>
                {
                    _pollMinutes = picked;
                    Rebuild();
                };
                times.AddChild(button);
            }

            poll.AddChild(times);
            poll.AddChild(Action(Loc.GetString("rumc-poll-start"), () =>
            {
                var options = _optionsDraft.Split(';').Select(o => o.Trim()).Where(o => o.Length > 0).ToList();
                OnStartPoll?.Invoke(_questionDraft, options, _pollMinutes);
            }, true, 190));
        }

        _page.AddChild(Card(poll));

        if (people.PastPolls.Count > 0)
        {
            var past = Column(4);
            past.AddChild(Gold(Loc.GetString("rumc-poll-past")));
            foreach (var old in people.PastPolls)
            {
                past.AddChild(Rule());
                past.AddChild(Text(old.Question, TermPalette.Text));
                for (var i = 0; i < old.Options.Count; i++)
                    past.AddChild(Muted(Loc.GetString("rumc-poll-option-line", ("option", old.Options[i]), ("votes", old.Counts[i]))));
            }

            _page.AddChild(Card(past));
        }

        // The appeals.
        var appeals = Column(6);
        appeals.AddChild(Gold(Loc.GetString("rumc-appeal-title")));
        appeals.AddChild(Wrapped(Loc.GetString("rumc-appeal-hint"), TermPalette.Muted));
        if (people.Appeals.Count == 0)
            appeals.AddChild(Muted(Loc.GetString("rumc-appeal-none")));

        foreach (var appeal in people.Appeals)
        {
            var id = appeal.Id;
            appeals.AddChild(Rule());
            appeals.AddChild(Text(Loc.GetString("rumc-appeal-line", ("author", appeal.Author)), TermPalette.Gold));
            appeals.AddChild(Wrapped(appeal.Text, TermPalette.Text));

            if (appeal.Status != AppealStatus.Open)
            {
                appeals.AddChild(Text(Loc.GetString(appeal.Status == AppealStatus.Accepted ? "rumc-appeal-status-accepted" : "rumc-appeal-status-rejected"),
                    appeal.Status == AppealStatus.Accepted ? TermPalette.Good : TermPalette.Bad));
                if (appeal.Reply.Length > 0)
                    appeals.AddChild(Muted(Loc.GetString("rumc-appeal-reply", ("reply", appeal.Reply))));

                continue;
            }

            appeals.AddChild(Edit(_replyDrafts.GetValueOrDefault(id, string.Empty), Loc.GetString("rumc-appeal-reply-placeholder"), 100, text => _replyDrafts[id] = text));
            var buttons = Row(8);
            buttons.AddChild(Action(Loc.GetString("rumc-appeal-accept"), () => OnAnswerAppeal?.Invoke(id, true, _replyDrafts.GetValueOrDefault(id, string.Empty)), true, 130));
            buttons.AddChild(Danger(Loc.GetString("rumc-appeal-reject"), () => OnAnswerAppeal?.Invoke(id, false, _replyDrafts.GetValueOrDefault(id, string.Empty)), true, 130));
            appeals.AddChild(buttons);
        }

        _page.AddChild(Card(appeals));
    }

    // ---------------------------------------------------------------- the notary

    private static string NotarySignature(ColonyGovernmentState state)
    {
        return "n|" + state.People.Registry.Count + ":" + state.People.NotaryTotal;
    }

    private void BuildNotary(ColonyGovernmentState state)
    {
        var people = state.People;

        var head = Column(6);
        head.AddChild(Gold(Loc.GetString("rumc-notary-title")));
        head.AddChild(Wrapped(Loc.GetString("rumc-notary-hint"), TermPalette.Muted));
        head.AddChild(Text(Loc.GetString("rumc-notary-income", ("sum", Money(people.NotaryTotal))), TermPalette.Good));
        _page.AddChild(Card(head));

        var register = Column(4);
        register.AddChild(Gold(Loc.GetString("rumc-notary-register")));
        if (people.Registry.Count == 0)
            register.AddChild(Muted(Loc.GetString("rumc-notary-register-none")));

        foreach (var contract in people.Registry)
        {
            register.AddChild(Rule());
            register.AddChild(Text(Loc.GetString("rumc-notary-register-line", ("number", contract.Number), ("kind", contract.Kind), ("first", contract.First), ("second", contract.Second)), TermPalette.Text));
            register.AddChild(Muted(Loc.GetString("rumc-notary-register-terms", ("sum", Money(contract.Sum)), ("fee", Money(contract.Fee)), ("subject", contract.Subject))));
        }

        _page.AddChild(Card(register));
    }
}
