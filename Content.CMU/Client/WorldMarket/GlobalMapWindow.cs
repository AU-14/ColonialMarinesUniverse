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
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Content.Client.CMU14.WorldMarket.TermKit;

namespace Content.Client.CMU14.WorldMarket;

// The window of the global map, which the console of the ambassador opens: the globe cut into cells with the biomes, the roads and the cities on the left, and on the right what is picked
// on it (a city with its favour and a gift, a road, a cell with its biome) and the lists of the cities and of the roads. Under the globe are the buttons that bring the map nearer and farther
// and the layers that can be put out. The globe is made once and kept, so that it does not begin to turn from the start again when the state comes.
public sealed class GlobalMapWindow : BaseWindow
{
    private const float PanelWidth = 320f;

    [Dependency] private readonly IPrototypeManager _proto = default!;

    private readonly Label _budget;
    private readonly Label _season = Muted(string.Empty);
    private readonly Label _points;
    private readonly Label _hover;
    private readonly Label _zoom;
    private readonly GlobeControl _globe = new();
    private readonly BoxContainer _panel = Column(6);
    private readonly ScrollContainer _scroll;

    private AmbassadorWorldMapState? _state;
    private MapSelection _selected = MapSelection.None;

    // A pick that waits for the next frame: the panel is not made again under the click that picked.
    private bool _dirty;

    // A gift to a city, paid from the budget of the console.
    public event Action<string>? OnGift;

    // Something done for a city (the city, the action).
    public event Action<string, string>? OnAction;

    // A region of interest marked round a place (the latitude, the longitude).
    public event Action<float, float>? OnClaimZone;

    // What is done with the other factions and the cities (pacts, strikes, bids, joint works, support of a good thing, a company against a threat).
    public event Action<MapCommandKind, string, string, string, int>? OnCommand;

    private void Command(MapCommandKind kind, string a = "", string b = "", string c = "", int n = 0)
    {
        OnCommand?.Invoke(kind, a, b, c, n);
    }

    public GlobalMapWindow()
    {
        IoCManager.InjectDependencies(this);

        Resizable = false;
        MouseFilter = MouseFilterMode.Stop;

        var close = new TermButton { Text = "✕", MinWidth = 28 };
        close.OnPressed += _ => Close();

        _budget = Text(string.Empty, TermPalette.Good);
        _points = Text(string.Empty, TermPalette.Gold);

        var header = Row(10);
        header.AddChild(Gold(Loc.GetString("rumc-map-title"), expand: true));
        header.AddChild(Muted(Loc.GetString("rumc-map-budget")));
        header.AddChild(_budget);
        header.AddChild(Muted(Loc.GetString("rumc-mother-points")));
        header.AddChild(_points);
        header.AddChild(close);

        // The globe and the bar under it.
        // The text under the globe changes with the cursor; clipped, so that its length cannot change the size of the bar (and of the globe over it).
        _hover = Muted(string.Empty, expand: true);
        _hover.ClipText = true;
        _hover.MinWidth = 40;
        _zoom = Muted(string.Empty);

        var zoomOut = new TermButton { Text = "−", MinWidth = 32, ToolTip = Loc.GetString("rumc-map-zoom-out") };
        zoomOut.OnPressed += _ => _globe.ZoomBy(1f / 1.3f);
        var zoomIn = new TermButton { Text = "+", MinWidth = 32, ToolTip = Loc.GetString("rumc-map-zoom-in") };
        zoomIn.OnPressed += _ => _globe.ZoomBy(1.3f);
        var reset = new TermButton { Text = Loc.GetString("rumc-map-zoom-reset") };
        reset.OnPressed += _ => _globe.ResetView();

        var bar = Row(6);
        bar.AddChild(zoomOut);
        bar.AddChild(zoomIn);
        bar.AddChild(reset);
        bar.AddChild(_zoom);
        bar.AddChild(_hover);
        bar.AddChild(Layer("rumc-map-layer-roads", _globe.ShowRoads, on => _globe.ShowRoads = on));
        bar.AddChild(Layer("rumc-map-layer-biomes", _globe.ShowBiomes, on => _globe.ShowBiomes = on));
        bar.AddChild(Layer("rumc-map-layer-grid", _globe.ShowGrid, on => _globe.ShowGrid = on));

        var left = Column(6);
        left.AddChild(Card(_globe, margin: 2));
        left.AddChild(bar);

        _globe.OnPicked += picked =>
        {
            _selected = picked;
            _dirty = true;
        };
        _globe.OnHovered += hovered => _hover.Text = DescribeHover(hovered);

        _scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, MinSize = new Vector2(PanelWidth, 0), MaxWidth = PanelWidth };
        _scroll.AddChild(_panel);

        var body = Row(10);
        body.AddChild(left);
        body.AddChild(_scroll);

        var root = Column(6);
        root.Margin = new Thickness(8, 6, 8, 8);
        root.AddChild(header);
        root.AddChild(Muted(Loc.GetString("rumc-map-hint")));
        root.AddChild(_season);
        root.AddChild(Rule());
        root.AddChild(body);

        AddChild(new TermPanel(scanlines: true) { MouseFilter = MouseFilterMode.Pass, Children = { root } });

        BuildPanel();
    }

    private static TermButton Layer(string key, bool on, Action<bool> set)
    {
        var button = new TermButton { Text = Loc.GetString(key), ToggleMode = true, Pressed = on };
        button.OnToggled += pressed => set(pressed);
        return button;
    }

    public void SetState(AmbassadorWorldMapState state)
    {
        _state = state;
        _budget.Text = Money(state.Budget);
        _points.Text = state.Points.ToString();
        _globe.SetCities(state.Cities, state.MapSeed, state.Roads);
        _globe.SetZones(state.Zones);
        _globe.SetHazards(state.Extras.Hazards);
        _globe.SetCaravans(state.Extras.Caravans);

        _season.Text = Loc.GetString("rumc-map-season",
                ("name", Loc.GetString("rumc-map-season-name-" + state.Extras.Season)), ("time", Time(state.Extras.SeasonSeconds)))
            + " — " + Loc.GetString("rumc-map-season-effect-" + state.Extras.Season);
        BuildPanel();
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos) => relativeMousePos.Y < 34 ? DragMode.Move : DragMode.None;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _zoom.Text = Loc.GetString("rumc-map-zoom", ("value", _globe.Zoom.ToString("0.0")));

        if (!_dirty)
            return;

        _dirty = false;
        BuildPanel();
    }

    #region Text

    private static string Coordinate(float value, string positive, string negative)
    {
        return Loc.GetString(value >= 0f ? positive : negative, ("value", ((int) MathF.Round(MathF.Abs(value))).ToString()));
    }

    private static string Place(float latitude, float longitude)
    {
        return Loc.GetString("rumc-map-place",
            ("lat", Coordinate(latitude, "rumc-map-north", "rumc-map-south")),
            ("lon", Coordinate(longitude, "rumc-map-east", "rumc-map-west")));
    }

    private static string KindName(WorldRoadKind kind)
    {
        return Loc.GetString(kind switch
        {
            WorldRoadKind.Road => "rumc-map-kind-road",
            WorldRoadKind.Trail => "rumc-map-kind-trail",
            _ => "rumc-map-kind-searoute",
        });
    }

    private string CityName(string id)
    {
        return _state?.Cities.FirstOrDefault(c => c.Id == id)?.Name ?? id;
    }

    // The line under the globe about what the cursor is on.
    private string DescribeHover(MapSelection hover)
    {
        switch (hover.Kind)
        {
            case SelectionKind.City:
                return CityName(hover.Id ?? string.Empty);
            case SelectionKind.Road when _state?.Roads.FirstOrDefault(r => r.Id == hover.Id) is { } road:
                return road.Name;
            case SelectionKind.Cell when hover.Id != null && _proto.TryIndex<WorldBiomePrototype>(hover.Id, out var biome):
                return Loc.GetString(biome.Name);
            default:
                return string.Empty;
        }
    }

    // Text that goes to the next line at the edge of the column.
    private static Control Wrapped(string text, Color color)
    {
        var label = new RichTextLabel { MaxWidth = PanelWidth - 24f, HorizontalExpand = true };
        label.SetMessage(FormattedMessage.FromUnformatted(text), null, color);
        return label;
    }

    #endregion

    #region The panel

    private void BuildPanel()
    {
        _panel.RemoveAllChildren();

        if (_state == null || _state.Cities.Count == 0)
        {
            _panel.AddChild(Card(Muted(Loc.GetString("rumc-map-no-cities"))));
            return;
        }

        switch (_selected.Kind)
        {
            case SelectionKind.City when _state.Cities.FirstOrDefault(c => c.Id == _selected.Id) is { } city:
                _panel.AddChild(Card(CityDetails(city), city.Color));
                break;
            case SelectionKind.Road when _state.Roads.FirstOrDefault(r => r.Id == _selected.Id) is { } road:
                _panel.AddChild(Card(RoadDetails(road), TermPalette.Gold));
                break;
            case SelectionKind.Cell when _selected.Id != null && _proto.TryIndex<WorldBiomePrototype>(_selected.Id, out var biome):
                _panel.AddChild(Card(CellDetails(biome), biome.Color));
                break;
            default:
                _panel.AddChild(Card(Wrapped(Loc.GetString("rumc-map-nothing"), TermPalette.Muted)));
                break;
        }

        _panel.AddChild(Card(CityList()));
        _panel.AddChild(Card(RoadList()));
        _panel.AddChild(Card(ZoneList()));
        _panel.AddChild(Card(DiplomacyList()));
        _panel.AddChild(Card(CaravanList()));
        _panel.AddChild(Card(DossierList()));
        _panel.AddChild(Card(HazardList()));
    }

    private Control CityDetails(CityEntry city)
    {
        var column = Column(6);
        column.AddChild(Text(city.Name, city.Color));
        column.AddChild(Muted(Place(city.Latitude, city.Longitude)));
        column.AddChild(Wrapped(city.Description, TermPalette.Text));

        // In the region of interest of another faction everything costs more, and the extra goes to the owner.
        if (city.ZoneOwner.Length > 0)
        {
            column.AddChild(Rule());
            column.AddChild(Wrapped(Loc.GetString("rumc-map-zone-foreign", ("owner", city.ZoneOwner), ("percent", ZoneSurchargePercent)), new Color(1f, 0.75f, 0.4f)));
        }

        column.AddChild(Rule());
        column.AddChild(Gold(Loc.GetString("rumc-map-capabilities")));
        foreach (var capability in city.Capabilities)
            column.AddChild(Wrapped("• " + capability, TermPalette.Text));

        column.AddChild(Rule());
        column.AddChild(Text(Loc.GetString("rumc-map-influence", ("value", city.Influence))));
        column.AddChild(new FillBar(city.Influence / 100f, city.Color));

        // The favour in a city is one scale for all the ambassadors: who has how much of the hundred.
        if (city.Extras.Shares.Count > 0)
        {
            column.AddChild(Muted(Loc.GetString("rumc-map-shares")));
            foreach (var share in city.Extras.Shares)
                column.AddChild(Text(Loc.GetString("rumc-map-share-line", ("faction", share.Faction), ("value", share.Total))));
        }

        var id = city.Id;
        var full = city.Influence >= WorldMarketInfluenceMax;
        var gift = full
            ? Loc.GetString("rumc-map-gift-full")
            : Loc.GetString(city.CanAfford ? "rumc-map-gift" : "rumc-map-gift-poor", ("cost", Money(city.GiftCost)));
        column.AddChild(Action(gift, () => OnGift?.Invoke(id), city.CanAfford));

        // The trouble that the city is in, what the faction has going there, and what it can do for the city.
        if (city.CrisisSeconds > 0)
        {
            column.AddChild(Rule());
            column.AddChild(Text(Loc.GetString("rumc-map-crisis", ("name", city.CrisisName), ("time", Time(city.CrisisSeconds))), new Color(1f, 0.5f, 0.45f)));
            column.AddChild(Wrapped(city.CrisisDescription, TermPalette.Muted));
        }

        if (city.Projects.Count > 0)
        {
            column.AddChild(Rule());
            column.AddChild(Gold(Loc.GetString("rumc-map-projects")));
            foreach (var project in city.Projects)
                column.AddChild(Wrapped("• " + ProjectLine(project), TermPalette.Text));
        }

        if (city.Actions.Count > 0)
        {
            column.AddChild(Rule());
            column.AddChild(Gold(Loc.GetString("rumc-map-help")));
            foreach (var action in city.Actions)
            {
                var actionId = action.Id;
                var price = action.PointCost > 0 ? Loc.GetString("rumc-mother-cost", ("points", action.PointCost)) : Money(action.Cost);
                var button = Action(action.Name + " · " + price, () => OnAction?.Invoke(id, actionId), action.Blocked.Length == 0);
                button.ToolTip = action.Description + "\n" + Loc.GetString("rumc-map-action-influence", ("value", action.Influence));
                column.AddChild(button);

                if (action.Blocked.Length > 0)
                    column.AddChild(Wrapped(action.Blocked, TermPalette.Muted));
            }
        }

        AddCityExtras(column, city);

        // The roads that go from the city.
        var roads = _state!.Roads.Where(r => r.From == city.Id || r.To == city.Id).ToList();
        if (roads.Count > 0)
        {
            column.AddChild(Rule());
            column.AddChild(Gold(Loc.GetString("rumc-map-roads-of-city")));
            foreach (var road in roads)
            {
                var roadId = road.Id;
                column.AddChild(Action(road.Name + " · " + KindName(road.Kind), () => _globe.FocusRoad(roadId)));
            }
        }

        return column;
    }

    // The most that the favour in a city can be (the server holds the same number).
    private const int WorldMarketInfluenceMax = 100;

    private Control RoadDetails(RoadEntry road)
    {
        var column = Column(6);
        column.AddChild(Text(road.Name, TermPalette.Gold));
        column.AddChild(Muted(Loc.GetString("rumc-map-road-kind", ("kind", KindName(road.Kind)))));

        if (road.Blocked.Length > 0)
            column.AddChild(Text(Loc.GetString("rumc-map-road-blocked", ("reason", road.Blocked)), new Color(1f, 0.5f, 0.45f)));

        column.AddChild(Wrapped(road.Description, TermPalette.Text));
        column.AddChild(Rule());

        var from = road.From;
        var to = road.To;
        column.AddChild(Action(CityName(from), () => _globe.FocusCity(from, true)));
        column.AddChild(Action(CityName(to), () => _globe.FocusCity(to, true)));
        return column;
    }

    private Control CellDetails(WorldBiomePrototype biome)
    {
        var column = Column(6);
        column.AddChild(Muted(Loc.GetString("rumc-map-cell")));
        column.AddChild(Text(Loc.GetString("rumc-map-biome") + ": " + Loc.GetString(biome.Name), biome.Color));
        column.AddChild(Muted(Place(_selected.Latitude, _selected.Longitude)));
        column.AddChild(Wrapped(Loc.GetString(biome.Description), TermPalette.Text));

        // A region of interest can be marked round the cell.
        if (_state != null)
        {
            var latitude = _selected.Latitude;
            var longitude = _selected.Longitude;
            column.AddChild(Rule());
            column.AddChild(Wrapped(Loc.GetString("rumc-map-zone-hint", ("percent", ZoneSurchargePercent)), TermPalette.Muted));
            column.AddChild(Action(Loc.GetString("rumc-map-zone-claim", ("points", _state.ZoneCost)), () => OnClaimZone?.Invoke(latitude, longitude), _state.Points >= _state.ZoneCost));
        }

        return column;
    }

    // The most that the others pay extra in a region of interest (the server holds the same number).
    private const int ZoneSurchargePercent = 25;

    // What is going on in a city besides the troubles: a good thing to support, a bargain, the patron, the secret strikes, the joint works.
    private void AddCityExtras(BoxContainer column, CityEntry city)
    {
        var extras = city.Extras;
        var id = city.Id;
        var state = _state!;

        if (extras.EventName.Length > 0)
        {
            column.AddChild(Rule());
            column.AddChild(Text(Loc.GetString("rumc-map-event", ("name", extras.EventName), ("time", Time(extras.EventSeconds))), TermPalette.Gold));
            column.AddChild(Wrapped(extras.EventDescription, TermPalette.Muted));
            column.AddChild(Wrapped(Loc.GetString("rumc-map-event-funding",
                ("sum", Money(extras.EventFunding)), ("goal", Money(extras.EventGoal)), ("mine", Money(extras.EventMine)), ("count", extras.EventSpoilers)), TermPalette.Text));

            foreach (var amount in new[] { 200, 500 })
            {
                var sum = amount;
                column.AddChild(Action(Loc.GetString("rumc-map-event-support", ("sum", Money(sum))), () => Command(MapCommandKind.EventSupport, id, n: sum), state.Budget >= sum));
            }

            column.AddChild(Action(Loc.GetString("rumc-map-event-spoil", ("sum", Money(extras.EventSpoilCost))), () => Command(MapCommandKind.EventSpoil, id), state.Budget >= extras.EventSpoilCost));
        }

        if (extras.BidSeconds > 0)
        {
            column.AddChild(Rule());
            column.AddChild(Text(Loc.GetString("rumc-map-bid-title", ("time", Time(extras.BidSeconds))), TermPalette.Gold));
            column.AddChild(Wrapped(Loc.GetString("rumc-map-bid-hint"), TermPalette.Muted));

            foreach (var bid in extras.Bids)
                column.AddChild(Wrapped("• " + Loc.GetString("rumc-map-bid-line", ("faction", bid.Faction), ("total", Money(bid.Total))), TermPalette.Text));

            foreach (var amount in new[] { 200, 500 })
            {
                var sum = amount;
                column.AddChild(Action(Loc.GetString("rumc-map-bid-raise", ("sum", Money(sum))), () => Command(MapCommandKind.Bid, id, n: sum), state.Budget >= sum));
            }
        }

        if (extras.Patron.Length > 0)
        {
            column.AddChild(Rule());
            column.AddChild(Wrapped(Loc.GetString("rumc-map-patron", ("faction", extras.Patron), ("time", Time(extras.PatronSeconds))), TermPalette.Gold));
        }

        if (extras.Strikes.Count > 0)
        {
            column.AddChild(Rule());
            column.AddChild(Gold(Loc.GetString("rumc-map-strikes")));
            column.AddChild(Wrapped(Loc.GetString("rumc-map-strike-hint"), TermPalette.Muted));

            foreach (var strike in extras.Strikes)
            {
                var target = strike.Target;
                var label = Loc.GetString(strike.Answer ? "rumc-map-strike-answer" : "rumc-map-strike", ("target", target), ("cost", Money(strike.Cost)), ("chance", strike.Chance));
                column.AddChild(Action(label, () => Command(MapCommandKind.Strike, id, target), strike.Blocked.Length == 0));

                if (strike.Blocked.Length > 0)
                    column.AddChild(Wrapped(strike.Blocked, TermPalette.Muted));
            }
        }

        if (extras.IntelCost > 0)
        {
            column.AddChild(Rule());
            column.AddChild(Gold(Loc.GetString("rumc-map-intel")));
            column.AddChild(Wrapped(Loc.GetString("rumc-map-intel-hint"), TermPalette.Muted));
            column.AddChild(Action(Loc.GetString("rumc-map-intel-buy", ("cost", Money(extras.IntelCost))), () => Command(MapCommandKind.Intel, id), extras.IntelBlocked.Length == 0));

            if (extras.IntelBlocked.Length > 0)
                column.AddChild(Wrapped(extras.IntelBlocked, TermPalette.Muted));
        }

        // The headquarters of the intelligence: the scouts, and the harm that is done in secret.
        if (extras.Hq)
        {
            column.AddChild(Rule());
            column.AddChild(Gold(Loc.GetString("rumc-map-hq-title")));
            column.AddChild(Wrapped(Loc.GetString("rumc-map-hq-hint"), TermPalette.Muted));

            if (extras.ScoutSeconds > 0)
            {
                column.AddChild(Wrapped(Loc.GetString("rumc-map-scouts-away", ("time", Time(extras.ScoutSeconds))), TermPalette.Gold));
            }
            else
            {
                column.AddChild(Action(Loc.GetString("rumc-map-scouts-send", ("cost", Money(extras.ScoutCost))), () => Command(MapCommandKind.Scouts, id), extras.ScoutBlocked.Length == 0));
                if (extras.ScoutBlocked.Length > 0)
                    column.AddChild(Wrapped(extras.ScoutBlocked, TermPalette.Muted));
            }

            if (extras.ScoutReport.Count > 0)
            {
                column.AddChild(Text(Loc.GetString("rumc-map-scouts-report"), TermPalette.Gold));
                foreach (var line in extras.ScoutReport)
                    column.AddChild(Wrapped("• " + line, TermPalette.Text));
            }

            foreach (var smear in extras.Smears)
            {
                var target = smear.Target;
                var label = Loc.GetString("rumc-map-smear", ("target", target), ("cost", Money(smear.Cost)), ("chance", smear.Chance));
                column.AddChild(Action(label, () => Command(MapCommandKind.Smear, id, target), smear.Blocked.Length == 0));
                if (smear.Blocked.Length > 0)
                    column.AddChild(Wrapped(smear.Blocked, TermPalette.Muted));
            }

            foreach (var sabotage in extras.Sabotages)
            {
                var target = sabotage.Target;
                var label = Loc.GetString("rumc-map-sabotage", ("target", target), ("cost", Money(sabotage.Cost)), ("chance", sabotage.Chance));
                column.AddChild(Action(label, () => Command(MapCommandKind.Sabotage, id, target), sabotage.Blocked.Length == 0));
                if (sabotage.Blocked.Length > 0)
                    column.AddChild(Wrapped(sabotage.Blocked, TermPalette.Muted));
            }

            AddHarmButtons(column, extras.Buyouts, "rumc-map-buyout", MapCommandKind.Buyout, id);
            AddHarmButtons(column, extras.Freezes, "rumc-map-freeze", MapCommandKind.Freeze, id);
            AddHarmButtons(column, extras.Poisons, "rumc-map-poison", MapCommandKind.Poison, id);

            // The scouts of the others that are on their way here.
            foreach (var spotted in extras.Intercepts)
            {
                var target = spotted.Target;
                var label = Loc.GetString("rumc-map-intercept", ("target", target), ("cost", Money(spotted.Cost)), ("chance", spotted.Chance));
                column.AddChild(Action(label, () => Command(MapCommandKind.Intercept, id, target), spotted.Blocked.Length == 0));
                if (spotted.Blocked.Length > 0)
                    column.AddChild(Wrapped(spotted.Blocked, TermPalette.Muted));
            }

            column.AddChild(Action(Loc.GetString("rumc-map-provoke", ("cost", Money(extras.ProvokeCost))), () => Command(MapCommandKind.Provoke, id), extras.ProvokeBlocked.Length == 0));
            if (extras.ProvokeBlocked.Length > 0)
                column.AddChild(Wrapped(extras.ProvokeBlocked, TermPalette.Muted));
        }

        if (extras.JointActions.Count > 0 && state.Extras.Factions.Count > 0)
        {
            column.AddChild(Rule());
            column.AddChild(Gold(Loc.GetString("rumc-map-joint-title")));
            column.AddChild(Wrapped(Loc.GetString("rumc-map-joint-hint"), TermPalette.Muted));

            foreach (var joint in extras.JointActions)
            {
                var actionId = joint.Id;
                foreach (var other in state.Extras.Factions)
                {
                    var partner = other.Name;
                    var label = Loc.GetString("rumc-map-joint-propose", ("action", joint.Name), ("faction", partner), ("cost", Money(joint.Cost)));
                    column.AddChild(Action(label, () => Command(MapCommandKind.JointPropose, id, actionId, partner), joint.Blocked.Length == 0));
                }

                if (joint.Blocked.Length > 0)
                    column.AddChild(Wrapped(joint.Blocked, TermPalette.Muted));
            }
        }
    }

    // The button of a harm to a faction as a whole (not in a place), if there is one for the faction: the cost is in points for a complaint, in money for the rest.
    private void AddFactionHarm(BoxContainer column, List<StrikeEntry> entries, string faction, string textKey, MapCommandKind kind, bool points)
    {
        var entry = entries.FirstOrDefault(e => e.Target == faction);
        if (entry == null)
            return;

        var label = Loc.GetString(textKey, ("cost", points ? Loc.GetString("rumc-mother-cost", ("points", entry.Cost)) : Money(entry.Cost)), ("chance", entry.Chance));
        column.AddChild(Action(label, () => Command(kind, faction), entry.Blocked.Length == 0));
        if (entry.Blocked.Length > 0)
            column.AddChild(Wrapped(entry.Blocked, TermPalette.Muted));
    }

    // A button for each faction that a secret harm can be done to in a place: what it is called (a text with the target, the cost and the chance), the command, the place.
    private void AddHarmButtons(BoxContainer column, List<StrikeEntry> entries, string textKey, MapCommandKind kind, string place)
    {
        foreach (var entry in entries)
        {
            var target = entry.Target;
            var label = Loc.GetString(textKey, ("target", target), ("cost", Money(entry.Cost)), ("chance", entry.Chance));
            column.AddChild(Action(label, () => Command(kind, place, target), entry.Blocked.Length == 0));
            if (entry.Blocked.Length > 0)
                column.AddChild(Wrapped(entry.Blocked, TermPalette.Muted));
        }
    }

    // The other ambassadors: a pact with each, and the joint works that they offer.
    private Control DiplomacyList()
    {
        var column = Column(4);
        column.AddChild(Gold(Loc.GetString("rumc-map-diplomacy")));
        column.AddChild(Wrapped(Loc.GetString("rumc-map-diplomacy-hint"), TermPalette.Muted));

        var extras = _state!.Extras;
        if (extras.Factions.Count == 0)
            column.AddChild(Muted(Loc.GetString("rumc-map-diplomacy-none")));

        // The counter-intelligence of the headquarters: harm to the faction is less likely to succeed and does not stay secret.
        if (extras.Hq)
        {
            column.AddChild(Rule());
            column.AddChild(Gold(Loc.GetString("rumc-map-counter-title")));
            column.AddChild(Wrapped(Loc.GetString("rumc-map-counter-hint"), TermPalette.Muted));

            if (extras.CounterSeconds > 0)
            {
                column.AddChild(Wrapped(Loc.GetString("rumc-map-counter-running", ("time", Time(extras.CounterSeconds))), TermPalette.Good));
            }
            else
            {
                column.AddChild(Action(Loc.GetString("rumc-map-counter-on-button", ("cost", Money(extras.CounterCost))), () => Command(MapCommandKind.CounterIntel), extras.CounterBlocked.Length == 0));
                if (extras.CounterBlocked.Length > 0)
                    column.AddChild(Wrapped(extras.CounterBlocked, TermPalette.Muted));
            }

            // False reports for the scouts of the others.
            column.AddChild(Rule());
            column.AddChild(Gold(Loc.GetString("rumc-map-disinfo-title")));
            column.AddChild(Wrapped(Loc.GetString("rumc-map-disinfo-hint"), TermPalette.Muted));
            if (extras.DisinfoSeconds > 0)
            {
                column.AddChild(Wrapped(Loc.GetString("rumc-map-disinfo-running", ("time", Time(extras.DisinfoSeconds))), TermPalette.Good));
            }
            else
            {
                column.AddChild(Action(Loc.GetString("rumc-map-disinfo-button", ("cost", Money(extras.DisinfoCost))), () => Command(MapCommandKind.Disinfo), extras.DisinfoBlocked.Length == 0));
                if (extras.DisinfoBlocked.Length > 0)
                    column.AddChild(Wrapped(extras.DisinfoBlocked, TermPalette.Muted));
            }

            // What the spy in a headquarters of another has seen.
            if (extras.SpySeconds > 0)
            {
                column.AddChild(Rule());
                column.AddChild(Text(Loc.GetString("rumc-map-spy-title", ("time", Time(extras.SpySeconds))), TermPalette.Gold));
                if (extras.SpyLines.Count == 0)
                    column.AddChild(Muted(Loc.GetString("rumc-map-spy-quiet")));

                foreach (var line in extras.SpyLines)
                    column.AddChild(Wrapped("• " + line, TermPalette.Text));
            }
        }

        // The mother faction has been told of the faction: its next payment is cut by half unless it pays the inspectors off.
        if (extras.Inspected)
        {
            column.AddChild(Rule());
            column.AddChild(Wrapped(Loc.GetString("rumc-map-inspected"), new Color(1f, 0.6f, 0.4f)));
            column.AddChild(Action(Loc.GetString("rumc-map-payoff-button", ("cost", Money(extras.PayOffCost))), () => Command(MapCommandKind.PayOff), extras.PayOffBlocked.Length == 0));
            if (extras.PayOffBlocked.Length > 0)
                column.AddChild(Wrapped(extras.PayOffBlocked, TermPalette.Muted));
        }

        foreach (var faction in extras.Factions)
        {
            var name = faction.Name;
            column.AddChild(Rule());

            var status = faction.Pact ? "rumc-map-pact-yes" : faction.OfferReceived ? "rumc-map-pact-received" : faction.OfferSent ? "rumc-map-pact-sent" : "rumc-map-pact-none";
            column.AddChild(Text(name + " — " + Loc.GetString(status), faction.Pact ? TermPalette.Good : TermPalette.Text));

            if (faction.Answer)
                column.AddChild(Wrapped(Loc.GetString("rumc-map-answer"), new Color(1f, 0.75f, 0.4f)));

            // Against the faction: a complaint to the mother faction (paid in points), and with the headquarters leaking its secrets, a spy, spoiling its main goals.
            AddFactionHarm(column, extras.Complaints, name, "rumc-map-complain", MapCommandKind.Complain, true);
            AddFactionHarm(column, extras.Leaks, name, "rumc-map-leak", MapCommandKind.Leak, false);
            AddFactionHarm(column, extras.Spies, name, "rumc-map-spy", MapCommandKind.Spy, false);
            AddFactionHarm(column, extras.TaskSabotages, name, "rumc-map-tasks", MapCommandKind.SabotageTasks, false);

            if (faction.Pact)
                column.AddChild(Action(Loc.GetString("rumc-map-pact-break"), () => Command(MapCommandKind.PactBreak, name)));
            else if (faction.OfferReceived)
                column.AddChild(Action(Loc.GetString("rumc-map-pact-accept"), () => Command(MapCommandKind.PactAccept, name)));
            else if (!faction.OfferSent)
                column.AddChild(Action(Loc.GetString("rumc-map-pact-propose"), () => Command(MapCommandKind.PactPropose, name)));

            // Deals: money for points, points for money.
            foreach (var (give, want) in DealPresets)
            {
                var g = give;
                var w = want;
                column.AddChild(Action(Loc.GetString("rumc-map-deal-propose", ("give", SideText(g)), ("want", SideText(w))), () => Command(MapCommandKind.DealPropose, name, g, w), CanGive(g)));
            }
        }

        if (extras.Deals.Count > 0)
        {
            column.AddChild(Rule());
            column.AddChild(Gold(Loc.GetString("rumc-map-deals")));
            column.AddChild(Wrapped(Loc.GetString("rumc-map-deal-hint"), TermPalette.Muted));

            foreach (var deal in extras.Deals)
            {
                var dealId = deal.Id;
                if (!deal.Incoming)
                {
                    column.AddChild(Wrapped("• " + Loc.GetString("rumc-map-deal-out", ("to", deal.To), ("give", deal.Give), ("want", deal.Want)), TermPalette.Muted));
                    continue;
                }

                column.AddChild(Wrapped("• " + Loc.GetString("rumc-map-deal-in", ("from", deal.From), ("give", deal.Give), ("want", deal.Want)), TermPalette.Text));
                column.AddChild(Action(Loc.GetString("rumc-map-deal-accept"), () => Command(MapCommandKind.DealAnswer, dealId, n: 1), deal.Blocked.Length == 0));
                column.AddChild(Action(Loc.GetString("rumc-map-deal-decline"), () => Command(MapCommandKind.DealAnswer, dealId, n: 0)));

                if (deal.Blocked.Length > 0)
                    column.AddChild(Wrapped(deal.Blocked, TermPalette.Muted));
            }
        }

        if (extras.JointOffers.Count > 0)
        {
            column.AddChild(Rule());
            column.AddChild(Gold(Loc.GetString("rumc-map-joint-offers")));

            foreach (var offer in extras.JointOffers)
            {
                var offerId = offer.Id;
                column.AddChild(Wrapped("• " + Loc.GetString("rumc-map-joint-offer-line", ("from", offer.From), ("action", offer.Action), ("city", offer.City)), TermPalette.Text));
                column.AddChild(Action(Loc.GetString("rumc-map-joint-accept", ("cost", Money(offer.Cost))), () => Command(MapCommandKind.JointAnswer, offerId, n: 1), offer.Blocked.Length == 0));
                column.AddChild(Action(Loc.GetString("rumc-map-joint-decline"), () => Command(MapCommandKind.JointAnswer, offerId, n: 0)));

                if (offer.Blocked.Length > 0)
                    column.AddChild(Wrapped(offer.Blocked, TermPalette.Muted));
            }
        }

        return column;
    }

    // The kinds of deals that are offered (what is given, what is asked): "M" is money, "P" is points.
    private static readonly (string Give, string Want)[] DealPresets =
    {
        ("M6000", "P1"), ("P1", "M4000"), ("P2", "M8000"), ("M12000", "P2"),
    };

    private static string SideText(string side)
    {
        var amount = int.Parse(side.Substring(1));
        return side[0] == 'M' ? Money(amount) : Loc.GetString("rumc-mother-cost", ("points", amount));
    }

    private bool CanGive(string side)
    {
        var amount = int.Parse(side.Substring(1));
        return _state != null && (side[0] == 'M' ? _state.Budget >= amount : _state.Points >= amount);
    }

    // The caravans that are on the roads, and an escort that can be hired for those of the faction.
    private Control CaravanList()
    {
        var column = Column(4);
        column.AddChild(Gold(Loc.GetString("rumc-map-caravans")));
        column.AddChild(Wrapped(Loc.GetString("rumc-map-caravans-hint"), TermPalette.Muted));

        var caravans = _state!.Extras.Caravans;
        if (caravans.Count == 0)
            column.AddChild(Muted(Loc.GetString("rumc-map-caravans-none")));

        foreach (var caravan in caravans)
        {
            var latitude = caravan.Latitude;
            var longitude = caravan.Longitude;
            var caravanId = caravan.Id.ToString();

            column.AddChild(Rule());
            var line = Loc.GetString("rumc-map-caravan-line", ("faction", caravan.Faction), ("what", caravan.Label), ("city", caravan.Target), ("time", Time(caravan.EtaSeconds)));
            column.AddChild(Wrapped(line + (caravan.Escorted ? " " + Loc.GetString("rumc-map-caravan-escorted") : string.Empty), caravan.Mine ? TermPalette.Gold : TermPalette.Text));
            column.AddChild(Action(caravan.Faction + " ⌖", () => _globe.FocusPlace(latitude, longitude)));

            if (caravan.Mine && !caravan.Escorted)
            {
                column.AddChild(Action(Loc.GetString("rumc-map-caravan-escort", ("cost", Money(caravan.EscortCost))), () => Command(MapCommandKind.Escort, caravanId), caravan.Blocked.Length == 0));

                if (caravan.Blocked.Length > 0)
                    column.AddChild(Wrapped(caravan.Blocked, TermPalette.Muted));
            }
        }

        return column;
    }

    // What the report of intelligence says of the other ambassadors.
    private Control DossierList()
    {
        var column = Column(4);
        column.AddChild(Gold(Loc.GetString("rumc-map-dossier-title")));

        var extras = _state!.Extras;
        if (extras.IntelSeconds <= 0)
        {
            column.AddChild(Muted(Loc.GetString("rumc-map-intel-none")));
            return column;
        }

        column.AddChild(Wrapped(Loc.GetString("rumc-map-intel-valid", ("time", Time(extras.IntelSeconds)), ("level", extras.IntelLevel)), TermPalette.Muted));

        foreach (var dossier in extras.Dossiers)
        {
            column.AddChild(Rule());
            var status = Loc.GetString(dossier.Online ? "rumc-map-dossier-online" : "rumc-map-dossier-offline");
            column.AddChild(Text(Loc.GetString("rumc-map-dossier-name", ("name", dossier.Faction), ("status", status)) + (dossier.Pact ? " " + Loc.GetString("rumc-map-dossier-pact") : string.Empty),
                dossier.Online ? TermPalette.Good : TermPalette.Text));
            column.AddChild(Wrapped(Loc.GetString("rumc-map-dossier-trust", ("value", dossier.Trust)), TermPalette.Text));

            if (dossier.Points >= 0)
            {
                column.AddChild(Wrapped(Loc.GetString("rumc-map-dossier-points", ("value", dossier.Points)), TermPalette.Text));
                column.AddChild(Wrapped(Loc.GetString("rumc-map-dossier-cities", ("value", dossier.Cities)), TermPalette.Text));
            }

            if (dossier.Influence.Length > 0)
                column.AddChild(Wrapped(Loc.GetString("rumc-map-dossier-influence", ("value", dossier.Influence)), TermPalette.Text));

            if (dossier.Projects.Length > 0)
                column.AddChild(Wrapped(Loc.GetString("rumc-map-dossier-projects", ("value", dossier.Projects)), TermPalette.Text));
        }

        if (extras.IntelLevel < 3)
            column.AddChild(Wrapped(Loc.GetString("rumc-map-dossier-hidden"), TermPalette.Muted));

        return column;
    }

    // The hazards of the map: what each is, where (a click turns the ball to it), and a company that can be hired against a threat.
    private Control HazardList()
    {
        var column = Column(4);
        column.AddChild(Gold(Loc.GetString("rumc-map-hazards")));

        var hazards = _state!.Extras.Hazards;
        if (hazards.Count == 0)
            column.AddChild(Muted(Loc.GetString("rumc-map-hazards-none")));

        foreach (var hazard in hazards)
        {
            var latitude = hazard.Latitude;
            var longitude = hazard.Longitude;
            var hazardId = hazard.Id.ToString();

            column.AddChild(Rule());
            column.AddChild(Text(Loc.GetString("rumc-map-hazard-line", ("name", hazard.Name), ("time", Time(hazard.SecondsLeft))), hazard.Weather ? new Color(0.65f, 0.8f, 1f) : new Color(1f, 0.5f, 0.45f)));
            column.AddChild(Wrapped(hazard.Description, TermPalette.Muted));
            column.AddChild(Action(hazard.Name + " ⌖", () => _globe.FocusPlace(latitude, longitude)));

            if (hazard.Weather)
            {
                column.AddChild(Muted(Loc.GetString("rumc-map-hazard-weather")));
                continue;
            }

            column.AddChild(Action(Loc.GetString("rumc-map-hazard-remove", ("name", hazard.Name), ("cost", Money(hazard.RemoveCost))), () => Command(MapCommandKind.HazardRemove, hazardId), hazard.Blocked.Length == 0));

            if (hazard.Blocked.Length > 0)
                column.AddChild(Wrapped(hazard.Blocked, TermPalette.Muted));
        }

        return column;
    }

    private Control ZoneList()
    {
        var column = Column(4);
        column.AddChild(Gold(Loc.GetString("rumc-map-zones")));

        if (_state!.Zones.Count == 0)
            column.AddChild(Muted(Loc.GetString("rumc-map-zones-none")));

        foreach (var zone in _state.Zones)
            column.AddChild(Wrapped("• " + Loc.GetString(zone.Mine ? "rumc-map-zone-mine" : "rumc-map-zone-other", ("owner", zone.Owner), ("time", Time(zone.SecondsLeft))), zone.Mine ? TermPalette.Gold : TermPalette.Text));

        return column;
    }

    private static string ProjectLine(ProjectEntry project)
    {
        var line = project.ReadySeconds > 0
            ? Loc.GetString("rumc-map-project-building", ("name", project.Name), ("time", Time(project.ReadySeconds)))
            : project.LeftSeconds >= 0
                ? Loc.GetString("rumc-map-project-standing", ("name", project.Name), ("time", Time(project.LeftSeconds)))
                : Loc.GetString("rumc-map-project-permanent", ("name", project.Name));

        return project.Charges > 0 ? line + " · " + Loc.GetString("rumc-map-project-charges", ("charges", project.Charges)) : line;
    }

    private Control CityList()
    {
        var column = Column(4);
        column.AddChild(Gold(Loc.GetString("rumc-map-cities")));
        foreach (var city in _state!.Cities)
        {
            var id = city.Id;
            column.AddChild(Action(city.Name, () => _globe.FocusCity(id, true), _selected.Kind != SelectionKind.City || _selected.Id != id));
        }

        return column;
    }

    private Control RoadList()
    {
        var column = Column(4);
        column.AddChild(Gold(Loc.GetString("rumc-map-roads")));
        foreach (var road in _state!.Roads.OrderBy(r => r.Kind).ThenBy(r => r.Name))
        {
            var id = road.Id;
            column.AddChild(Action(road.Name + " · " + KindName(road.Kind) + (road.Blocked.Length > 0 ? " ✗" : string.Empty), () => _globe.FocusRoad(id), _selected.Kind != SelectionKind.Road || _selected.Id != id));
        }

        return column;
    }

    #endregion
}
