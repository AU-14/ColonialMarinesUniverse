using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Eui;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Yautja.ClanHall;

// one window for everything clan related. tabs swap on the client instantly, anything that
// changes the selection or the data goes to the server, and the state that comes back is the truth
public sealed partial class YautjaClanHallWindow : BaseWindow
{
    private const float ResizeGrip = 14;

    private readonly Font _title;
    private readonly Font _heading;
    private readonly Font _readout;
    private readonly Font _small;
    private readonly Font _body;
    private readonly Font _bodyBold;
    private readonly Font _big;

    private readonly YautjaClanHallHolo _holo;
    private readonly Label _viewerName;
    private readonly Label _viewerLine;
    private readonly Label _viewerHonor;
    private readonly TextureRect _viewerIcon;
    private readonly Label _status;
    private readonly BoxContainer _rail;
    private readonly Dictionary<YautjaClanHallPage, HallButton> _tabs = new();
    private readonly Dictionary<YautjaClanHallPage, Control> _pages = new();
    private readonly Control _staffDivider;

    private YautjaClanHallEuiState? _state;
    private YautjaClanHallPage _page = YautjaClanHallPage.Clan;
    private YautjaClanHallPage _backPage = YautjaClanHallPage.Leaderboard;
    private int _pendingAfter = -1;
    private float _pendingDots;

    public event Action<EuiMessageBase>? Send;

    public YautjaClanHallWindow()
    {
        _title = YautjaClanHallStyle.Font(YautjaClanHallStyle.Osd, 13);
        _heading = YautjaClanHallStyle.Font(YautjaClanHallStyle.Osd, 11);
        _readout = YautjaClanHallStyle.Font(YautjaClanHallStyle.Osd, 10);
        _small = YautjaClanHallStyle.Font(YautjaClanHallStyle.Osd, 8);
        _big = YautjaClanHallStyle.Font(YautjaClanHallStyle.Osd, 18);
        _body = YautjaClanHallStyle.Font(YautjaClanHallStyle.Sans, 12);
        _bodyBold = YautjaClanHallStyle.Font(YautjaClanHallStyle.SansBold, 12);

        Resizable = true;
        // the frame itself drags, so it has to catch clicks on empty bezel
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(880, 560);
        SetSize = new Vector2(1020, 660);

        var bezel = new YautjaClanHallBezel();
        AddChild(bezel);

        // top band: title glyphs + close
        var band = YautjaClanHallStyle.HBox(8);
        band.AddChild(new GlyphStrip(3, 4, YautjaClanHallStyle.HotRed) { VerticalAlignment = VAlignment.Center });
        var title = YautjaClanHallStyle.Label(Loc.GetString("cmu-yautja-hall-title"), _title, YautjaClanHallStyle.HotRed);
        title.VerticalAlignment = VAlignment.Center;
        band.AddChild(title);
        band.AddChild(new GlyphStrip(11, 14, YautjaClanHallStyle.Border) { VerticalAlignment = VAlignment.Center, HorizontalExpand = true });
        var close = new HallButton("✕", _heading, YautjaClanHallStyle.HotRed, 8, 1) { VerticalAlignment = VAlignment.Center };
        close.OnPressed += _ => Close();
        band.AddChild(close);
        bezel.AddChild(band);

        var screen = YautjaClanHallStyle.VBox(0);
        screen.HorizontalExpand = screen.VerticalExpand = true;
        bezel.AddChild(screen);

        // header: who's looking
        var header = YautjaClanHallStyle.Panel(YautjaClanHallStyle.Surface, YautjaClanHallStyle.MutedBorder, 10, 6);
        var headerRow = YautjaClanHallStyle.HBox(10);
        _viewerIcon = YautjaClanHallStyle.Icon(null, 36);
        headerRow.AddChild(_viewerIcon);
        var who = YautjaClanHallStyle.VBox(0);
        who.HorizontalExpand = true;
        _viewerName = YautjaClanHallStyle.Label(string.Empty, _heading, YautjaClanHallStyle.Text);
        _viewerLine = YautjaClanHallStyle.Label(string.Empty, _small, YautjaClanHallStyle.Muted);
        who.AddChild(_viewerName);
        who.AddChild(_viewerLine);
        headerRow.AddChild(who);
        var honorBox = YautjaClanHallStyle.VBox(0);
        var honorLabel = YautjaClanHallStyle.Label(Loc.GetString("cmu-yautja-hall-honor"), _small, YautjaClanHallStyle.HonorDim);
        honorLabel.HorizontalAlignment = HAlignment.Right;
        honorBox.AddChild(honorLabel);
        _viewerHonor = YautjaClanHallStyle.Label("0", _big, YautjaClanHallStyle.Honor);
        _viewerHonor.HorizontalAlignment = HAlignment.Right;
        honorBox.AddChild(_viewerHonor);
        headerRow.AddChild(honorBox);
        header.AddChild(headerRow);
        screen.AddChild(header);

        var body = YautjaClanHallStyle.HBox(0);
        body.VerticalExpand = true;
        screen.AddChild(body);

        _rail = YautjaClanHallStyle.VBox(4);
        _rail.MinWidth = 150;
        _rail.Margin = new Thickness(8, 10, 8, 8);
        body.AddChild(_rail);
        body.AddChild(new PanelContainer { MinWidth = 1, PanelOverride = YautjaClanHallStyle.Box(YautjaClanHallStyle.MutedBorder) });

        var pageArea = new Control { HorizontalExpand = true, VerticalExpand = true, Margin = new Thickness(12, 10, 12, 8) };
        body.AddChild(pageArea);

        AddTab(YautjaClanHallPage.Clan, "cmu-yautja-hall-tab-clan", 101, BuildClanPage());
        AddTab(YautjaClanHallPage.Leaderboard, "cmu-yautja-hall-tab-boards", 202, BuildBoardsPage());
        AddTab(YautjaClanHallPage.Hunter, "cmu-yautja-hall-tab-hunter", 303, BuildHunterPage());
        _staffDivider = YautjaClanHallStyle.Label(Loc.GetString("cmu-yautja-hall-staff"), _small, YautjaClanHallStyle.Staff);
        _staffDivider.Margin = new Thickness(2, 12, 0, 2);
        _rail.AddChild(_staffDivider);
        AddTab(YautjaClanHallPage.Whitelists, "cmu-yautja-hall-tab-whitelists", 404, BuildWhitelistPage(), staff: true);
        AddTab(YautjaClanHallPage.Round, "cmu-yautja-hall-tab-round", 505, BuildRoundPage(), staff: true);
        foreach (var page in _pages.Values)
            pageArea.AddChild(page);

        // status line
        var statusBar = YautjaClanHallStyle.Panel(YautjaClanHallStyle.Surface, YautjaClanHallStyle.MutedBorder, 10, 3);
        _status = YautjaClanHallStyle.Label(string.Empty, _small, YautjaClanHallStyle.Dim);
        statusBar.AddChild(_status);
        screen.AddChild(statusBar);

        _holo = new YautjaClanHallHolo();
        bezel.AddChild(_holo);

        ShowPage(YautjaClanHallPage.Clan, flicker: false);
    }

    private void AddTab(YautjaClanHallPage page, string loc, int seed, Control content, bool staff = false)
    {
        var accent = staff ? YautjaClanHallStyle.Staff : YautjaClanHallStyle.HotRed;
        var button = new TabButton(Loc.GetString(loc), _heading, accent, seed);
        button.OnPressed += _ => ShowPage(page);
        _rail.AddChild(button);
        _tabs[page] = button;
        content.Visible = false;
        _pages[page] = content;
    }

    private void ShowPage(YautjaClanHallPage page, bool flicker = true)
    {
        if (page != YautjaClanHallPage.Hunter)
            _backPage = page;

        if (flicker && page != _page)
            _holo.Flicker();

        _page = page;
        foreach (var (key, content) in _pages)
            content.Visible = key == page;
        foreach (var (key, tab) in _tabs)
            tab.Selected = key == page;
    }

    // every request: mark the window as waiting until a newer state comes back
    private void Request(EuiMessageBase message)
    {
        _pendingAfter = _state?.Revision ?? 0;
        Send?.Invoke(message);
        UpdateStatus();
    }

    private void OpenClan(int clanId)
    {
        ShowPage(YautjaClanHallPage.Clan);
        if (_state?.Clan?.Id != clanId)
            Request(new YautjaClanHallShowMessage(YautjaClanHallPage.Clan, clanId));
    }

    private void OpenHunter(NetUserId hunter)
    {
        ShowPage(YautjaClanHallPage.Hunter);
        if (_state?.Hunter?.PlayerId != hunter)
            Request(new YautjaClanHallShowMessage(YautjaClanHallPage.Hunter, hunter: hunter));
    }

    public void UpdateState(YautjaClanHallEuiState state)
    {
        var first = _state == null;
        _state = state;
        if (_pendingAfter >= 0 && state.Revision > _pendingAfter)
            _pendingAfter = -1;

        var viewer = state.Viewer;
        _viewerIcon.Texture = YautjaClanHallStyle.RankIcon(viewer.Rank);
        _viewerName.Text = viewer.Name.ToUpperInvariant();
        var clan = viewer.ClanId is { } clanId ? state.Clans.FirstOrDefault(c => c.Id == clanId) : null;
        _viewerLine.Text = Loc.GetString("cmu-yautja-hall-viewer-line",
            ("rank", YautjaClanHallStyle.RankName(viewer.Rank)),
            ("clan", clan?.Name ?? Loc.GetString("cmu-yautja-hall-clanless")));
        if (viewer.Staff)
            _viewerLine.Text += "  ·  " + Loc.GetString(viewer.Host ? "cmu-yautja-hall-viewer-host" : "cmu-yautja-hall-viewer-staff");
        _viewerHonor.Text = YautjaClanHallStyle.Number(viewer.Honor);

        _staffDivider.Visible = viewer.Staff || viewer.Host;
        _tabs[YautjaClanHallPage.Whitelists].Visible = state.Whitelist != null;
        _tabs[YautjaClanHallPage.Round].Visible = state.Round != null;

        UpdateClanPage(state);
        UpdateBoardsPage(state);
        UpdateHunterPage(state);
        UpdateWhitelistPage(state);
        UpdateRoundPage(state);

        if (state.Navigate || first)
        {
            var page = state.Page;
            if (page == YautjaClanHallPage.Whitelists && state.Whitelist == null ||
                page == YautjaClanHallPage.Round && state.Round == null)
            {
                page = YautjaClanHallPage.Clan;
            }

            // nothing to show on the clan tab, start somewhere useful
            if (page == YautjaClanHallPage.Clan && state.Clan == null)
                page = YautjaClanHallPage.Leaderboard;

            ShowPage(page, flicker: !first);
        }

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_pendingAfter >= 0)
        {
            _status.FontColorOverride = YautjaClanHallStyle.Muted;
            _status.Text = "▸ " + Loc.GetString("cmu-yautja-hall-transmitting") + new string('.', 1 + (int) _pendingDots % 3);
            return;
        }

        if (_state is not { } state || string.IsNullOrEmpty(state.Status))
        {
            _status.FontColorOverride = YautjaClanHallStyle.Dim;
            _status.Text = "▸ " + Loc.GetString("cmu-yautja-hall-ready");
            return;
        }

        _status.FontColorOverride = state.StatusIsError ? YautjaClanHallStyle.HotRed : YautjaClanHallStyle.Honor;
        _status.Text = "▸ " + state.Status;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_pendingAfter < 0)
            return;

        _pendingDots += args.DeltaSeconds * 3;
        UpdateStatus();
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        if (relativeMousePos.X >= Size.X - ResizeGrip && relativeMousePos.Y >= Size.Y - ResizeGrip)
            return DragMode.Bottom | DragMode.Right;

        return DragMode.Move;
    }

    // shared bits the pages use

    private string ClanName(int? clanId)
    {
        if (clanId == null)
            return Loc.GetString("cmu-yautja-hall-clanless");

        return _state?.Clans.FirstOrDefault(c => c.Id == clanId)?.Name ?? Loc.GetString("cmu-yautja-hall-clan-unknown");
    }

    private Color ClanInk(int? clanId)
    {
        var clan = clanId == null ? null : _state?.Clans.FirstOrDefault(c => c.Id == clanId);
        return clan == null ? YautjaClanHallStyle.Dim : YautjaClanHallStyle.Readable(YautjaClanHallStyle.ClanColor(clan.Color));
    }

    private Control SectionTitle(string text, Color? color = null)
    {
        var row = YautjaClanHallStyle.HBox(6);
        row.AddChild(new GlyphStrip(text.Length, 2, (color ?? YautjaClanHallStyle.HotRed).WithAlpha(0.6f)) { VerticalAlignment = VAlignment.Center });
        row.AddChild(YautjaClanHallStyle.Label(text.ToUpperInvariant(), _heading, color ?? YautjaClanHallStyle.HotRed));
        return row;
    }

    // a hunter on a list: rank icon, name, honor, a lit pip when they're online. click opens the dossier
    private Control HunterLine(YautjaClanHallHunterRow hunter, string? prefix = null, bool showClan = false, bool highlight = false)
    {
        var button = new HallButton(string.Empty, _body, YautjaClanHallStyle.HotRed, 0, 0) { HorizontalExpand = true };
        button.Label.Visible = false;
        if (highlight)
            button.Selected = true;

        var row = YautjaClanHallStyle.HBox(8);
        row.Margin = new Thickness(6, 2);
        if (prefix != null)
        {
            var number = YautjaClanHallStyle.Label(prefix, _readout, YautjaClanHallStyle.Dim);
            number.MinWidth = 34;
            number.VerticalAlignment = VAlignment.Center;
            row.AddChild(number);
        }

        row.AddChild(YautjaClanHallStyle.Icon(YautjaClanHallStyle.RankIcon(hunter.Rank), 22));
        var name = YautjaClanHallStyle.Label(hunter.Name, _bodyBold, YautjaClanHallStyle.Text, clip: true);
        name.HorizontalExpand = true;
        name.VerticalAlignment = VAlignment.Center;
        row.AddChild(name);
        if (showClan)
        {
            var clan = YautjaClanHallStyle.Label(ClanName(hunter.ClanId), _small, ClanInk(hunter.ClanId), clip: true);
            clan.MinWidth = 150;
            clan.VerticalAlignment = VAlignment.Center;
            row.AddChild(clan);
        }

        var honor = YautjaClanHallStyle.Label(YautjaClanHallStyle.Number(hunter.Honor), _readout, YautjaClanHallStyle.Honor);
        honor.MinWidth = 64;
        honor.Align = Label.AlignMode.Right;
        honor.VerticalAlignment = VAlignment.Center;
        row.AddChild(honor);
        row.AddChild(new Pip(hunter.Online) { VerticalAlignment = VAlignment.Center });

        button.AddChild(row);
        var id = hunter.PlayerId;
        button.OnPressed += _ => OpenHunter(id);
        return button;
    }

    private static Control Scroll(Control content)
    {
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
        content.HorizontalExpand = true;
        scroll.AddChild(content);
        return scroll;
    }

    private Control Empty(string text)
    {
        var label = YautjaClanHallStyle.Label(text, _readout, YautjaClanHallStyle.Dim);
        label.Margin = new Thickness(4, 6);
        return label;
    }

    private sealed class TabButton : HallButton
    {
        public TabButton(string text, Font font, Color accent, int seed) : base(string.Empty, font, accent, 0, 0)
        {
            Label.Visible = false;
            MinHeight = 34;
            var row = YautjaClanHallStyle.HBox(8);
            row.Margin = new Thickness(8, 4);
            row.AddChild(new GlyphStrip(seed, 2, accent) { VerticalAlignment = VAlignment.Center });
            var label = YautjaClanHallStyle.Label(text.ToUpperInvariant(), font, YautjaClanHallStyle.Text);
            label.VerticalAlignment = VAlignment.Center;
            row.AddChild(label);
            AddChild(row);
        }
    }

    // a short run of glyphs as a control, for headings and the tab rail
    private sealed class GlyphStrip : Control
    {
        private readonly int _seed;
        private readonly int _count;
        private readonly Color _color;

        public GlyphStrip(int seed, int count, Color color)
        {
            _seed = seed;
            _count = count;
            _color = color;
            MinSize = new Vector2(count * 7 + 2, 9);
            MouseFilter = MouseFilterMode.Ignore;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            YautjaClanHallStyle.DrawGlyphs(handle, new Vector2(0, (PixelSize.Y - 7 * UIScale) / 2), _seed, _count, UIScale, _color);
        }
    }

    private sealed class Pip : Control
    {
        private readonly bool _on;

        public Pip(bool on)
        {
            _on = on;
            MinSize = new Vector2(10, 10);
            MouseFilter = MouseFilterMode.Ignore;
            ToolTip = Loc.GetString(on ? "cmu-yautja-hall-online" : "cmu-yautja-hall-offline");
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            var c = PixelSize / 2;
            if (_on)
            {
                handle.DrawCircle(c, 4.5f * UIScale, YautjaClanHallStyle.Online.WithAlpha(0.25f));
                handle.DrawCircle(c, 2.5f * UIScale, YautjaClanHallStyle.Online);
            }
            else
            {
                handle.DrawCircle(c, 2.5f * UIScale, YautjaClanHallStyle.MutedBorder);
            }
        }
    }
}
