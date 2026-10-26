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

// The window of the city relations, which the console of the administration opens (the administrator is the mayor of one city): six tabs. The factions (the petitions and the demands of the
// ambassadors, the agreements with the factions); the other cities of the map (gifts, help in troubles, agreements); the credit (the lenders, the loans, the investments); the licences (given
// to the people who stand by the console, and the cards of them); the land (the zones, the plots, the rent and the tax); and the symbols (the flag, the arms, the motto, the holidays; see
// ColonyGovernmentWindow.Land.cs). The page is made again only when what it is made of changes (a thing done, a price that is now within reach); the numbers that tick (the time, the money) are
// changed in place, so that a button is not taken from under the cursor.
public sealed partial class ColonyGovernmentWindow : BaseWindow
{
    private const float WindowWidth = 980f;
    private const float SidebarWidth = 210f;
    private const float ContentWidth = WindowWidth - SidebarWidth - 80f;

    // The sections of the menu at the side: a title for each, and the tabs that stand under it.
    private static readonly (string Header, Tab[] Tabs)[] NavGroups =
    {
        ("rumc-nav-diplomacy", new[] { Tab.Factions, Tab.Cities, Tab.Projects }),
        ("rumc-nav-economy", new[] { Tab.Credit, Tab.Quotas }),
        ("rumc-nav-land", new[] { Tab.Land, Tab.Auctions }),
        ("rumc-nav-people", new[] { Tab.Appeals, Tab.Licenses, Tab.Awards, Tab.Orders, Tab.Loans, Tab.Subsidies, Tab.Notary }),
    };

    private enum Tab : byte
    {
        Factions,
        Cities,
        Credit,
        Licenses,
        Land,
        Awards,
        Orders,
        Appeals,
        Notary,
        Auctions,
        Loans,
        Projects,
        Quotas,
        Subsidies,
    }

    private readonly Label _budget;
    private readonly Label _summary;
    private readonly Dictionary<Tab, TermButton> _tabs = new();
    private readonly BoxContainer _page = Column(6);
    private readonly ScrollContainer _scroll;

    // What is changed in place when the state comes again with the same page.
    private readonly List<Action<ColonyGovernmentState>> _live = new();

    private ColonyGovernmentState? _state;
    private Tab _tab = Tab.Factions;
    private string _signature = string.Empty;

    // The person by the console to whom a licence will be given.
    private NetEntity? _target;

    public event Action<string, string>? OnPropose;
    public event Action<string, string>? OnBreak;
    public event Action<int, PetitionAnswer>? OnAnswer;
    public event Action<string, int>? OnTakeLoan;
    public event Action<int>? OnRepayLoan;
    public event Action<string>? OnInvest;
    public event Action<string>? OnGift;
    public event Action<string>? OnAid;
    public event Action<string, string>? OnSignAccord;
    public event Action<string, string>? OnBreakAccord;
    public event Action<NetEntity, string>? OnIssue;
    public event Action<string>? OnRevoke;
    public event Action<int>? OnSetTax;
    public event Action<int>? OnSetSalesTax;
    public event Action<bool>? OnSetEnforce;
    public event Action? OnIssueSurveyor;
    public event Action<int>? OnRemoveZone;
    public event Action<int>? OnAllot;
    public event Action<int>? OnDeclineRequest;
    public event Action<int>? OnRevokePlot;
    public event Action<NetEntity, string, string>? OnGrantAward;
    public event Action<string, int, int>? OnCreateOrder;
    public event Action<int>? OnCancelOrder;
    public event Action<int, bool, string>? OnAnswerAppeal;
    public event Action<string, List<string>, int>? OnStartPoll;
    public event Action? OnClosePoll;
    public event Action<ZoneKind, int, int>? OnStartAuction;
    public event Action<int>? OnCancelAuction;
    public event Action<string, int, int, int>? OnStartTender;
    public event Action<int>? OnCancelTender;
    public event Action<NetEntity, int, int, int>? OnOfferLoan;
    public event Action<int>? OnWithdrawLoanOffer;
    public event Action<int>? OnForgiveLoan;
    public event Action<string, string>? OnStartProject;
    public event Action<int>? OnCancelProject;
    public event Action<string, string>? OnRunCovert;
    public event Action<int>? OnPayQuota;
    public event Action<int>? OnApproveSubsidy;
    public event Action<int>? OnRejectSubsidy;
    public event Action<int>? OnCancelSubsidy;

    public ColonyGovernmentWindow()
    {
        Resizable = false;
        MinSize = new Vector2(WindowWidth, 0);
        MouseFilter = MouseFilterMode.Stop;

        _budget = Text(string.Empty, TermPalette.Good);
        _summary = Muted(string.Empty);

        var close = new TermButton { Text = "✕", MinWidth = 28 };
        close.OnPressed += _ => Close();

        var header = Row(10);
        header.AddChild(Gold(Loc.GetString("rumc-government-title"), expand: true));
        header.AddChild(Muted(Loc.GetString("rumc-government-budget")));
        header.AddChild(_budget);
        header.AddChild(close);

        var sidebar = Column(10);
        foreach (var (headerKey, tabsOfGroup) in NavGroups)
        {
            var box = Column(3);
            box.AddChild(Muted(Loc.GetString(headerKey)));
            foreach (var tab in tabsOfGroup)
            {
                var button = new TermButton { Text = Loc.GetString(TabKey(tab)), MinWidth = (int) SidebarWidth - 40 };
                var picked = tab;
                button.OnPressed += _ => Pick(picked);
                _tabs[tab] = button;
                box.AddChild(button);
            }

            sidebar.AddChild(box);
        }

        _scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, MinSize = new Vector2(0, 620), HorizontalExpand = true };
        _scroll.AddChild(_page);

        var menu = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, MinSize = new Vector2(SidebarWidth, 0) };
        menu.AddChild(Card(sidebar));

        var body = Row(10);
        body.AddChild(menu);
        body.AddChild(_scroll);

        var root = Column(6);
        root.Margin = new Thickness(8, 6, 8, 8);
        root.AddChild(header);
        root.AddChild(_summary);
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
        _signature = string.Empty;
        UpdateTabs();
        if (_state != null)
            SetState(_state);
    }

    private void UpdateTabs()
    {
        foreach (var (tab, button) in _tabs)
            button.Pressed = tab == _tab;
    }

    public void SetState(ColonyGovernmentState state)
    {
        _state = state;

        _budget.Text = Money(state.Budget);
        var openAppeals = state.People.Appeals.Count(a => a.Status == AppealStatus.Open);
        _tabs[Tab.Appeals].Text = Loc.GetString("rumc-government-tab-appeals") + (openAppeals > 0 ? $" ({openAppeals})" : string.Empty);
        _summary.Text = Loc.GetString("rumc-government-summary",
            ("income", Signed((int) MathF.Round(state.IncomePerMinute))),
            ("duty", state.DutyCut), ("call", state.CallDiscount));

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
            case Tab.Factions:
                BuildForeign(state);
                break;
            case Tab.Cities:
                BuildCities(state);
                break;
            case Tab.Credit:
                BuildCredit(state);
                break;
            case Tab.Land:
                BuildLand(state);
                break;
            case Tab.Awards:
                BuildAwards(state);
                break;
            case Tab.Orders:
                BuildOrders(state);
                break;
            case Tab.Appeals:
                BuildAppeals(state);
                break;
            case Tab.Notary:
                BuildNotary(state);
                break;
            case Tab.Auctions:
                BuildAuctions(state);
                break;
            case Tab.Loans:
                BuildLoans(state);
                break;
            case Tab.Projects:
                BuildProjects(state);
                break;
            case Tab.Quotas:
                BuildQuotas(state);
                break;
            case Tab.Subsidies:
                BuildSubsidies(state);
                break;
            default:
                BuildLicenses(state);
                break;
        }

        _scroll.SetScrollValue(scroll);
    }

    // What the page is made of: it is made again when this changes.
    private string Signature(ColonyGovernmentState state)
    {
        switch (_tab)
        {
            case Tab.Factions:
                return "f|" + state.TreatiesInForce + "/" + state.TreatySlots
                       + "|" + string.Join(";", state.Factions.Select(f => $"{f.Name}:{f.Attitude}"))
                       + "|" + string.Join(";", state.Petitions.Select(p => $"{p.Id}:{p.CanAfford}:{p.CanBargain}:{p.Reward}:{p.Cost}"))
                       + "|" + string.Join(";", state.Active.Select(a => $"{a.Id}:{a.Faction}"))
                       + "|" + string.Join(";", state.Treaties.Select(t => $"{t.Id}:{state.Budget >= t.MoneyCost}"));

            case Tab.Cities:
                var cities = state.Cities;
                return "c|" + string.Join(";", cities.Cities.Select(c => $"{c.Id}:{c.Relation}:{c.Crisis}"))
                       + "|" + string.Join(";", cities.Active.Select(a => $"{a.Id}:{a.City}"))
                       + $"|{state.Budget >= cities.GiftCost}:{state.Budget >= cities.AidCost}"
                       + "|" + string.Join(";", cities.Accords.Select(a => $"{a.Id}:{state.Budget >= a.MoneyCost}"));

            case Tab.Credit:
                var credit = state.Credit;
                return "k|" + credit.Rating
                       + "|" + string.Join(";", credit.Lenders.Select(l => $"{l.Id}:{l.Available}:{l.Blocked}"))
                       + "|" + string.Join(";", credit.Loans.Select(l => $"{l.Id}:{l.Left}:{l.Instalment}:{l.Missed}:{state.Budget >= l.Instalment * l.Left}"))
                       + "|" + string.Join(";", credit.Investments.Select(i => $"{i.Id}:{i.ChancePercent}:{i.Blocked}"))
                       + "|" + string.Join(";", credit.Active.Select(a => $"{a.Id}:{a.Done}:{a.Success}"));

            case Tab.Land:
                return LandSignature(state);

            case Tab.Awards:
                return AwardsSignature(state);

            case Tab.Orders:
                return OrdersSignature(state);

            case Tab.Appeals:
                return AppealsSignature(state);

            case Tab.Notary:
                return NotarySignature(state);

            case Tab.Auctions:
                return AuctionsSignature(state);

            case Tab.Loans:
                return LoansSignature(state);

            case Tab.Projects:
                return ProjectsSignature(state);

            case Tab.Quotas:
                return QuotasSignature(state);

            case Tab.Subsidies:
                return SubsidiesSignature(state);

            default:
                var licenses = state.Licenses;
                return "l|" + _target
                       + "|" + string.Join(";", licenses.Nearby.Select(n => n.Entity))
                       + "|" + string.Join(";", licenses.Issued.Select(i => $"{i.Serial}:{i.Revoked}:{i.LeftSeconds > 0}"));
        }
    }

    // ---------------------------------------------------------------- helpers

    private static string Signed(int value) => value > 0 ? $"+{value}" : value < 0 ? $"−{value * -1}" : "0";

    private static string Signed(float value) => value > 0.0001f ? "+" + value.ToString("0.#") : value < -0.0001f ? "−" + (-value).ToString("0.#") : "0";

    private static Control Wrapped(string text, Color color)
    {
        var label = new RichTextLabel { MaxWidth = ContentWidth, HorizontalExpand = true };
        label.SetMessage(FormattedMessage.FromUnformatted(text), null, color);
        return label;
    }

    // A label whose text is made again from the state each time it comes, with no page made again.
    private Label Live(Func<ColonyGovernmentState, string> text, Color? color = null)
    {
        var label = Text(_state == null ? string.Empty : text(_state), color);
        _live.Add(state => label.Text = text(state));
        return label;
    }

    private static Color AttitudeColor(int value) => value >= 15 ? TermPalette.Good : value < -10 ? TermPalette.Bad : TermPalette.Text;

    // ---------------------------------------------------------------- the factions

    private void BuildForeign(ColonyGovernmentState state)
    {
        if (state.Factions.Count == 0)
        {
            _page.AddChild(Card(Muted(Loc.GetString("rumc-government-no-factions"))));
            return;
        }

        _page.AddChild(Card(PetitionsPanel(state)));
        _page.AddChild(Card(TreatiesPanel(state)));

        foreach (var faction in state.Factions)
            _page.AddChild(Card(FactionPanel(state, faction)));
    }

    private Control PetitionsPanel(ColonyGovernmentState state)
    {
        var column = Column(6);
        column.AddChild(Gold(Loc.GetString("rumc-government-petitions")));

        if (state.Petitions.Count == 0)
            column.AddChild(Muted(Loc.GetString("rumc-government-no-petitions")));

        foreach (var petition in state.Petitions)
        {
            column.AddChild(Rule());

            var id = petition.Id;
            var head = Row(8);
            head.AddChild(Text(Loc.GetString(petition.Ultimatum ? "rumc-government-petition-ultimatum" : "rumc-government-petition-request"),
                petition.Ultimatum ? TermPalette.Bad : TermPalette.Gold));
            head.AddChild(Text(petition.Faction, TermPalette.Text, expand: true));
            head.AddChild(Live(s => Loc.GetString("rumc-government-petition-left", ("time", Time(s.Petitions.FirstOrDefault(p => p.Id == id)?.LeftSeconds ?? 0))), TermPalette.Muted));
            column.AddChild(head);

            column.AddChild(Wrapped(petition.Text, petition.Ultimatum ? TermPalette.Text : TermPalette.Muted));

            var accept = new List<string>();
            if (petition.Cost > 0)
                accept.Add("−" + Money(petition.Cost));

            if (petition.Reward > 0)
                accept.Add("+" + Money(petition.Reward));

            column.AddChild(Muted(Loc.GetString("rumc-government-petition-accept-line", ("line", accept.Count == 0 ? "—" : string.Join(", ", accept)))));

            var buttons = Row(8);
            buttons.AddChild(Action(Loc.GetString("rumc-government-accept"), () => OnAnswer?.Invoke(id, PetitionAnswer.Accept), petition.CanAfford, 110));
            buttons.AddChild(Action(Loc.GetString("rumc-government-bargain"), () => OnAnswer?.Invoke(id, PetitionAnswer.Bargain), petition.CanBargain, 120));
            buttons.AddChild(Danger(Loc.GetString("rumc-government-decline"), () => OnAnswer?.Invoke(id, PetitionAnswer.Decline), true, 100));
            column.AddChild(buttons);
        }

        return column;
    }

    private Control TreatiesPanel(ColonyGovernmentState state)
    {
        var column = Column(6);
        var head = Row(8);
        head.AddChild(Gold(Loc.GetString("rumc-government-treaties"), expand: true));
        head.AddChild(Muted(Loc.GetString("rumc-government-slots", ("used", state.TreatiesInForce), ("slots", state.TreatySlots))));
        column.AddChild(head);

        if (state.Active.Count == 0)
            column.AddChild(Muted(Loc.GetString("rumc-government-no-treaties")));

        foreach (var treaty in state.Active)
        {
            var id = treaty.Id;
            var faction = treaty.Faction;
            var row = Row(8);
            row.AddChild(Text(treaty.Name, TermPalette.Good));
            row.AddChild(Text(faction, TermPalette.Text, expand: true));
            row.AddChild(Live(s => Loc.GetString("rumc-government-treaty-left",
                ("time", Time(s.Active.FirstOrDefault(a => a.Id == id && a.Faction == faction)?.LeftSeconds ?? 0))), TermPalette.Muted));
            row.AddChild(Danger(Loc.GetString("rumc-government-break"), () => OnBreak?.Invoke(id, faction), true, 110));
            column.AddChild(row);
        }

        return column;
    }

    private Control FactionPanel(ColonyGovernmentState state, FactionEntry faction)
    {
        var column = Column(4);

        var head = Row(8);
        head.AddChild(Text(faction.Name, TermPalette.Gold, expand: true));
        head.AddChild(Text(Loc.GetString("rumc-government-attitude", ("value", Signed(faction.Attitude))), AttitudeColor(faction.Attitude)));
        column.AddChild(head);
        column.AddChild(new FillBar((faction.Attitude + 100) / 200f, faction.Attitude >= 15 ? TermPalette.Good : faction.Attitude < -10 ? TermPalette.Bad : TermPalette.Gold));

        var held = state.Active.Where(a => a.Faction == faction.Name).Select(a => a.Id).ToHashSet();
        var slotsFull = state.TreatiesInForce >= state.TreatySlots;

        foreach (var treaty in state.Treaties)
        {
            column.AddChild(Rule());

            var sanction = treaty.Kind == TreatyKind.Sanction;
            var title = Row(8);
            title.AddChild(Text(treaty.Name, sanction ? TermPalette.Bad : TermPalette.Text, expand: true));
            column.AddChild(title);

            column.AddChild(Wrapped(treaty.Description, TermPalette.Muted));
            if (treaty.Effects.Length > 0)
                column.AddChild(Text(Loc.GetString("rumc-government-effects") + " " + treaty.Effects, TermPalette.Text));

            column.AddChild(Muted(sanction
                ? Loc.GetString("rumc-government-treaty-cost-sanction", ("money", Money(treaty.MoneyCost)), ("time", Time(treaty.DurationSeconds)))
                : Loc.GetString("rumc-government-treaty-cost", ("money", Money(treaty.MoneyCost)), ("time", Time(treaty.DurationSeconds)), ("attitude", treaty.MinAttitude))));

            // Why it cannot be proposed (the server checks it again).
            string? why = null;
            if (held.Contains(treaty.Id))
                why = Loc.GetString("rumc-government-treaty-in-force");
            else if (slotsFull)
                why = Loc.GetString("rumc-government-treaty-slots-full");
            else if (!sanction && faction.Attitude < treaty.MinAttitude)
                why = Loc.GetString("rumc-government-treaty-need", ("attitude", treaty.MinAttitude));
            else if (state.Budget < treaty.MoneyCost)
                why = Loc.GetString("rumc-government-blocked-money");

            var treatyId = treaty.Id;
            var factionName = faction.Name;
            var row = Row(8);
            row.AddChild(Action(Loc.GetString("rumc-government-propose"), () => OnPropose?.Invoke(treatyId, factionName), why == null, 120));
            if (why != null)
                row.AddChild(Muted(why, expand: true));

            column.AddChild(row);
        }

        return column;
    }

    // ---------------------------------------------------------------- the other cities of the map

    private void BuildCities(ColonyGovernmentState state)
    {
        var cities = state.Cities;

        var top = Column(6);
        top.AddChild(Gold(Loc.GetString("rumc-government-cities-title")));
        top.AddChild(Muted(Loc.GetString("rumc-government-cities-hint")));

        foreach (var city in cities.Cities)
        {
            top.AddChild(Rule());

            var id = city.Id;
            var head = Row(8);
            head.AddChild(Text(city.Name, TermPalette.Gold, expand: true));
            head.AddChild(Text(Loc.GetString("rumc-government-city-relation", ("value", city.Relation)), city.Relation >= 40 ? TermPalette.Good : TermPalette.Text));
            top.AddChild(head);
            top.AddChild(new FillBar(city.Relation / 100f, city.Relation >= 40 ? TermPalette.Good : TermPalette.Gold));

            if (city.Capabilities.Length > 0)
                top.AddChild(Muted(city.Capabilities));

            var inTrouble = city.Crisis.Length > 0;
            top.AddChild(inTrouble
                ? Live(s => Loc.GetString("rumc-government-city-crisis", ("crisis", city.Crisis),
                    ("time", Time(s.Cities.Cities.FirstOrDefault(c => c.Id == id)?.CrisisLeftSeconds ?? 0))), TermPalette.Bad)
                : Muted(Loc.GetString("rumc-government-city-calm")));

            var buttons = Row(8);
            buttons.AddChild(Action(Loc.GetString("rumc-government-city-gift", ("cost", Money(cities.GiftCost))), () => OnGift?.Invoke(id), state.Budget >= cities.GiftCost, 170));
            buttons.AddChild(Action(Loc.GetString("rumc-government-city-aid", ("cost", Money(cities.AidCost))), () => OnAid?.Invoke(id), inTrouble && state.Budget >= cities.AidCost, 210));
            top.AddChild(buttons);
        }

        _page.AddChild(Card(top));

        // The agreements.
        var accords = Column(6);
        var head2 = Row(8);
        head2.AddChild(Gold(Loc.GetString("rumc-government-accords"), expand: true));
        head2.AddChild(Muted(Loc.GetString("rumc-government-accords-slots", ("used", cities.Active.Count), ("slots", cities.AccordSlots))));
        accords.AddChild(head2);

        if (cities.Active.Count == 0)
            accords.AddChild(Muted(Loc.GetString("rumc-government-no-accords")));

        foreach (var active in cities.Active)
        {
            var id = active.Id;
            var city = active.City;
            var row = Row(8);
            row.AddChild(Text(active.Name, TermPalette.Good));
            row.AddChild(Text(city, TermPalette.Text, expand: true));
            row.AddChild(Live(s => Loc.GetString("rumc-government-accord-left",
                ("time", Time(s.Cities.Active.FirstOrDefault(a => a.Id == id && a.City == city)?.LeftSeconds ?? 0))), TermPalette.Muted));
            row.AddChild(Danger(Loc.GetString("rumc-government-break"), () => OnBreakAccord?.Invoke(id, city), true, 110));
            accords.AddChild(row);
        }

        _page.AddChild(Card(accords));

        var held = cities.Active.Select(a => (a.Id, a.City)).ToHashSet();
        var slotsFull = cities.Active.Count >= cities.AccordSlots;

        foreach (var accord in cities.Accords)
        {
            var column = Column(4);
            column.AddChild(Text(accord.Name, TermPalette.Text));
            column.AddChild(Wrapped(accord.Description, TermPalette.Muted));
            if (accord.Effects.Length > 0)
                column.AddChild(Text(Loc.GetString("rumc-government-effects") + " " + accord.Effects, TermPalette.Text));

            column.AddChild(Muted(Loc.GetString("rumc-government-accord-cost", ("money", Money(accord.MoneyCost)),
                ("time", Time(accord.DurationSeconds)), ("relation", accord.MinRelation))));
            if (accord.Requires.Length > 0)
                column.AddChild(Muted(Loc.GetString("rumc-government-accord-requires", ("kind", accord.Requires))));

            foreach (var city in cities.Cities)
            {
                string? why = null;
                if (held.Contains((accord.Id, city.Id)))
                    why = Loc.GetString("rumc-government-accord-in-force");
                else if (slotsFull)
                    why = Loc.GetString("rumc-government-accord-slots-full");
                else if (city.Relation < accord.MinRelation)
                    why = Loc.GetString("rumc-government-accord-need", ("relation", accord.MinRelation));
                else if (accord.Requires.Length > 0 && !city.Capabilities.Contains(accord.Requires))
                    why = Loc.GetString("rumc-government-accord-city-lacks");
                else if (state.Budget < accord.MoneyCost)
                    why = Loc.GetString("rumc-government-blocked-money");

                var accordId = accord.Id;
                var cityId = city.Id;
                var row = Row(8);
                row.AddChild(Action(Loc.GetString("rumc-government-accord-sign") + " — " + city.Name, () => OnSignAccord?.Invoke(accordId, cityId), why == null, 260));
                if (why != null)
                    row.AddChild(Muted(why, expand: true));

                column.AddChild(row);
            }

            _page.AddChild(Card(column));
        }
    }

    // ---------------------------------------------------------------- the credit

    // Up to three sums that can be taken at once: a fourth of what can be, a half, and the whole, in steps of five hundred.
    private static IEnumerable<int> LoanSums(int available)
    {
        return new[] { available / 4 / 500 * 500, available / 2 / 500 * 500, available }.Where(s => s >= 500).Distinct();
    }

    private void BuildCredit(ColonyGovernmentState state)
    {
        var credit = state.Credit;

        var rating = Column(4);
        rating.AddChild(Gold(Loc.GetString("rumc-government-credit-rating", ("value", credit.Rating))));
        rating.AddChild(new FillBar(credit.Rating / 100f, credit.Rating >= 55 ? TermPalette.Good : credit.Rating < 30 ? TermPalette.Bad : TermPalette.Gold));
        rating.AddChild(Wrapped(Loc.GetString("rumc-government-credit-rating-hint"), TermPalette.Muted));
        _page.AddChild(Card(rating));

        // The lenders.
        var lenders = Column(6);
        lenders.AddChild(Gold(Loc.GetString("rumc-government-credit-lenders")));
        foreach (var lender in credit.Lenders)
        {
            lenders.AddChild(Rule());
            lenders.AddChild(Text(lender.Name, TermPalette.Text));
            lenders.AddChild(Wrapped(lender.Description, TermPalette.Muted));
            lenders.AddChild(Muted(Loc.GetString("rumc-government-credit-terms", ("interest", lender.InterestPercent), ("count", lender.Instalments), ("time", Time(lender.CycleSeconds)))));

            if (lender.Blocked.Length > 0)
            {
                lenders.AddChild(Text(lender.Blocked, TermPalette.Bad));
                continue;
            }

            lenders.AddChild(Muted(Loc.GetString("rumc-government-credit-available", ("amount", Money(lender.Available)))));
            var id = lender.Id;
            var row = Row(8);
            foreach (var sum in LoanSums(lender.Available))
            {
                var amount = sum;
                row.AddChild(Action(Loc.GetString("rumc-government-credit-take", ("amount", Money(amount))), () => OnTakeLoan?.Invoke(id, amount), true, 150));
            }

            lenders.AddChild(row);
        }

        _page.AddChild(Card(lenders));

        // The loans of the city.
        var loans = Column(6);
        loans.AddChild(Gold(Loc.GetString("rumc-government-credit-loans")));
        if (credit.Loans.Count == 0)
            loans.AddChild(Muted(Loc.GetString("rumc-government-credit-no-loans")));

        foreach (var loan in credit.Loans)
        {
            var id = loan.Id;
            var sum = loan.Instalment * loan.Left;
            loans.AddChild(Rule());
            loans.AddChild(Live(s =>
            {
                var l = s.Credit.Loans.FirstOrDefault(x => x.Id == id);
                return l == null ? string.Empty : Loc.GetString("rumc-government-credit-loan-line", ("lender", l.Lender), ("instalment", Money(l.Instalment)), ("left", l.Left), ("time", Time(l.NextSeconds)));
            }, TermPalette.Text));

            if (loan.Missed > 0)
                loans.AddChild(Text(Loc.GetString("rumc-government-credit-missed-line", ("count", loan.Missed)), TermPalette.Bad));

            loans.AddChild(Action(Loc.GetString("rumc-government-credit-repay", ("sum", Money(sum))), () => OnRepayLoan?.Invoke(id), state.Budget >= sum, 220));
        }

        _page.AddChild(Card(loans));

        // The investments.
        var investments = Column(6);
        investments.AddChild(Gold(Loc.GetString("rumc-government-investments")));
        investments.AddChild(Wrapped(Loc.GetString("rumc-government-investments-hint"), TermPalette.Muted));
        foreach (var investment in credit.Investments)
        {
            investments.AddChild(Rule());
            investments.AddChild(Text(investment.Name, TermPalette.Text));
            investments.AddChild(Wrapped(investment.Description, TermPalette.Muted));
            investments.AddChild(Muted(Loc.GetString("rumc-government-investment-line", ("cost", Money(investment.Cost)), ("time", Time(investment.DurationSeconds)),
                ("chance", investment.ChancePercent), ("payout", Money(investment.Payout)))));
            if (investment.Income > 0f)
                investments.AddChild(Muted(Loc.GetString("rumc-government-investment-income", ("income", (int) MathF.Round(investment.Income)))));

            var id = investment.Id;
            var row = Row(8);
            row.AddChild(Action(Loc.GetString("rumc-government-invest"), () => OnInvest?.Invoke(id), investment.Blocked.Length == 0, 110));
            if (investment.Blocked.Length > 0)
                row.AddChild(Muted(investment.Blocked, expand: true));

            investments.AddChild(row);
        }

        if (credit.Active.Count > 0)
        {
            investments.AddChild(Rule());
            investments.AddChild(Gold(Loc.GetString("rumc-government-investments-active")));
            foreach (var active in credit.Active)
            {
                var id = active.Id;
                if (active.Done)
                {
                    investments.AddChild(Text(Loc.GetString(active.Success ? "rumc-government-investment-success" : "rumc-government-investment-failure", ("name", active.Name)),
                        active.Success ? TermPalette.Good : TermPalette.Bad));
                }
                else
                {
                    investments.AddChild(Live(s => Loc.GetString("rumc-government-investment-running", ("name", active.Name),
                        ("time", Time(s.Credit.Active.FirstOrDefault(a => a.Id == id)?.LeftSeconds ?? 0))), TermPalette.Gold));
                }
            }
        }

        _page.AddChild(Card(investments));
    }

    // ---------------------------------------------------------------- the licences

    private void BuildLicenses(ColonyGovernmentState state)
    {
        var licenses = state.Licenses;

        // Who stands by the console: one of them is chosen.
        var nearby = Column(6);
        nearby.AddChild(Gold(Loc.GetString("rumc-government-licenses-title")));
        nearby.AddChild(Wrapped(Loc.GetString("rumc-government-licenses-hint"), TermPalette.Muted));
        nearby.AddChild(Rule());
        nearby.AddChild(Text(Loc.GetString("rumc-government-license-nearby"), TermPalette.Text));

        if (licenses.Nearby.Count == 0)
            nearby.AddChild(Muted(Loc.GetString("rumc-government-license-no-nearby")));

        foreach (var person in licenses.Nearby)
        {
            var entity = person.Entity;
            var button = new TermButton { Text = person.Name, MinWidth = 220, Pressed = _target == entity };
            button.OnPressed += _ =>
            {
                _target = _target == entity ? null : entity;
                _signature = string.Empty;
                if (_state != null)
                    SetState(_state);
            };
            nearby.AddChild(button);
        }

        _page.AddChild(Card(nearby));

        foreach (var type in licenses.Types)
        {
            var id = type.Id;
            var column = Column(4);
            column.AddChild(Text(type.Name, TermPalette.Text));
            column.AddChild(Wrapped(type.Description, TermPalette.Muted));
            column.AddChild(Muted(Loc.GetString("rumc-government-license-cost", ("fee", Money(type.Fee)), ("time", Time(type.DurationSeconds)))));
            column.AddChild(Action(Loc.GetString("rumc-government-license-issue"), () =>
            {
                if (_target is { } target)
                    OnIssue?.Invoke(target, id);
            }, _target != null && licenses.Nearby.Any(n => n.Entity == _target), 110));
            _page.AddChild(Card(column));
        }

        // What was given.
        var issued = Column(6);
        issued.AddChild(Gold(Loc.GetString("rumc-government-licenses-issued")));
        if (licenses.Issued.Count == 0)
            issued.AddChild(Muted(Loc.GetString("rumc-government-license-none")));

        foreach (var license in licenses.Issued)
        {
            var serial = license.Serial;
            issued.AddChild(Rule());
            issued.AddChild(Text(Loc.GetString("rumc-government-license-line", ("serial", serial), ("type", license.Type), ("holder", license.Holder)), TermPalette.Text));

            if (license.Revoked)
            {
                issued.AddChild(Text(Loc.GetString("rumc-government-license-revoked"), TermPalette.Bad));
            }
            else if (license.LeftSeconds <= 0)
            {
                issued.AddChild(Muted(Loc.GetString("rumc-government-license-expired")));
            }
            else
            {
                issued.AddChild(Live(s => Loc.GetString("rumc-government-license-valid",
                    ("time", Time(s.Licenses.Issued.FirstOrDefault(i => i.Serial == serial)?.LeftSeconds ?? 0))), TermPalette.Good));
                issued.AddChild(Danger(Loc.GetString("rumc-government-license-revoke"), () => OnRevoke?.Invoke(serial), true, 130));
            }
        }

        _page.AddChild(Card(issued));
    }
}
