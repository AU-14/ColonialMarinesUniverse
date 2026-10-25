// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.WorldMarket;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using static Content.Client.CMU14.WorldMarket.TermKit;

namespace Content.Client.CMU14.WorldMarket;

// The window of the exchange with the mother faction, which the console of the ambassador opens: the two scales of the ambassador (the money of the budget and the points of influence), and
// what can be ordered for the points: private military companies of the factions (as many as there is influence for), food, medicine, workers, holidays from taxes. A small window of its own
// changes the points for money at a set rate (the subsidy of the mother faction).
public sealed class MotherExchangeWindow : BaseWindow
{
    private const float WindowWidth = 560f;

    private readonly Label _budget;
    private readonly Label _points;
    private readonly BoxContainer _page = Column(6);
    private readonly ScrollContainer _scroll;
    private readonly SubsidyWindow _subsidy = new();

    private AmbassadorMotherState? _state;

    // An order (the offer, and the place that it is sent to: nothing for what is not sent).
    public event Action<string, string>? OnOrder;

    // Points changed for money (how many points).
    public event Action<int>? OnSubsidy;

    // The report that the mother faction asked for is sent.
    public event Action? OnReport;

    public MotherExchangeWindow()
    {
        Resizable = false;
        MinSize = new Vector2(WindowWidth, 0);
        MouseFilter = MouseFilterMode.Stop;

        _budget = Text(string.Empty, TermPalette.Good);
        _points = Text(string.Empty, TermPalette.Gold);

        var close = new TermButton { Text = "✕", MinWidth = 28 };
        close.OnPressed += _ => Close();

        var header = Row(10);
        header.AddChild(Gold(Loc.GetString("rumc-mother-title"), expand: true));
        header.AddChild(Muted(Loc.GetString("rumc-mother-budget")));
        header.AddChild(_budget);
        header.AddChild(Muted(Loc.GetString("rumc-mother-points")));
        header.AddChild(_points);
        header.AddChild(close);

        var subsidyButton = new TermButton { Text = Loc.GetString("rumc-mother-subsidy-open") };
        subsidyButton.OnPressed += _ => OpenSubsidy();

        var bar = Row(8);
        bar.AddChild(Muted(Loc.GetString("rumc-mother-hint"), expand: true));
        bar.AddChild(subsidyButton);

        _scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, MinSize = new Vector2(0, 460), HorizontalExpand = true };
        _scroll.AddChild(_page);

        var root = Column(6);
        root.Margin = new Thickness(8, 6, 8, 8);
        root.AddChild(header);
        root.AddChild(bar);
        root.AddChild(Rule());
        root.AddChild(_scroll);

        AddChild(new TermPanel(scanlines: true) { MouseFilter = MouseFilterMode.Pass, Children = { root } });

        _subsidy.OnSubsidy += points => OnSubsidy?.Invoke(points);

        // The small window goes with the big one.
        OnClose += () => _subsidy.Close();
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos) => relativeMousePos.Y < 34 ? DragMode.Move : DragMode.None;

    private void OpenSubsidy()
    {
        if (_state != null)
            _subsidy.SetState(_state);

        _subsidy.OpenCentered();
    }

    public void SetState(AmbassadorMotherState state)
    {
        _state = state;
        _budget.Text = Money(state.Budget);
        _points.Text = state.Points.ToString();
        _subsidy.SetState(state);

        // The page is made again; where it was scrolled to is kept.
        var scroll = _scroll.GetScrollValue();
        _page.RemoveAllChildren();
        Build(state);
        _scroll.SetScrollValue(scroll);
    }

    private static string GroupName(MotherOfferGroup group)
    {
        return Loc.GetString(group switch
        {
            MotherOfferGroup.Pmc => "rumc-mother-group-pmc",
            MotherOfferGroup.Food => "rumc-mother-group-food",
            MotherOfferGroup.Medicine => "rumc-mother-group-medicine",
            MotherOfferGroup.Workers => "rumc-mother-group-workers",
            MotherOfferGroup.Transport => "rumc-mother-group-transport",
            MotherOfferGroup.Support => "rumc-mother-group-support",
            _ => "rumc-mother-group-tax",
        });
    }

    private static Control Wrapped(string text, Color color)
    {
        var label = new RichTextLabel { MaxWidth = WindowWidth - 70f, HorizontalExpand = true };
        label.SetMessage(FormattedMessage.FromUnformatted(text), null, color);
        return label;
    }

    private Control TasksPanel(AmbassadorMotherState state)
    {
        var column = Column(6);
        column.AddChild(Gold(Loc.GetString("rumc-mother-tasks")));

        var prices = state.Trust >= 70 ? "rumc-mother-prices-low" : state.Trust < 30 ? "rumc-mother-prices-high" : "rumc-mother-prices-normal";
        column.AddChild(Text(Loc.GetString("rumc-mother-trust", ("value", state.Trust), ("prices", Loc.GetString(prices))), state.Trust >= 70 ? TermPalette.Good : state.Trust < 30 ? new Color(1f, 0.5f, 0.45f) : TermPalette.Text));
        column.AddChild(new FillBar(state.Trust / 100f, state.Trust >= 70 ? TermPalette.Good : state.Trust < 30 ? new Color(1f, 0.45f, 0.4f) : TermPalette.Gold));
        column.AddChild(Muted(Loc.GetString("rumc-mother-stipend", ("sum", Money(state.StipendAmount)), ("time", Time(state.StipendSeconds)))));

        if (state.Tasks.Count == 0)
            column.AddChild(Muted(Loc.GetString("rumc-mother-no-tasks")));

        foreach (var task in state.Tasks)
        {
            column.AddChild(Rule());

            var head = Row(8);
            head.AddChild(Text(Loc.GetString(task.Report ? "rumc-mother-report" : "rumc-mother-goal", ("name", task.Name)), task.Report ? TermPalette.Text : TermPalette.Gold, expand: true));
            if (task.SecondsLeft >= 0)
                head.AddChild(Muted(Time(task.SecondsLeft)));

            column.AddChild(head);

            column.AddChild(Wrapped(task.Description, TermPalette.Muted));
            foreach (var line in task.Lines)
                column.AddChild(Wrapped(line, line.StartsWith("✓") ? TermPalette.Good : TermPalette.Text));

            column.AddChild(Muted(Loc.GetString(task.Report ? "rumc-mother-reward" : "rumc-mother-reward-open", ("reward", task.Reward))));

            if (task.Report)
                column.AddChild(Action(Loc.GetString("rumc-mother-send-report"), () => OnReport?.Invoke(), task.CanSend));
        }

        return column;
    }

    private void Build(AmbassadorMotherState state)
    {
        _page.AddChild(Card(TasksPanel(state)));

        if (state.Offers.Count == 0)
        {
            _page.AddChild(Card(Muted(Loc.GetString("rumc-mother-none"))));
            return;
        }

        foreach (var group in state.Offers.GroupBy(o => o.Group))
        {
            var column = Column(6);
            column.AddChild(Gold(GroupName(group.Key)));

            // Companies are told apart by the faction that they come from.
            var faction = string.Empty;
            foreach (var offer in group)
            {
                if (group.Key is MotherOfferGroup.Pmc or MotherOfferGroup.Support && offer.Faction != faction)
                {
                    faction = offer.Faction;
                    column.AddChild(Rule());
                    column.AddChild(Text(faction, TermPalette.Text));
                }

                column.AddChild(OfferCard(offer));
            }

            _page.AddChild(Card(column));
        }
    }

    private Control OfferCard(MotherOfferEntry offer)
    {
        var column = Column(3);

        var head = Row(8);
        head.AddChild(Text(offer.Name, TermPalette.Text, expand: true));
        head.AddChild(Muted(Loc.GetString("rumc-mother-cost", ("points", offer.Cost))));
        column.AddChild(head);

        column.AddChild(Wrapped(offer.Description, TermPalette.Muted));

        var id = offer.Id;

        // A transport is ordered to a place: a button for each.
        if (offer.Group == MotherOfferGroup.Transport && offer.Targets.Count > 0)
        {
            foreach (var target in offer.Targets)
            {
                var targetId = target.Id;
                column.AddChild(Action(Loc.GetString("rumc-mother-order-to", ("target", target.Name)), () => OnOrder?.Invoke(id, targetId), offer.Blocked.Length == 0));
            }

            if (offer.Blocked.Length > 0)
                column.AddChild(Muted(offer.Blocked));

            return column;
        }

        var row = Row(8);
        row.AddChild(Action(Loc.GetString("rumc-mother-order"), () => OnOrder?.Invoke(id, string.Empty), offer.Blocked.Length == 0, 90));
        if (offer.Blocked.Length > 0)
            row.AddChild(Muted(offer.Blocked, expand: true));

        column.AddChild(row);
        return column;
    }
}

// The small window of the subsidy: it changes points of influence for money at a set rate.
public sealed class SubsidyWindow : BaseWindow
{
    private readonly Label _rate;
    private readonly Label _points;
    private readonly BoxContainer _buttons = Row(6);

    public event Action<int>? OnSubsidy;

    public SubsidyWindow()
    {
        Resizable = false;
        MinSize = new Vector2(320, 0);
        MouseFilter = MouseFilterMode.Stop;

        var close = new TermButton { Text = "✕", MinWidth = 28 };
        close.OnPressed += _ => Close();

        var header = Row(10);
        header.AddChild(Gold(Loc.GetString("rumc-mother-subsidy-title"), expand: true));
        header.AddChild(close);

        _rate = Text(string.Empty, TermPalette.Good);
        _points = Muted(string.Empty);

        var root = Column(6);
        root.Margin = new Thickness(8, 6, 8, 8);
        root.AddChild(header);
        root.AddChild(Muted(Loc.GetString("rumc-mother-subsidy-hint")));
        root.AddChild(_rate);
        root.AddChild(_points);
        root.AddChild(_buttons);

        AddChild(new TermPanel(scanlines: true) { MouseFilter = MouseFilterMode.Pass, Children = { root } });
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos) => relativeMousePos.Y < 34 ? DragMode.Move : DragMode.None;

    public void SetState(AmbassadorMotherState state)
    {
        _rate.Text = Loc.GetString("rumc-mother-subsidy-rate", ("money", Money(state.MoneyPerPoint)));
        _points.Text = Loc.GetString("rumc-mother-subsidy-points", ("points", state.Points));

        _buttons.RemoveAllChildren();
        foreach (var count in new[] { 1, 5, 10 }.Where(c => c < state.Points).Append(state.Points).Distinct().Where(c => c > 0))
        {
            var points = count;
            _buttons.AddChild(Action(Loc.GetString("rumc-mother-subsidy-take", ("points", points), ("money", Money(points * (float) state.MoneyPerPoint))), () => OnSubsidy?.Invoke(points)));
        }

        if (state.Points <= 0)
            _buttons.AddChild(Muted(Loc.GetString("rumc-mother-subsidy-none")));
    }
}
