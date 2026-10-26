using System;
using System.Linq;
using System.Numerics;
using Content.Client.Lobby.UI;
using Content.Shared.CMU14.RoundStatistics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.RoundStatistics;

public sealed partial class CMURoundStatisticsWindow
{
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly BoxContainer _body;
    private readonly BoxContainer _navigation;
    private readonly BoxContainer _location;
    private readonly PanelContainer _locationPanel;
    private readonly ScrollContainer _scroll;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _refresh;
    private readonly BoxContainer _modeTabs;
    private CMURoundStatisticsDashboard? _dashboard;
    private CMURoundStatisticsDashboard? _browseDashboard;
    private CMURoundStatisticsQuery _query = new();
    private int _view;
    private bool _againstThreat = true;
    private string? _detailTitle;
    private string? _detailKind;
    private CMURoundMatchupStatistics? _selectedMatchup;
    private CMURoundOutcomeRecord? _selectedRound;
    private Vector2 _browseScroll;
    private Vector2? _pendingScroll;
    private ScrollContainer? _matchupScroll;
    private Vector2 _browseMatrixScroll;
    private Vector2? _pendingMatrixScroll;
    private bool _loading;
    private static readonly string[] Views = { "showcase", "planets", "population", "threats", "matchups", "archive" };

    public event Action? OnRefresh;
    public event Action<CMURoundStatisticsQuery>? OnQuery;

    public CMURoundStatisticsWindow()
    {
        IoCManager.InjectDependencies(this);
        MinSize = new Vector2(1080, 680);
        SetSize = new Vector2(1180, 820);
        Title = Loc.GetString("cmu-round-stats-title");
        var root = Column();
        root.Margin = new Thickness(8);
        root.VerticalExpand = true;
        var header = new BoxContainer { HorizontalExpand = true, SeparationOverride = 12 };
        _summary = Hint(Loc.GetString("cmu-round-stats-loading"));
        header.AddChild(_summary);
        _refresh = ActionButton("refresh", () => OnRefresh?.Invoke());
        header.AddChild(_refresh);
        root.AddChild(header);

        _modeTabs = new BoxContainer { SeparationOverride = 4, HorizontalExpand = true };
        foreach (var preset in Enum.GetValues<CMURoundStatisticsPreset>())
        {
            var button = DeskButton(PresetName(preset), () =>
            {
                ClearDetail();
                _againstThreat = preset is CMURoundStatisticsPreset.DistressSignal or CMURoundStatisticsPreset.ColonyFall;
                Query(new CMURoundStatisticsQuery(preset));
            });
            button.ToggleMode = true;
            button.HorizontalExpand = true;
            button.MinHeight = 42;
            _modeTabs.AddChild(button);
        }
        root.AddChild(_modeTabs);
        _status = Hint("");
        _status.Visible = false;
        root.AddChild(_status);

        var layout = new BoxContainer { HorizontalExpand = true, VerticalExpand = true, SeparationOverride = 0 };
        var rail = Surface(Card, DeskLine);
        rail.HorizontalExpand = false;
        rail.MinWidth = 156;
        rail.MaxWidth = 156;
        _navigation = Column();
        _navigation.Margin = new Thickness(6, 16);
        _navigation.SeparationOverride = 4;
        for (var i = 0; i < Views.Length; i++)
        {
            var view = i;
            var button = ActionButton(Views[i], () =>
            {
                var scoped = _query.PlanetId != null || _query.ThreatId != null || _query.PopulationBand != null;
                ClearDetail();
                _view = view;
                if (scoped)
                    Query(new CMURoundStatisticsQuery(_query.Preset));
                else
                    Render(Vector2.Zero);
            });
            button.MinHeight = 42;
            button.HorizontalExpand = true;
            button.ToggleMode = true;
            button.Label.Align = Label.AlignMode.Left;
            _navigation.AddChild(button);
        }
        rail.AddChild(_navigation);
        layout.AddChild(rail);
        var page = Column();
        page.SeparationOverride = 0;
        page.VerticalExpand = true;
        _locationPanel = Surface(Background, DeskLine);
        _location = new BoxContainer { HorizontalExpand = true, SeparationOverride = 12, Margin = new Thickness(12, 8), MinHeight = 34 };
        _locationPanel.AddChild(_location);
        page.AddChild(_locationPanel);
        _scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
        var background = Surface(Background, Color.Transparent);
        _body = Column();
        _body.Margin = new Thickness(18);
        background.AddChild(_body);
        _scroll.AddChild(background);
        page.AddChild(_scroll);
        layout.AddChild(page);
        root.AddChild(layout);
        Contents.AddChild(root);
        CrtLobbyTheme.ApplyWindow(this, useCrtTypography: true);
        UpdateNavigation();
    }

    public void UpdateDashboard(CMURoundStatisticsDashboard dashboard, bool loading, bool failed)
    {
        _loading = loading;
        _refresh.Disabled = loading;
        foreach (var child in _modeTabs.Children.Concat(_navigation.Children))
            ((Button) child).Disabled = loading;
        _status.Visible = loading || failed;
        if (_status.Visible)
            _status.Text = Loc.GetString(failed ? "cmu-round-stats-failed" : "cmu-round-stats-loading");
        if (loading || failed)
            return;

        _dashboard = dashboard;
        _query = dashboard.Query;
        _summary.Text = Loc.GetString("cmu-round-stats-history",
            ("count", dashboard.TotalRecords), ("round", dashboard.LatestRoundId),
            ("date", dashboard.FirstRecordedAt?.ToString("yyyy-MM-dd") ?? "—"));
        _summary.ToolTip = Loc.GetString("cmu-round-stats-history-note");
        if (_selectedMatchup is { } pair)
            _selectedMatchup = dashboard.Matchups.FirstOrDefault(candidate =>
                candidate.AgainstThreat == pair.AgainstThreat && candidate.PlatoonId == pair.PlatoonId &&
                candidate.OpponentId == pair.OpponentId);
        Render(_pendingScroll ?? _scroll.GetScrollValue());
    }

    private void Query(CMURoundStatisticsQuery query)
    {
        _query = query;
        _pendingScroll ??= Vector2.Zero;
        // Do not keep interactive rows from the previous scope while the new request is pending.
        _body.ReleaseChildren();
        UpdateNavigation();
        OnQuery?.Invoke(query);
    }

    private void OpenDetail(string kind, string title, CMURoundStatisticsQuery? query = null,
        CMURoundMatchupStatistics? matchup = null, CMURoundOutcomeRecord? round = null)
    {
        _browseDashboard = _dashboard;
        _browseScroll = _scroll.GetScrollValue();
        _browseMatrixScroll = _matchupScroll?.GetScrollValue() ?? Vector2.Zero;
        _detailKind = kind;
        _detailTitle = title;
        _selectedMatchup = matchup;
        _selectedRound = round;
        if (query != null)
            Query(query);
        else
            Render(Vector2.Zero);
    }

    private void Back()
    {
        if (_browseDashboard is not { } browse)
            return;
        var scoped = _query.PlanetId != null || _query.ThreatId != null || _query.PopulationBand != null;
        var position = _browseScroll;
        ClearDetail();
        _dashboard = browse;
        _query = browse.Query;
        _pendingMatrixScroll = _browseMatrixScroll;
        Render(position);
        if (scoped)
        {
            // Restore the server query too, so Refresh and archive paging keep the browse scope.
            OnQuery?.Invoke(_query);
        }
    }

    private void ClearDetail()
    {
        _detailKind = null;
        _detailTitle = null;
        _selectedMatchup = null;
        _selectedRound = null;
        _browseDashboard = null;
    }

    private void UpdateNavigation()
    {
        for (var i = 0; i < _navigation.ChildCount; i++)
        {
            var button = (Button) _navigation.GetChild(i);
            button.Pressed = i == _view;
            StyleNavigation(button, i == _view, false);
        }
        for (var i = 0; i < _modeTabs.ChildCount; i++)
        {
            var button = (Button) _modeTabs.GetChild(i);
            button.Pressed = i == (int) _query.Preset;
            StyleNavigation(button, button.Pressed, true);
        }
        _location.ReleaseChildren();
        _locationPanel.PanelOverride = DeskStyle(_detailTitle == null ? Background : DeskPurple, DeskLine);
        if (_detailTitle != null)
        {
            var back = DeskButton(Loc.GetString("cmu-round-stats-back-to",
                ("section", Loc.GetString("cmu-round-stats-" + Views[_view]))), Back);
            back.ToolTip = Loc.GetString("cmu-round-stats-back-hint");
            _location.AddChild(back);
        }
        var path = PresetName(_query.Preset) + " / " + Loc.GetString("cmu-round-stats-" + Views[_view]);
        if (_detailTitle != null)
            path += " / " + _detailTitle;
        _location.AddChild(Hint(path));
        _location.AddChild(new Label
        {
            Text = Loc.GetString(_detailTitle == null ? "cmu-round-stats-browse-level" : "cmu-round-stats-detail-level"),
            FontColorOverride = _detailTitle == null ? Muted : DeskAccent,
        });
    }

    private void Render(Vector2? position = null)
    {
        _pendingScroll = position ?? _scroll.GetScrollValue();
        _pendingMatrixScroll ??= _matchupScroll?.GetScrollValue();
        _matchupScroll = null;
        _body.ReleaseChildren();
        UpdateNavigation();
        if (_dashboard?.SelectedMode is not { } mode)
            return;
        if (_detailTitle != null)
        {
            RenderDetail(_selectedMatchup?.Statistics ?? mode);
            return;
        }
        if (_view != 0)
            AddPageHeading(Loc.GetString("cmu-round-stats-" + Views[_view]), mode.Total);
        switch (_view)
        {
            case 0: _body.AddChild(MakeModePanel(mode)); break;
            case 1: RenderPlanets(mode); break;
            case 2: RenderPopulation(mode); break;
            case 3: RenderThreats(mode); break;
            case 4: RenderMatchups(mode); break;
            case 5: RenderArchive(); break;
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_pendingMatrixScroll is { } matrixPosition && _matchupScroll is { IsArrangeValid: true })
        {
            _pendingMatrixScroll = null;
            _matchupScroll.SetScrollValue(matrixPosition);
        }
        // A rebuilt page must finish layout before the old scroll position can be restored.
        if (_pendingScroll is not { } position || !_scroll.IsArrangeValid || !_body.IsArrangeValid)
            return;
        _pendingScroll = null;
        _scroll.SetScrollValue(position);
    }

    private static BoxContainer Column() => new()
    {
        Orientation = BoxContainer.LayoutOrientation.Vertical,
        SeparationOverride = 10,
        HorizontalExpand = true,
    };

    private static Label Hint(string text) => new()
    {
        Text = text, FontColorOverride = Muted, ClipText = true, HorizontalExpand = true, ToolTip = text,
    };

    private static Button ActionButton(string key, Action action)
        => DeskButton(Loc.GetString("cmu-round-stats-" + key), action);
}
