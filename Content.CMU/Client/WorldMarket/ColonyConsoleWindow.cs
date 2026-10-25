// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Crafting;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.WorldMarket;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Content.Client.CMU14.WorldMarket.ConsoleKit;

namespace Content.Client.CMU14.WorldMarket;

// The window of the console of the colony administration, in the style of the crafting window and its games: the same dark panel with the gold edge, tabs that are pills, cards, bars.
// The tabs: the treasury and the taxes, the world market (the indices, the companies, the news), the agreements, the quotas of the mother company, the voyages, the black market and the
// services. A page is made again only when what it is made of changes (a new agreement, a new price); the numbers that tick (the time, the progress, the money) are changed in place,
// so that a button is not taken from under the cursor.
public sealed class ColonyConsoleWindow : BaseWindow
{
    private const float WindowWidth = 800f;

    public enum Tab : byte
    {
        Treasury,
        Market,
        Agreements,
        Quotas,
        Voyages,
        Underworld,
        Services,
    }

    private readonly Label _budget;
    private readonly BoxContainer _tabs = Row(4);
    private readonly Dictionary<Tab, TabPill> _pills = new();
    private readonly ScrollContainer _scroll;
    private readonly BoxContainer _page = Column(6);

    // What ticks on the page that is open: it is given the state again every time one comes.
    private readonly List<Action<ColonyConsoleState>> _live = new();

    private ColonyConsoleState? _state;
    private string _signature = string.Empty;
    private Vector2 _pendingScroll;
    private bool _restoreScroll;

    public Tab Current { get; private set; } = Tab.Treasury;

    public ColonyConsoleState? State => _state;

    // Every message that the window sends to the server goes through here.
    public event Action<BoundUserInterfaceMessage>? OnSend;

    public ColonyConsoleWindow()
    {
        Resizable = false;
        MinSize = new Vector2(WindowWidth, 0);
        MouseFilter = MouseFilterMode.Stop;

        var title = Gold(Loc.GetString("rumc-console-title"), expand: true);
        _budget = Text(string.Empty, CraftPalette.Good);
        var close = new Button { Text = "✕", MinWidth = 28 };
        close.OnPressed += _ => Close();

        var header = Row(10);
        header.AddChild(title);
        header.AddChild(Muted(Loc.GetString("rumc-console-treasury-short")));
        header.AddChild(_budget);
        header.AddChild(close);

        foreach (var (tab, key) in new[]
                 {
                     (Tab.Treasury, "rumc-console-tab-treasury"),
                     (Tab.Market, "rumc-console-tab-market"),
                     (Tab.Agreements, "rumc-console-tab-agreements"),
                     (Tab.Quotas, "rumc-console-tab-quotas"),
                     (Tab.Voyages, "rumc-console-tab-voyages"),
                     (Tab.Underworld, "rumc-console-tab-underworld"),
                     (Tab.Services, "rumc-console-tab-services"),
                 })
        {
            var pill = new TabPill(Loc.GetString(key));
            var picked = tab;
            pill.OnPicked += () => Open(picked);
            _pills[tab] = pill;
            _tabs.AddChild(pill);
        }

        _scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, MinSize = new Vector2(0, 430), HorizontalExpand = true };
        _scroll.AddChild(_page);

        var root = Column(6);
        root.Margin = new Thickness(8, 6, 8, 8);
        root.AddChild(header);
        root.AddChild(_tabs);
        root.AddChild(Rule());
        root.AddChild(_scroll);

        AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = CraftPalette.Panel,
                BorderColor = CraftPalette.Gold.WithAlpha(0.55f),
                BorderThickness = new Thickness(1),
            },
            MouseFilter = MouseFilterMode.Pass,
            Children = { root },
        });

        Open(Tab.Treasury);
    }

    public void Open(Tab tab)
    {
        Current = tab;
        foreach (var (key, pill) in _pills)
            pill.Active = key == tab;

        _signature = string.Empty;
        _pendingScroll = Vector2.Zero;
        _restoreScroll = false;

        if (_state != null)
            Rebuild(_state);
    }

    public void SetState(ColonyConsoleState state)
    {
        _state = state;
        _budget.Text = Money(state.Budget);

        var signature = SignatureOf(state);
        if (signature != _signature)
        {
            // The page is made again: where it was scrolled to is kept.
            _pendingScroll = _scroll.GetScrollValue();
            _restoreScroll = true;
            Rebuild(state);
        }

        foreach (var live in _live)
            live(state);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_restoreScroll)
        {
            _restoreScroll = false;
            _scroll.SetScrollValue(_pendingScroll);
        }
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos) => relativeMousePos.Y < 34 ? DragMode.Move : DragMode.None;

    private void Send(BoundUserInterfaceMessage message) => OnSend?.Invoke(message);

    #region Signatures

    // What the page is made of: when it changes the page is made again, and when it does not only the numbers that tick are changed.
    private string SignatureOf(ColonyConsoleState s)
    {
        switch (Current)
        {
            case Tab.Treasury:
                return $"t|{string.Join(",", s.FactionEmbargoes)}|{string.Join(",", s.FactionPacts)}";

            case Tab.Market:
                return "m|" + string.Join(";", s.Indices.Select(i => $"{i.Id}:{(int) Math.Round(i.Value)}:{i.History.Count}"))
                       + "|" + string.Join(";", s.Companies.Select(c => $"{c.Company}{c.Categories}:{c.SellPercent}:{c.BuyPercent}:{c.DutyPercent}:{c.EmbargoSeconds > 0}:{c.Agreement}"))
                       + "|" + string.Join(";", s.News.Select(n => n.Text));

            case Tab.Agreements:
                return "a|" + string.Join(";", s.Agreements.Select(a => $"{a.Id}:{a.Active}:{a.CanAfford}"))
                       + "|" + string.Join(";", s.Companies.Where(c => c.EmbargoSeconds > 0).Select(c => c.Company));

            case Tab.Quotas:
                return $"q|{s.Standing}|{s.InspectionSeconds > 0}|" + string.Join(";", s.Quotas.Select(q => q.Id)) + "|" + string.Join(";", s.QuotaLog.Select(q => $"{q.Text}{q.Done}"));

            case Tab.Voyages:
                return $"v|{s.Voyage.CanUrgent}|{s.Voyage.DelayedSeconds > 0}|{s.Voyage.UrgentCooldownSeconds > 0}";

            case Tab.Underworld:
                return "u|" + string.Join(";", s.Orgs.Select(o => o.Id
                    + string.Join(",", o.Wares.Select(w => $"{w.Id}:{w.Price}:{w.Incoming}:{s.Budget >= w.Price}"))
                    + string.Join(",", o.Orders.Select(r => $"{r.Id}:{r.Taken}"))));

            case Tab.Services:
                return "s|" + string.Join(";", s.Parties.Select(p => $"{p.Id}:{p.Called}:{s.Budget >= p.Cost}"));
        }

        return string.Empty;
    }

    #endregion

    #region The pages

    private void Rebuild(ColonyConsoleState s)
    {
        _signature = SignatureOf(s);
        _live.Clear();
        _page.RemoveAllChildren();

        switch (Current)
        {
            case Tab.Treasury:
                BuildTreasury(s);
                break;
            case Tab.Market:
                BuildMarket(s);
                break;
            case Tab.Agreements:
                BuildAgreements(s);
                break;
            case Tab.Quotas:
                BuildQuotas(s);
                break;
            case Tab.Voyages:
                BuildVoyages(s);
                break;
            case Tab.Underworld:
                BuildUnderworld(s);
                break;
            case Tab.Services:
                BuildServices(s);
                break;
        }

        foreach (var live in _live)
            live(s);
    }

    // A label that is given its text again with every state.
    private Label Live(Func<ColonyConsoleState, string> text, Color? color = null, bool expand = false)
    {
        var label = Text(string.Empty, color, expand);
        _live.Add(state => label.Text = text(state));
        return label;
    }

    private FillBar LiveBar(Func<ColonyConsoleState, (float Fraction, Color Color)> value, int height = 10)
    {
        var bar = new FillBar(0f, CraftPalette.Gold, height);
        _live.Add(state =>
        {
            var (fraction, color) = value(state);
            bar.Set(fraction, color);
        });
        return bar;
    }

    private static Control Heading(string text) => Gold(text);

    // ---------------------------------------------------------------- the treasury

    private void BuildTreasury(ColonyConsoleState s)
    {
        var taxes = Column(6);
        taxes.AddChild(Heading(Loc.GetString("rumc-console-taxes")));
        taxes.AddChild(TaxRow("rumc-console-tax-sales", "rumc-console-tax-sales-hint", state => state.SalesTax, delta => new AdminConsoleSetTaxBuiMsg(Math.Clamp(_state!.SalesTax + delta, 0f, 50f))));
        taxes.AddChild(Rule());
        taxes.AddChild(TaxRow("rumc-console-tax-income", "rumc-console-tax-income-hint", state => state.IncomeTax, delta => new AdminConsoleSetIncomeTaxBuiMsg(Math.Clamp(_state!.IncomeTax + delta, 0f, 50f))));
        taxes.AddChild(Rule());
        taxes.AddChild(TaxRow("rumc-console-tax-trade", "rumc-console-tax-trade-hint", state => state.TradeTax, delta => new AdminConsoleSetTradeTaxBuiMsg(Math.Clamp(_state!.TradeTax + delta, 0f, 50f))));
        _page.AddChild(Card(taxes));

        var duty = Column(4);
        duty.AddChild(Heading(Loc.GetString("rumc-console-duty-title")));
        duty.AddChild(Live(state => Loc.GetString("rumc-console-duty", ("percent", state.DutyPercent))));
        duty.AddChild(Muted(Loc.GetString("rumc-console-duty-hint")));
        duty.AddChild(Live(state => state.InspectionSeconds > 0
            ? Loc.GetString("rumc-console-inspection", ("time", Time(state.InspectionSeconds)))
            : Loc.GetString("rumc-console-no-inspection"), CraftPalette.Muted));
        _page.AddChild(Card(duty));

        var factions = Column(3);
        factions.AddChild(Heading(Loc.GetString("rumc-console-factions")));
        factions.AddChild(s.FactionEmbargoes.Count == 0
            ? Muted(Loc.GetString("rumc-console-no-embargoes"))
            : Text(Loc.GetString("rumc-console-embargoes", ("list", string.Join(", ", s.FactionEmbargoes))), CraftPalette.Bad));
        factions.AddChild(s.FactionPacts.Count == 0
            ? Muted(Loc.GetString("rumc-console-no-pacts"))
            : Text(Loc.GetString("rumc-console-pacts", ("list", string.Join(", ", s.FactionPacts))), CraftPalette.Good));
        _page.AddChild(Card(factions));
    }

    private Control TaxRow(string nameKey, string hintKey, Func<ColonyConsoleState, float> value, Func<float, BoundUserInterfaceMessage> change)
    {
        var row = Row(6);

        var names = Column(1);
        names.AddChild(Text(Loc.GetString(nameKey)));
        names.AddChild(Muted(Loc.GetString(hintKey)));
        row.AddChild(names);

        foreach (var delta in new[] { -5f, -1f })
            row.AddChild(Action(delta.ToString("+0;-0"), () => Send(change(delta)), minWidth: 34));

        row.AddChild(Live(state => $"{value(state):0}%", CraftPalette.Gold));

        foreach (var delta in new[] { 1f, 5f })
            row.AddChild(Action(delta.ToString("+0;-0"), () => Send(change(delta)), minWidth: 34));

        return row;
    }

    // ---------------------------------------------------------------- the world market

    private void BuildMarket(ColonyConsoleState s)
    {
        var grid = new GridContainer { Columns = 2, HorizontalExpand = true, HSeparationOverride = 6, VSeparationOverride = 6 };
        foreach (var index in s.Indices)
            grid.AddChild(IndexCard(index));

        _page.AddChild(grid);

        var companies = Column(3);
        companies.AddChild(Heading(Loc.GetString("rumc-console-companies")));
        companies.AddChild(Muted(Loc.GetString("rumc-console-companies-hint")));

        var header = Row(6);
        header.AddChild(Muted(Loc.GetString("rumc-console-company"), expand: true));
        header.AddChild(Fixed(Muted(Loc.GetString("rumc-console-col-pays")), 70));
        header.AddChild(Fixed(Muted(Loc.GetString("rumc-console-col-asks")), 70));
        header.AddChild(Fixed(Muted(Loc.GetString("rumc-console-col-duty")), 60));
        header.AddChild(Fixed(Muted(Loc.GetString("rumc-console-col-status")), 150));
        companies.AddChild(header);

        foreach (var company in s.Companies)
        {
            var row = Row(6);
            var name = Column(0);
            name.AddChild(Text(company.Company));
            name.AddChild(Muted(company.Categories));
            row.AddChild(name);

            var closed = company.EmbargoSeconds > 0;
            row.AddChild(Fixed(Text($"{company.SellPercent}%", closed ? CraftPalette.Bad : PercentColor(company.SellPercent, higherIsGood: true)), 70));
            row.AddChild(Fixed(Text($"{company.BuyPercent}%", PercentColor(company.BuyPercent, higherIsGood: false)), 70));
            row.AddChild(Fixed(Text($"{company.DutyPercent}%", CraftPalette.Muted), 60));

            var status = closed
                ? Live(state => EmbargoText(state, company.Company, company.Categories), CraftPalette.Bad)
                : Text(company.Agreement ?? Loc.GetString("rumc-console-trades"), company.Agreement != null ? CraftPalette.Gold : CraftPalette.Good);
            row.AddChild(Fixed(status, 150));
            companies.AddChild(row);
        }

        _page.AddChild(Card(companies));

        var news = Column(3);
        news.AddChild(Heading(Loc.GetString("rumc-console-news")));
        if (s.News.Count == 0)
            news.AddChild(Muted(Loc.GetString("rumc-console-no-news")));

        foreach (var item in s.News)
        {
            var line = Row(8);
            line.AddChild(Fixed(Muted(Loc.GetString("rumc-console-ago", ("time", Time(item.AgoSeconds)))), 64));
            line.AddChild(Paragraph(item.Text));
            news.AddChild(line);
        }

        _page.AddChild(Card(news));
    }

    private static string EmbargoText(ColonyConsoleState state, string company, string categories)
    {
        var entry = state.Companies.FirstOrDefault(c => c.Company == company && c.Categories == categories);
        return Loc.GetString("rumc-console-embargo", ("time", Time(entry?.EmbargoSeconds ?? 0)));
    }

    private static Color PercentColor(int percent, bool higherIsGood)
    {
        if (percent >= 108)
            return higherIsGood ? CraftPalette.Good : CraftPalette.Bad;

        if (percent <= 92)
            return higherIsGood ? CraftPalette.Bad : CraftPalette.Good;

        return CraftPalette.Text;
    }

    private static Control Fixed(Control control, float width)
    {
        control.MinWidth = width;
        control.HorizontalExpand = false;
        return control;
    }

    // A paragraph: it wraps its lines at the width it is given, which a plain label does not.
    private static Control Paragraph(string text, Color? color = null)
    {
        var rich = new RichTextLabel { HorizontalExpand = true };
        rich.SetMessage(FormattedMessage.FromUnformatted(text), null, color ?? CraftPalette.Text);
        return rich;
    }

    private Control IndexCard(IndexEntry index)
    {
        var content = Column(2);

        var top = Row(6);
        top.AddChild(Text(index.Name, index.Color, expand: true));
        top.AddChild(Text($"{index.Value:0}", CraftPalette.Text));

        var arrow = index.Change > 0.05f ? "▲" : index.Change < -0.05f ? "▼" : "•";
        var color = index.Change > 0.05f ? CraftPalette.Bad : index.Change < -0.05f ? CraftPalette.Good : CraftPalette.Muted;
        top.AddChild(Text($"{arrow} {Math.Abs(index.Change):0.0}", color));
        content.AddChild(top);

        content.AddChild(new SparkLine(index.History, index.Color));
        return Card(content, index.Color.WithAlpha(0.6f), margin: 6);
    }

    // ---------------------------------------------------------------- the agreements

    private void BuildAgreements(ColonyConsoleState s)
    {
        _page.AddChild(Muted(Loc.GetString("rumc-console-agreements-hint")));

        foreach (var agreement in s.Agreements)
        {
            var id = agreement.Id;
            var content = Column(3);

            var top = Row(8);
            top.AddChild(Text(agreement.Name, agreement.Active ? CraftPalette.Gold : CraftPalette.Text, expand: true));
            top.AddChild(Muted(agreement.Company));
            content.AddChild(top);

            content.AddChild(Paragraph(agreement.Description, CraftPalette.Muted));
            content.AddChild(Text(Loc.GetString("rumc-console-agreement-effects", ("duty", agreement.DutyCut), ("bonus", agreement.SellBonus), ("time", Time(agreement.DurationSeconds))), CraftPalette.Good));

            var bottom = Row(8);
            if (agreement.Active)
            {
                bottom.AddChild(Live(state => Loc.GetString("rumc-console-agreement-left", ("time", Time(state.Agreements.FirstOrDefault(a => a.Id == id)?.LeftSeconds ?? 0))), CraftPalette.Gold, expand: true));
                bottom.AddChild(Action(Loc.GetString("rumc-console-break"), () => Send(new BreakAgreementMessage(id))));
            }
            else
            {
                bottom.AddChild(Text(Loc.GetString("rumc-console-cost", ("cost", Money(agreement.Cost))), agreement.CanAfford ? CraftPalette.Text : CraftPalette.Bad, expand: true));
                bottom.AddChild(Action(Loc.GetString("rumc-console-sign"), () => Send(new SignAgreementMessage(id)), agreement.CanAfford));
            }

            content.AddChild(bottom);
            _page.AddChild(Card(content, agreement.Active ? CraftPalette.Gold : null));
        }
    }

    // ---------------------------------------------------------------- the quotas of the mother company

    private void BuildQuotas(ColonyConsoleState s)
    {
        var status = Column(3);
        status.AddChild(Heading(Loc.GetString("rumc-console-metropolis")));
        status.AddChild(Live(state => Loc.GetString("rumc-console-standing", ("value", state.Standing)), CraftPalette.Text));
        status.AddChild(Muted(Loc.GetString("rumc-console-standing-hint")));
        status.AddChild(Live(state => state.InspectionSeconds > 0
            ? Loc.GetString("rumc-console-inspection", ("time", Time(state.InspectionSeconds)))
            : Loc.GetString("rumc-console-no-inspection"), CraftPalette.Muted));
        _page.AddChild(Card(status));

        if (s.Quotas.Count == 0)
            _page.AddChild(Card(Muted(Loc.GetString("rumc-console-no-quotas"))));

        foreach (var quota in s.Quotas)
        {
            var id = quota.Id;
            var content = Column(3);
            content.AddChild(Paragraph(quota.Text));

            var bar = LiveBar(state =>
            {
                var q = state.Quotas.FirstOrDefault(x => x.Id == id);
                return q == null ? (0f, CraftPalette.Muted) : ((float) q.Progress / Math.Max(1, q.Amount), CraftPalette.Gold);
            }, 12);
            content.AddChild(bar);

            var bottom = Row(8);
            bottom.AddChild(Live(state =>
            {
                var q = state.Quotas.FirstOrDefault(x => x.Id == id);
                return q == null ? string.Empty : Loc.GetString("rumc-console-quota-progress", ("done", q.Progress), ("total", q.Amount));
            }, CraftPalette.Text, expand: true));
            bottom.AddChild(Live(state =>
            {
                var q = state.Quotas.FirstOrDefault(x => x.Id == id);
                return q == null ? string.Empty : Loc.GetString("rumc-console-quota-left", ("time", Time(q.LeftSeconds)));
            }, CraftPalette.Muted));
            bottom.AddChild(Text(Loc.GetString("rumc-console-quota-reward", ("reward", Money(quota.Reward)), ("fine", Money(quota.Fine))), CraftPalette.Gold));
            content.AddChild(bottom);

            _page.AddChild(Card(content));
        }

        if (s.QuotaLog.Count > 0)
        {
            var log = Column(2);
            log.AddChild(Heading(Loc.GetString("rumc-console-quota-log")));
            foreach (var record in s.QuotaLog)
            {
                var row = Row(8);
                row.AddChild(Text(record.Done ? "✔" : "✘", record.Done ? CraftPalette.Good : CraftPalette.Bad));
                row.AddChild(Paragraph(record.Text, CraftPalette.Muted));
                log.AddChild(row);
            }

            _page.AddChild(Card(log));
        }
    }

    // ---------------------------------------------------------------- the voyages

    private void BuildVoyages(ColonyConsoleState s)
    {
        var content = Column(5);
        content.AddChild(Heading(Loc.GetString("rumc-console-voyages")));
        content.AddChild(Paragraph(Loc.GetString("rumc-console-voyages-hint"), CraftPalette.Muted));

        content.AddChild(Live(state => Time(state.Voyage.NextSeconds), CraftPalette.Gold));

        content.AddChild(LiveBar(state =>
        {
            var interval = Math.Max(1, state.Voyage.IntervalSeconds);
            return (1f - Math.Clamp((float) state.Voyage.NextSeconds / interval, 0f, 1f), CraftPalette.Gold);
        }, 14));

        content.AddChild(Live(state => state.Voyage.DelayedSeconds > 0
            ? Loc.GetString("rumc-console-voyage-delayed", ("time", Time(state.Voyage.DelayedSeconds)))
            : string.Empty, CraftPalette.Bad));

        var urgent = Row(8);
        urgent.AddChild(Live(state => state.Voyage.UrgentCooldownSeconds > 0
            ? Loc.GetString("rumc-console-urgent-cooldown", ("time", Time(state.Voyage.UrgentCooldownSeconds)))
            : Loc.GetString("rumc-console-urgent-cost", ("cost", Money(state.Voyage.UrgentCost))), CraftPalette.Text, expand: true));
        urgent.AddChild(Action(Loc.GetString("rumc-console-urgent"), () => Send(new UrgentVoyageMessage()), s.Voyage.CanUrgent));
        content.AddChild(urgent);

        _page.AddChild(Card(content));
    }

    // ---------------------------------------------------------------- the black market

    private void BuildUnderworld(ColonyConsoleState s)
    {
        _page.AddChild(Paragraph(Loc.GetString("rumc-console-underworld-hint"), CraftPalette.Muted));

        foreach (var org in s.Orgs)
        {
            var orgId = org.Id;
            var content = Column(4);

            content.AddChild(Text(org.Name, org.Color));
            content.AddChild(Paragraph(org.Description, CraftPalette.Muted));

            var bars = Row(10);
            var standing = Column(1);
            standing.AddChild(Live(state => Loc.GetString("rumc-console-org-standing", ("value", state.Orgs.FirstOrDefault(o => o.Id == orgId)?.Standing ?? 0)), CraftPalette.Muted));
            standing.AddChild(LiveBar(state => ((state.Orgs.FirstOrDefault(o => o.Id == orgId)?.Standing ?? 0) / 100f, CraftPalette.Good)));
            var heat = Column(1);
            heat.AddChild(Live(state => Loc.GetString("rumc-console-org-heat", ("value", state.Orgs.FirstOrDefault(o => o.Id == orgId)?.Heat ?? 0)), CraftPalette.Muted));
            heat.AddChild(LiveBar(state =>
            {
                var value = (state.Orgs.FirstOrDefault(o => o.Id == orgId)?.Heat ?? 0) / 100f;
                return (value, value > 0.7f ? CraftPalette.Bad : CraftPalette.Gold);
            }));
            bars.AddChild(standing);
            bars.AddChild(heat);
            content.AddChild(bars);

            content.AddChild(Rule());
            content.AddChild(Gold(Loc.GetString("rumc-console-wares")));
            foreach (var ware in org.Wares)
            {
                var wareId = ware.Id;
                var row = Row(8);
                var name = Column(0);
                name.AddChild(Text(ware.Name));
                name.AddChild(Muted(ware.Incoming > 0
                    ? Loc.GetString("rumc-console-ware-incoming", ("count", ware.Incoming), ("heat", ware.Heat))
                    : Loc.GetString("rumc-console-ware-heat", ("heat", ware.Heat))));
                row.AddChild(name);
                row.AddChild(Text(Money(ware.Price), s.Budget >= ware.Price ? CraftPalette.Text : CraftPalette.Bad));
                row.AddChild(Action(Loc.GetString("rumc-console-buy"), () => Send(new BuyWareMessage(orgId, wareId)), s.Budget >= ware.Price));
                name.HorizontalExpand = true;
                content.AddChild(row);
            }

            content.AddChild(Rule());
            content.AddChild(Gold(Loc.GetString("rumc-console-orders")));
            foreach (var order in org.Orders)
            {
                var orderId = order.Id;
                var row = Column(2);

                var top = Row(8);
                top.AddChild(Paragraph(order.Text));
                top.AddChild(Text(Loc.GetString("rumc-console-order-pays", ("price", order.PricePerUnit), ("bonus", Money(order.Bonus))), CraftPalette.Gold));
                if (!order.Taken)
                    top.AddChild(Action(Loc.GetString("rumc-console-take"), () => Send(new TakeOrderMessage(orgId, orderId))));

                row.AddChild(top);

                if (order.Taken)
                {
                    row.AddChild(LiveBar(state =>
                    {
                        var current = state.Orgs.FirstOrDefault(o => o.Id == orgId)?.Orders.FirstOrDefault(o => o.Id == orderId);
                        return current == null ? (0f, CraftPalette.Muted) : ((float) current.Progress / Math.Max(1, current.Amount), org.Color);
                    }));
                    row.AddChild(Live(state =>
                    {
                        var current = state.Orgs.FirstOrDefault(o => o.Id == orgId)?.Orders.FirstOrDefault(o => o.Id == orderId);
                        return current == null ? string.Empty : Loc.GetString("rumc-console-order-progress", ("done", current.Progress), ("total", current.Amount), ("time", Time(current.LeftSeconds)));
                    }, CraftPalette.Muted));
                }

                content.AddChild(row);
            }

            _page.AddChild(Card(content, org.Color.WithAlpha(0.6f)));
        }
    }

    // ---------------------------------------------------------------- the services

    private void BuildServices(ColonyConsoleState s)
    {
        _page.AddChild(Muted(Loc.GetString("rumc-console-services-hint")));

        if (s.Parties.Count == 0)
            _page.AddChild(Card(Muted(Loc.GetString("rumc-console-no-services"))));

        foreach (var party in s.Parties)
        {
            var id = party.Id;
            var row = Row(8);
            row.AddChild(Text(party.Name, party.Called ? CraftPalette.Muted : CraftPalette.Text, expand: true));
            row.AddChild(Text(Money(party.Cost), s.Budget >= party.Cost ? CraftPalette.Text : CraftPalette.Bad));
            row.AddChild(Action(party.Called ? Loc.GetString("rumc-console-called") : Loc.GetString("rumc-console-call"), () => Send(new AdminConsoleCallThirdPartyBuiMsg(id)), !party.Called && s.Budget >= party.Cost));
            _page.AddChild(Card(row, margin: 6));
        }
    }

    #endregion

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            Close();
            args.Handle();
            return;
        }

        base.KeyBindDown(args);
    }
}
