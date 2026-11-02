// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.WorldMarket;
using Content.Shared.CMU14.Government;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using static Content.Client.CMU14.WorldMarket.TermKit;

namespace Content.Client.CMU14.Government;

// The window of the console of the people, for the townsfolk (the players): four tabs. The appeals (the board that the mayor reads and answers); the vote that goes now; the orders of the city, which a player
// fills by bringing the thing in his hand; and the notary (a deal between two players, under the seal of the city). The page is made again only when what it is made of changes, and the numbers that tick are
// changed in place, so that a button is not taken from under the cursor.
public sealed partial class CitizenConsoleWindow : BaseWindow
{
    private const float WindowWidth = 900f;
    private const float SidebarWidth = 190f;
    private const float ContentWidth = WindowWidth - SidebarWidth - 80f;

    // The sections of the menu at the side.
    private static readonly (string Header, Tab[] Tabs)[] NavGroups =
    {
        ("rumc-nav-contact", new[] { Tab.Appeals, Tab.Poll }),
        ("rumc-nav-work", new[] { Tab.Orders, Tab.World, Tab.Auctions, Tab.Loans, Tab.Subsidies }),
        ("rumc-nav-deals", new[] { Tab.Trade, Tab.Notary }),
    };

    private enum Tab : byte
    {
        Appeals,
        Poll,
        Orders,
        Notary,
        Auctions,
        Loans,
        Subsidies,
        Trade,
        World,
    }

    private readonly Dictionary<Tab, TermButton> _tabs = new();
    private readonly BoxContainer _page = Column(6);
    private readonly ScrollContainer _scroll;
    private readonly List<Action<CitizenConsoleState>> _live = new();

    private CitizenConsoleState? _state;
    private Tab _tab = Tab.Appeals;
    private string _signature = string.Empty;

    // What is typed or picked and not yet sent: it is held here, so that a page made again does not take it away.
    private string _appealDraft = string.Empty;
    private string? _kind;
    private NetEntity? _partner;
    private string _sumDraft = string.Empty;
    private string _subjectDraft = string.Empty;
    private int _plot;

    public event Action<string>? OnSubmitAppeal;
    public event Action<int, int>? OnVote;
    public event Action<int>? OnDeliver;
    public event Action<string, NetEntity, int, string, int>? OnPropose;
    public event Action<int>? OnSign;
    public event Action<int>? OnCancel;
    public event Action<int, int>? OnBidAuction;
    public event Action<int, int>? OnBidTender;
    public event Action<int>? OnAcceptLoan;
    public event Action<int>? OnDeclineLoan;
    public event Action<int>? OnRepayLoan;
    public event Action<string, SubsidyForm>? OnApplySubsidy;
    public event Action<int>? OnWithdrawApplication;
    public event Action<NetEntity, int>? OnOfferSale;
    public event Action<int>? OnAcceptSale;
    public event Action<int>? OnDeclineSale;
    public event Action<int, int>? OnStartItemAuction;
    public event Action<int>? OnCancelItemAuction;
    public event Action<int, int>? OnBidItemAuction;
    public event Action<string, int>? OnOrderWorld;
    public event Action<int>? OnCancelWorld;

    public CitizenConsoleWindow()
    {
        Resizable = false;
        MinSize = new Vector2(WindowWidth, 0);
        MouseFilter = MouseFilterMode.Stop;

        var close = new TermButton { Text = "✕", MinWidth = 28 };
        close.OnPressed += _ => Close();

        var header = Row(10);
        header.AddChild(Gold(Loc.GetString("rumc-citizen-title"), expand: true));
        header.AddChild(close);

        var sidebar = Column(10);
        foreach (var (headerKey, tabsOfGroup) in NavGroups)
        {
            var box = Column(3);
            box.AddChild(Muted(Loc.GetString(headerKey)));
            foreach (var tab in tabsOfGroup)
            {
                var button = new TermButton { Text = Loc.GetString("rumc-citizen-tab-" + tab.ToString().ToLowerInvariant()), MinWidth = (int) SidebarWidth - 40 };
                var picked = tab;
                button.OnPressed += _ => Pick(picked);
                _tabs[tab] = button;
                box.AddChild(button);
            }

            sidebar.AddChild(box);
        }

        _scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, MinSize = new Vector2(0, 560), HorizontalExpand = true };
        _scroll.AddChild(_page);

        var menu = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, MinSize = new Vector2(SidebarWidth, 0) };
        menu.AddChild(Card(sidebar));

        var body = Row(10);
        body.AddChild(menu);
        body.AddChild(_scroll);

        var root = Column(6);
        root.Margin = new Thickness(8, 6, 8, 8);
        root.AddChild(header);
        root.AddChild(Muted(Loc.GetString("rumc-citizen-subtitle")));
        root.AddChild(Rule());
        root.AddChild(body);

        AddChild(new TermPanel(scanlines: true) { MouseFilter = MouseFilterMode.Pass, Children = { root } });
        UpdateTabs();
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos) => relativeMousePos.Y < 34 ? DragMode.Move : DragMode.None;

    private void Pick(Tab tab)
    {
        if (_tab == tab)
            return;

        _tab = tab;
        Rebuild();
        UpdateTabs();
    }

    private void UpdateTabs()
    {
        foreach (var (tab, button) in _tabs)
            button.Pressed = tab == _tab;
    }

    private void Rebuild()
    {
        _signature = string.Empty;
        if (_state != null)
            SetState(_state);
    }

    // The body and the name of the one who looks: what is his in the state (his votes, his plots, the offers made to him) is told by these.
    private (NetEntity? entity, string name) Me()
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        if (IoCManager.Resolve<IPlayerManager>().LocalEntity is not { } local)
            return (null, string.Empty);

        return (entMan.GetNetEntity(local), entMan.GetComponent<MetaDataComponent>(local).EntityName);
    }

    public void SetState(CitizenConsoleState state)
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

        switch (_tab)
        {
            case Tab.Appeals:
                BuildAppeals(state);
                break;
            case Tab.Poll:
                BuildPoll(state);
                break;
            case Tab.Orders:
                BuildOrders(state);
                break;
            case Tab.Auctions:
                BuildAuctions(state);
                break;
            case Tab.Loans:
                BuildLoans(state);
                break;
            case Tab.Subsidies:
                BuildSubsidies(state);
                break;
            case Tab.Trade:
                BuildTrade(state);
                break;
            case Tab.World:
                BuildWorld(state);
                break;
            default:
                BuildNotary(state);
                break;
        }

        _scroll.SetScrollValue(scroll);
    }

    private string Signature(CitizenConsoleState state)
    {
        switch (_tab)
        {
            case Tab.Appeals:
                return "a|" + string.Join(";", state.Appeals.Select(a => $"{a.Id}:{a.Status}"));

            case Tab.Poll:
                return "p|" + (state.Poll?.Id ?? 0) + ":" + (state.Poll?.Voted.Count ?? 0) + "|" + string.Join(";", state.PastPolls.Select(p => p.Id));

            case Tab.Orders:
                return "o|" + string.Join(";", state.Orders.Select(o => $"{o.Id}:{o.Left}"));

            case Tab.Auctions:
                return "u|" + string.Join(";", state.Admin.Auctions.Select(a => a.Id)) + "|" + string.Join(";", state.Admin.Tenders.Select(t => t.Id));

            case Tab.Loans:
                return "l|" + string.Join(";", state.Admin.Offers.Select(o => o.Id)) + "|" + string.Join(";", state.Admin.Loans.Select(l => $"{l.Id}:{l.Left}:{l.Missed}"));

            case Tab.Subsidies:
                return "y|" + string.Join(";", state.Subsidy.Applications.Select(a => a.Id)) + "|" + string.Join(";", state.Subsidy.Active.Select(s => $"{s.Id}:{s.Paid}:{s.Stage}"));

            case Tab.Trade:
                return "t|" + string.Join(";", state.Trade.Offers.Select(o => o.Id)) + "|" + string.Join(";", state.Trade.Auctions.Select(a => a.Id))
                       + "|" + string.Join(";", state.Nearby.Select(n => n.Entity)) + "|" + _tradeBuyer + ":" + _auctionMinutes;

            case Tab.World:
                return "w|" + state.World.Phase + ":" + state.World.City + ":" + _worldLots + "|" + string.Join(";", state.World.Goods.Select(g => $"{g.Id}:{g.BuyPrice}")) + "|" + string.Join(";", state.World.Orders.Select(o => o.Id));

            default:
                return "n|" + _kind + ":" + _partner + ":" + _plot
                       + "|" + string.Join(";", state.Nearby.Select(n => n.Entity))
                       + "|" + string.Join(";", state.Plots.Select(p => $"{p.Id}:{p.Holder}"))
                       + "|" + string.Join(";", state.Proposals.Select(p => p.Id))
                       + "|" + state.Registry.Count;
        }
    }

    // ---------------------------------------------------------------- helpers

    private static Control Wrapped(string text, Color color)
    {
        var label = new RichTextLabel { MaxWidth = ContentWidth, HorizontalExpand = true };
        label.SetMessage(FormattedMessage.FromUnformatted(text), null, color);
        return label;
    }

    private Label Live(Func<CitizenConsoleState, string> text, Color? color = null)
    {
        var label = Text(_state == null ? string.Empty : text(_state), color);
        _live.Add(state => label.Text = text(state));
        return label;
    }

    private static LineEdit Edit(string text, string placeholder, int max, Action<string> changed)
    {
        var edit = new LineEdit { HorizontalExpand = true, PlaceHolder = placeholder, Text = text, IsValid = value => value.Length <= max };
        edit.OnTextChanged += args => changed(args.Text);
        return edit;
    }

    // ---------------------------------------------------------------- the appeals

    private void BuildAppeals(CitizenConsoleState state)
    {
        var write = Column(6);
        write.AddChild(Gold(Loc.GetString("rumc-citizen-appeal-write")));
        write.AddChild(Wrapped(Loc.GetString("rumc-citizen-appeal-hint"), TermPalette.Muted));
        write.AddChild(Edit(_appealDraft, Loc.GetString("rumc-citizen-appeal-placeholder", ("max", state.MaxAppealLength)), state.MaxAppealLength, text => _appealDraft = text));
        write.AddChild(Action(Loc.GetString("rumc-citizen-appeal-send"), () =>
        {
            OnSubmitAppeal?.Invoke(_appealDraft);
            _appealDraft = string.Empty;
            Rebuild();
        }, true, 170));
        _page.AddChild(Card(write));

        var board = Column(6);
        board.AddChild(Gold(Loc.GetString("rumc-citizen-appeal-board")));
        if (state.Appeals.Count == 0)
            board.AddChild(Muted(Loc.GetString("rumc-citizen-appeal-none")));

        foreach (var appeal in state.Appeals)
        {
            board.AddChild(Rule());
            board.AddChild(Text(appeal.Author, TermPalette.Gold));
            board.AddChild(Wrapped(appeal.Text, TermPalette.Text));
            if (appeal.Status == AppealStatus.Open)
            {
                board.AddChild(Muted(Loc.GetString("rumc-citizen-appeal-open")));
                continue;
            }

            board.AddChild(Text(Loc.GetString(appeal.Status == AppealStatus.Accepted ? "rumc-appeal-status-accepted" : "rumc-appeal-status-rejected"),
                appeal.Status == AppealStatus.Accepted ? TermPalette.Good : TermPalette.Bad));
            if (appeal.Reply.Length > 0)
                board.AddChild(Muted(Loc.GetString("rumc-appeal-reply", ("reply", appeal.Reply))));
        }

        _page.AddChild(Card(board));
    }

    // ---------------------------------------------------------------- the vote

    private void BuildPoll(CitizenConsoleState state)
    {
        var (_, myName) = Me();
        var poll = Column(6);
        poll.AddChild(Gold(Loc.GetString("rumc-poll-title")));

        if (state.Poll is { } active)
        {
            var voted = active.Voted.Contains(myName);
            poll.AddChild(Text(active.Question, TermPalette.Text));
            for (var i = 0; i < active.Options.Count; i++)
            {
                var index = i;
                var pollId = active.Id;
                var row = Row(8);
                row.AddChild(Action(active.Options[i], () => OnVote?.Invoke(pollId, index), !voted, 300));
                row.AddChild(Live(s => Loc.GetString("rumc-citizen-poll-votes", ("votes", s.Poll?.Counts.ElementAtOrDefault(index) ?? 0)), TermPalette.Text));
                poll.AddChild(row);
            }

            poll.AddChild(Live(s => Loc.GetString("rumc-poll-left", ("time", Time(s.Poll?.LeftSeconds ?? 0)), ("voted", s.Poll?.Voted.Count ?? 0)), TermPalette.Muted));
            if (voted)
                poll.AddChild(Text(Loc.GetString("rumc-citizen-poll-voted"), TermPalette.Good));
        }
        else
        {
            poll.AddChild(Muted(Loc.GetString("rumc-citizen-poll-none")));
        }

        _page.AddChild(Card(poll));

        if (state.PastPolls.Count > 0)
        {
            var past = Column(4);
            past.AddChild(Gold(Loc.GetString("rumc-poll-past")));
            foreach (var old in state.PastPolls)
            {
                past.AddChild(Rule());
                past.AddChild(Text(old.Question, TermPalette.Text));
                for (var i = 0; i < old.Options.Count; i++)
                    past.AddChild(Muted(Loc.GetString("rumc-poll-option-line", ("option", old.Options[i]), ("votes", old.Counts[i]))));
            }

            _page.AddChild(Card(past));
        }
    }

    // ---------------------------------------------------------------- the orders

    private void BuildOrders(CitizenConsoleState state)
    {
        var head = Column(6);
        head.AddChild(Gold(Loc.GetString("rumc-citizen-orders-title")));
        head.AddChild(Wrapped(Loc.GetString("rumc-citizen-orders-hint"), TermPalette.Muted));
        _page.AddChild(Card(head));

        if (state.Orders.Count == 0)
        {
            _page.AddChild(Card(Muted(Loc.GetString("rumc-citizen-orders-none"))));
            return;
        }

        foreach (var order in state.Orders)
        {
            var id = order.Id;
            var column = Column(4);
            column.AddChild(Text(order.Kind, TermPalette.Text));
            column.AddChild(Wrapped(order.Description, TermPalette.Muted));
            column.AddChild(Text(Loc.GetString("rumc-citizen-order-line", ("left", order.Left), ("total", order.Total), ("price", Money(order.Price))), TermPalette.Gold));
            column.AddChild(Live(s => Loc.GetString("rumc-order-time", ("time", Time(s.Orders.FirstOrDefault(o => o.Id == id)?.LeftSeconds ?? 0))), TermPalette.Muted));
            column.AddChild(Action(Loc.GetString("rumc-citizen-order-deliver"), () => OnDeliver?.Invoke(id), true, 220));
            _page.AddChild(Card(column));
        }
    }

    // ---------------------------------------------------------------- the notary

    private void BuildNotary(CitizenConsoleState state)
    {
        var (me, myName) = Me();

        // The offers: those made to me are signed or turned down, those that I made wait or are taken back.
        var offers = Column(6);
        offers.AddChild(Gold(Loc.GetString("rumc-citizen-notary-offers")));
        var mine = state.Proposals.Where(p => p.First == me || p.Second == me).ToList();
        if (mine.Count == 0)
            offers.AddChild(Muted(Loc.GetString("rumc-citizen-notary-offers-none")));

        foreach (var offer in mine)
        {
            var id = offer.Id;
            offers.AddChild(Rule());
            offers.AddChild(Text(Loc.GetString("rumc-citizen-notary-offer-line", ("kind", offer.Kind), ("first", offer.FirstName), ("second", offer.SecondName)), TermPalette.Text));
            offers.AddChild(Muted(Loc.GetString("rumc-citizen-notary-offer-terms", ("sum", Money(offer.Sum)), ("subject", offer.Subject))));
            if (offer.Plot.Length > 0)
                offers.AddChild(Muted(Loc.GetString("rumc-citizen-notary-offer-plot", ("plot", offer.Plot))));

            var buttons = Row(8);
            if (offer.Second == me)
                buttons.AddChild(Action(Loc.GetString("rumc-citizen-notary-sign"), () => OnSign?.Invoke(id), true, 150));

            buttons.AddChild(Danger(Loc.GetString(offer.Second == me ? "rumc-citizen-notary-decline" : "rumc-citizen-notary-withdraw"), () => OnCancel?.Invoke(id), true, 150));
            offers.AddChild(buttons);
        }

        _page.AddChild(Card(offers));

        // A new offer: the kind, the other party (who stands by the console), the sum, what it is about, and, for a sale, which plot.
        var form = Column(6);
        form.AddChild(Gold(Loc.GetString("rumc-citizen-notary-new")));
        form.AddChild(Wrapped(Loc.GetString("rumc-citizen-notary-hint"), TermPalette.Muted));

        var kind = state.Kinds.FirstOrDefault(k => k.Id == _kind);
        foreach (var k in state.Kinds)
        {
            var id = k.Id;
            var button = new TermButton { Text = Loc.GetString("rumc-citizen-notary-kind", ("name", k.Name), ("fee", Money(k.Fee))), MinWidth = 300, Pressed = _kind == id };
            button.OnPressed += _ =>
            {
                _kind = _kind == id ? null : id;
                _plot = 0;
                Rebuild();
            };
            form.AddChild(button);
            if (_kind == id)
                form.AddChild(Wrapped(k.Description, TermPalette.Muted));
        }

        form.AddChild(Rule());
        form.AddChild(Text(Loc.GetString("rumc-citizen-notary-partner"), TermPalette.Text));
        var others = state.Nearby.Where(n => n.Entity != me).ToList();
        if (others.Count == 0)
            form.AddChild(Muted(Loc.GetString("rumc-citizen-notary-nobody")));

        foreach (var person in others)
        {
            var entity = person.Entity;
            var button = new TermButton { Text = person.Name, MinWidth = 220, Pressed = _partner == entity };
            button.OnPressed += _ =>
            {
                _partner = _partner == entity ? null : entity;
                Rebuild();
            };
            form.AddChild(button);
        }

        form.AddChild(Rule());
        form.AddChild(Text(Loc.GetString("rumc-citizen-notary-sum"), TermPalette.Text));
        form.AddChild(Edit(_sumDraft, "0", 6, text => _sumDraft = new string(text.Where(char.IsDigit).ToArray())));
        form.AddChild(Text(Loc.GetString("rumc-citizen-notary-subject"), TermPalette.Text));
        form.AddChild(Edit(_subjectDraft, Loc.GetString("rumc-citizen-notary-subject-placeholder"), state.MaxSubjectLength, text => _subjectDraft = text));

        var needPlot = kind?.Mode == ContractMode.PlotSale;
        if (needPlot)
        {
            form.AddChild(Text(Loc.GetString("rumc-citizen-notary-plot"), TermPalette.Text));
            var myPlots = state.Plots.Where(p => p.Holder == myName).ToList();
            if (myPlots.Count == 0)
                form.AddChild(Muted(Loc.GetString("rumc-citizen-notary-no-plots")));

            foreach (var plot in myPlots)
            {
                var id = plot.Id;
                var button = new TermButton { Text = plot.Label, MinWidth = 300, Pressed = _plot == id };
                button.OnPressed += _ =>
                {
                    _plot = _plot == id ? 0 : id;
                    Rebuild();
                };
                form.AddChild(button);
            }
        }

        var ready = kind != null && _partner != null && others.Any(o => o.Entity == _partner) && _subjectDraft.Trim().Length >= 3 && (!needPlot || _plot != 0);
        form.AddChild(Action(Loc.GetString("rumc-citizen-notary-offer"), () =>
        {
            if (_kind is { } kindId && _partner is { } partner)
                OnPropose?.Invoke(kindId, partner, int.TryParse(_sumDraft, out var sum) ? sum : 0, _subjectDraft, _plot);
        }, ready, 200));
        _page.AddChild(Card(form));

        var register = Column(4);
        register.AddChild(Gold(Loc.GetString("rumc-citizen-notary-register")));
        if (state.Registry.Count == 0)
            register.AddChild(Muted(Loc.GetString("rumc-notary-register-none")));

        foreach (var contract in state.Registry)
        {
            register.AddChild(Rule());
            register.AddChild(Text(Loc.GetString("rumc-notary-register-line", ("number", contract.Number), ("kind", contract.Kind), ("first", contract.First), ("second", contract.Second)), TermPalette.Text));
            register.AddChild(Muted(Loc.GetString("rumc-notary-register-terms", ("sum", Money(contract.Sum)), ("fee", Money(contract.Fee)), ("subject", contract.Subject))));
        }

        _page.AddChild(Card(register));
    }
}
