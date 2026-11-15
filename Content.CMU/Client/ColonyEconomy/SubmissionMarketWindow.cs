using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Shared.CMU14.ColonyEconomy;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.ColonyEconomy;

// the price board on a submission point, CRT theme like the budget console.
// rows are built once per set of commodities and updated in place, the server
// pushes a fresh state every time something sells
public sealed partial class SubmissionMarketWindow : DefaultWindow
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private readonly CrtConsoleKit _kit = new();
    private readonly PanelContainer _tradeTag;
    private readonly Label _tradeText;
    private readonly BoxContainer _events;
    private readonly BoxContainer _body;
    private readonly Label _footer;

    private readonly Dictionary<string, MarketRow> _rows = new();
    private string[] _rowKeys = [];

    private sealed class MarketRow
    {
        public required CommoditySparkline Spark;
        public required Label Price;
        public required Label Change;
    }

    public SubmissionMarketWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("cmu-submission-market-title");
        MinSize = new Vector2(500, 480);
        SetSize = new Vector2(540, 620);

        _tradeTag = _kit.Tag(out _tradeText);
        _kit.Root.AddChild(_kit.Header(Loc.GetString("cmu-submission-market-heading"), out var subheading, _tradeTag));
        subheading.Text = Loc.GetString("cmu-submission-market-subheading");

        _events = CrtConsoleKit.Column(4);
        _kit.Root.AddChild(_events);

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _body = CrtConsoleKit.Column(8);
        _body.HorizontalExpand = true;
        _body.Margin = new Thickness(0, 0, 6, 0);
        scroll.AddChild(_body);
        _kit.Root.AddChild(scroll);

        _kit.Root.AddChild(_kit.Footer(out _footer));

        Contents.AddChild(_kit.Tube);
    }

    public void UpdateState(SubmissionMarketBuiState state)
    {
        var trade = state.TradeMultiplier;
        var tradePercent = Percent(trade);
        if (tradePercent > 0)
            CrtConsoleKit.SetTag(_tradeTag, _tradeText, Loc.GetString("cmu-submission-market-trade-pact", ("percent", tradePercent)), CrtTerminalPalette.Accent);
        else if (tradePercent < 0)
            CrtConsoleKit.SetTag(_tradeTag, _tradeText, Loc.GetString("cmu-submission-market-embargo", ("percent", -tradePercent)), CrtTerminalPalette.Alert);
        else
            CrtConsoleKit.SetTag(_tradeTag, _tradeText, Loc.GetString("cmu-submission-market-open"), CrtTerminalPalette.TextDim);

        _footer.Text = Loc.GetString("cmu-submission-market-footer", ("tariff", (int) MathF.Round(state.Tariff * 100f)));

        UpdateEvents(state);

        var keys = state.Rows.Select(r => r.Commodity.Id).ToArray();
        if (!keys.SequenceEqual(_rowKeys))
            Rebuild(state);

        foreach (var row in state.Rows)
        {
            if (!_rows.TryGetValue(row.Commodity.Id, out var controls) || !_proto.TryIndex(row.Commodity, out var commodity))
                continue;

            var multiplier = row.Multiplier * trade;
            var (low, high) = PriceRange(commodity);
            controls.Price.Text = low == high
                ? Money(low * multiplier)
                : $"{Money(low * multiplier)}-{Money(high * multiplier)}";

            // market movement only, the trade pact already sits in the corner tag and would
            // otherwise paint every row +20%
            var change = Percent(row.Multiplier);
            controls.Change.Text = change switch
            {
                > 0 => Loc.GetString("cmu-submission-market-up", ("percent", change)),
                < 0 => Loc.GetString("cmu-submission-market-down", ("percent", -change)),
                _ => Loc.GetString("cmu-submission-market-steady"),
            };
            controls.Change.FontColorOverride = change == 0 ? CrtTerminalPalette.TextDim : CommoditySparkline.ColorFor(row.Multiplier);
            controls.Price.FontColorOverride = row.Event == CommodityEventKind.Boom ? CrtTerminalPalette.Accent : CrtTerminalPalette.TextBright;
            controls.Spark.SetHistory(row.History);
        }
    }

    private void UpdateEvents(SubmissionMarketBuiState state)
    {
        _events.RemoveAllChildren();
        foreach (var (row, elsewhere) in state.Rows.Select(r => (r, false)).Concat(state.Elsewhere.Select(r => (r, true))))
        {
            if (row.Event == CommodityEventKind.None || !_proto.TryIndex(row.Commodity, out var commodity))
                continue;

            var boom = row.Event == CommodityEventKind.Boom;
            var minutes = Math.Max(1, (int) Math.Ceiling((row.EventEnds - _timing.CurTime).TotalMinutes));
            var text = Loc.GetString(boom ? "cmu-submission-market-boom" : "cmu-submission-market-glut",
                ("commodity", Loc.GetString(commodity.Name)),
                ("percent", Math.Abs(Percent(row.EventMultiplier))),
                ("minutes", minutes));
            if (elsewhere)
                text += " " + Loc.GetString("cmu-submission-market-elsewhere");

            var color = boom ? CrtTerminalPalette.Accent : CrtTerminalPalette.Caution;
            var strip = CrtConsoleKit.Strip(CrtTerminalPalette.Surface2);
            var line = CrtConsoleKit.Row(8);
            line.AddChild(_kit.Chip(Loc.GetString(boom ? "cmu-submission-market-boom-chip" : "cmu-submission-market-glut-chip"), color));
            var label = CrtConsoleKit.Wrapping(text, color);
            label.HorizontalExpand = true;
            line.AddChild(label);
            strip.AddChild(line);
            _events.AddChild(strip);
        }
    }

    private void Rebuild(SubmissionMarketBuiState state)
    {
        _body.RemoveAllChildren();
        _rows.Clear();
        _rowKeys = state.Rows.Select(r => r.Commodity.Id).ToArray();

        if (state.Rows.Count == 0)
        {
            _body.AddChild(CrtConsoleKit.Text(Loc.GetString("cmu-submission-market-empty"), _kit.Small, CrtTerminalPalette.TextDim));
            return;
        }

        var grouped = state.Rows
            .Select(r => _proto.TryIndex(r.Commodity, out var c) ? c : null)
            .Where(c => c != null && c.Categories.Count > 0)
            .GroupBy(c => c!.Categories[0])
            .OrderBy(g => _proto.TryIndex(g.Key, out var cat) ? cat.Order : int.MaxValue);

        foreach (var group in grouped)
        {
            var title = _proto.TryIndex(group.Key, out var category) ? Loc.GetString(category.Name) : group.Key.Id;
            _body.AddChild(_kit.Section(title, out var column));

            // fallback rows ("other fruit") go last, they're the catch-all
            foreach (var commodity in group.OrderBy(c => c!.Fallback).ThenBy(c => Loc.GetString(c!.Name)))
            {
                column.AddChild(BuildRow(commodity!));
            }
        }
    }

    private Control BuildRow(CMUCommodityPrototype commodity)
    {
        var row = CrtConsoleKit.Row(8);

        var icon = new EntityPrototypeView
        {
            SetSize = new Vector2(32, 32),
            Stretch = SpriteView.StretchMode.Fill,
            VerticalAlignment = VAlignment.Center,
        };
        icon.SetPrototype(commodity.Icon);
        row.AddChild(icon);

        var names = CrtConsoleKit.Column(0);
        names.HorizontalExpand = true;
        names.VerticalAlignment = VAlignment.Center;
        var name = CrtConsoleKit.Text(Loc.GetString(commodity.Name), _kit.Body, CrtTerminalPalette.TextBright);
        name.ClipText = true;
        names.AddChild(name);
        var (low, high) = PriceRange(commodity);
        var normally = low == high
            ? Loc.GetString("cmu-submission-market-per-unit", ("price", Money(low)))
            : Loc.GetString("cmu-submission-market-grades", ("low", Money(low)), ("high", Money(high)));
        var sub = CrtConsoleKit.Text(normally, _kit.Small, CrtTerminalPalette.TextDim);
        sub.ClipText = true;
        names.AddChild(sub);
        row.AddChild(names);

        var spark = new CommoditySparkline();
        row.AddChild(spark);

        var prices = CrtConsoleKit.Column(0);
        prices.MinWidth = 96;
        prices.VerticalAlignment = VAlignment.Center;
        var price = CrtConsoleKit.Text(string.Empty, _kit.Bold, CrtTerminalPalette.TextBright);
        price.Align = Label.AlignMode.Right;
        prices.AddChild(price);
        var change = CrtConsoleKit.Text(string.Empty, _kit.Small, CrtTerminalPalette.TextDim);
        change.Align = Label.AlignMode.Right;
        prices.AddChild(change);
        row.AddChild(prices);

        _rows[commodity.ID] = new MarketRow { Spark = spark, Price = price, Change = change };
        return row;
    }

    private static (float Low, float High) PriceRange(CMUCommodityPrototype commodity)
    {
        if (commodity.Prices.Count == 0)
            return (0f, 0f);

        return (commodity.Prices.Values.Min(), commodity.Prices.Values.Max());
    }

    private static int Percent(float multiplier)
    {
        return (int) MathF.Round((multiplier - 1f) * 100f);
    }

    // crop prices have cents, CrtConsoleKit.Money rounds them away
    private static string Money(float amount)
    {
        return "$" + amount.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
