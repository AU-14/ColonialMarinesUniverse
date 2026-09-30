using System.Linq;
using System.Text;
using Content.Shared.CMU14.Radio;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     Everything the set has heard, newest first, with foreign traffic called out. Printing
///     is how an intercept leaves the radio for whoever can act on it.
/// </summary>
public sealed class ANPRCLogPage : BoxContainer
{
    private readonly RichTextLabel _intro;
    private readonly OptionButton _netFilter;
    private readonly LineEdit _search;
    private readonly Button _interceptsOnly;
    private readonly Button _print;
    private readonly Button _printIntercepts;
    private readonly Label _count;
    private readonly BoxContainer _entries;

    private readonly List<string> _nets = new();
    private string? _filterNet;
    private string _filterText = string.Empty;
    private bool _filterIntercepts;

    private ANPRCPanelData? _data;
    private string _key = string.Empty;

    public ANPRCLogPage(ANPRCRadioActions radio)
    {
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 5;

        _intro = ANPRCUi.Wrapped(Loc.GetString("anprc-op-log-intro"), ANPRCUi.TextDim);
        AddChild(_intro);

        var filters = ANPRCUi.Row(4);

        _netFilter = new OptionButton { MinWidth = 110 };
        _netFilter.OnItemSelected += args =>
        {
            _netFilter.SelectId(args.Id);
            _filterNet = args.Id == 0 || args.Id > _nets.Count ? null : _nets[args.Id - 1];
            Refresh();
        };

        _search = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("anprc-op-log-search-placeholder"),
        };
        _search.OnTextChanged += args =>
        {
            _filterText = args.Text.Trim();
            Refresh();
        };

        _interceptsOnly = ANPRCUi.Button(Loc.GetString("anprc-op-log-intercepts-only"), Loc.GetString("anprc-op-log-intercepts-only-tooltip"));
        _interceptsOnly.ToggleMode = true;
        _interceptsOnly.OnToggled += args =>
        {
            _filterIntercepts = args.Pressed;
            Refresh();
        };

        filters.AddChild(_netFilter);
        filters.AddChild(_search);
        filters.AddChild(_interceptsOnly);
        AddChild(filters);

        var printing = ANPRCUi.Row(4);
        _print = ANPRCUi.Button(Loc.GetString("anprc-op-log-print"), Loc.GetString("anprc-op-log-print-tooltip"), () => radio.PrintLog(false));
        _printIntercepts = ANPRCUi.Button(Loc.GetString("anprc-op-log-print-intercepts"), Loc.GetString("anprc-op-log-print-intercepts-tooltip"), () => radio.PrintLog(true));
        _print.HorizontalExpand = true;
        _printIntercepts.HorizontalExpand = true;
        printing.AddChild(_print);
        printing.AddChild(_printIntercepts);
        AddChild(printing);

        _count = ANPRCUi.Label(string.Empty, ANPRCUi.TextDim);
        AddChild(_count);

        _entries = ANPRCUi.Column(3);
        AddChild(ANPRCUi.Framed(_entries, ANPRCUi.LcdBack, ANPRCUi.LcdEdge));
        _entries.Margin = new Thickness(6, 4);
    }

    public void Update(ANPRCPanelData data)
    {
        _data = data;

        _print.Disabled = !data.Online || data.State.NetLog.Count == 0;
        _printIntercepts.Disabled = !data.Online || data.State.NetLog.All(entry => !entry.Intercepted);

        RebuildNetFilter(data);
        Refresh();
    }

    private void Refresh()
    {
        if (_data == null)
            return;

        var log = _data.State.NetLog;
        var key = new StringBuilder()
            .Append(log.Count).Append('|')
            // whole milliseconds: the sandbox refuses StringBuilder.Append(float)
            .Append(log.Count > 0 ? (int) (log[^1].Timestamp * 1000f) : 0).Append('|')
            .Append(_filterNet).Append('|').Append(_filterText).Append('|').Append(_filterIntercepts)
            .ToString();

        if (key == _key)
            return;

        _key = key;
        _entries.RemoveAllChildren();

        var shown = 0;

        // newest first: an operator opening the log wants what just came over the air
        for (var i = log.Count - 1; i >= 0; i--)
        {
            var entry = log[i];

            if (!Matches(entry))
                continue;

            shown++;
            _entries.AddChild(BuildEntry(entry));
        }

        var filtered = _filterNet != null || _filterText.Length > 0 || _filterIntercepts;

        _count.Text = filtered
            ? Loc.GetString("anprc-op-log-count-filtered", ("shown", shown), ("total", log.Count), ("max", ANPRCRadioComponent.MaxNetLogEntries))
            : Loc.GetString("anprc-op-log-count", ("count", log.Count), ("max", ANPRCRadioComponent.MaxNetLogEntries));

        if (shown == 0)
        {
            _entries.AddChild(ANPRCUi.Wrapped(Loc.GetString(log.Count == 0
                ? "anprc-op-log-empty"
                : "anprc-op-log-no-match"), ANPRCUi.LcdDim));
        }
    }

    private bool Matches(ANPRCNetLogEntry entry)
    {
        if (_filterIntercepts && !entry.Intercepted)
            return false;

        if (_filterNet != null && entry.ChannelDisplay != _filterNet)
            return false;

        return _filterText.Length == 0 ||
               entry.SenderName.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ||
               entry.Message.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
    }

    private static Control BuildEntry(ANPRCNetLogEntry entry)
    {
        var stamp = TimeSpan.FromSeconds(entry.Timestamp);
        var time = ((int) stamp.TotalMinutes).ToString("D2") + ":" + stamp.Seconds.ToString("D2");

        // the message came off the air, so it is added as text and never read as markup
        var message = new FormattedMessage();
        message.PushColor(entry.Intercepted ? ANPRCUi.Warn : ANPRCUi.LcdMid);
        message.AddText(Loc.GetString(entry.Intercepted ? "anprc-op-log-header-intercept" : "anprc-op-log-header",
            ("time", time),
            ("sender", entry.SenderName),
            ("net", entry.ChannelDisplay)));
        message.Pop();
        message.PushNewline();
        message.PushColor(ANPRCUi.Text);
        message.AddText(entry.Message);
        message.Pop();

        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(message);

        return label;
    }

    // only rebuilt when the set of nets in the log changes, otherwise every state push would
    // yank an open dropdown shut
    private void RebuildNetFilter(ANPRCPanelData data)
    {
        var nets = data.State.NetLog
            .Select(entry => entry.ChannelDisplay)
            .Distinct()
            .OrderBy(net => net, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (_netFilter.ItemCount > 0 && nets.SequenceEqual(_nets))
            return;

        _nets.Clear();
        _nets.AddRange(nets);

        _netFilter.Clear();
        _netFilter.AddItem(Loc.GetString("anprc-op-log-all-nets"), 0);

        for (var i = 0; i < _nets.Count; i++)
        {
            _netFilter.AddItem(_nets[i], i + 1);
        }

        // a net that rolled out of the fifty-line log would otherwise filter on nothing
        var index = _filterNet == null ? -1 : _nets.IndexOf(_filterNet);

        if (index < 0)
            _filterNet = null;

        _netFilter.SelectId(index < 0 ? 0 : index + 1);
        _key = string.Empty;
    }

    /// <summary>The newest entry's timestamp, so the panel can badge traffic that arrived unseen.</summary>
    public static float Newest(ANPRCRadioState state)
        => state.NetLog.Count > 0 ? state.NetLog[^1].Timestamp : 0f;
}
