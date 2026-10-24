using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Eui;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Yautja.ClanHall;

// one window for clan info, leaderboard, hunter records and the staff pages, drawn as a predator hud.
// tabs switch on the client, anything that changes data goes to the server and its reply is the truth
public sealed partial class YautjaClanHallWindow : BaseWindow
{
    private const float ResizeGrip = 16;
    private static readonly ResPath RankIcons = new("/Textures/CMU14/Yautja/rank_icons.rsi");
    private static readonly ResPath Masks = new("/Textures/CMU14/Yautja/masks");

    private readonly Font _title;
    private readonly Font _label;
    private readonly Font _small;
    private readonly Font _number;
    private readonly Font _big;
    private readonly Font _body;
    private readonly Font _bodyBold;

    private readonly Dictionary<YautjaClanHallPage, HudButton> _tabButtons = new();
    private readonly Dictionary<YautjaClanHallPage, Control> _pages = new();
    private readonly Label _status;

    private YautjaClanHallEuiState? _state;
    private YautjaClanHallPage _page = YautjaClanHallPage.Clan;
    private NetUserId? _selectedHunter;

    public event Action<EuiMessageBase>? Send;

    public YautjaClanHallWindow()
    {
        _title = Hud.Font(Hud.Osd, 14);
        _label = Hud.Font(Hud.Osd, 10);
        _small = Hud.Font(Hud.Osd, 8);
        _number = Hud.Font(Hud.Osd, 22);
        _big = Hud.Font(Hud.Osd, 18);
        _body = Hud.Font(Hud.Sans, 12);
        _bodyBold = Hud.Font(Hud.SansBold, 12);

        Resizable = true;
        // the black field drags the window, buttons take their own clicks first
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(820, 560);
        SetSize = new Vector2(960, 660);

        var background = new HudBackground { HorizontalExpand = true, VerticalExpand = true };
        AddChild(background);
        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 10, Margin = new Thickness(18, 14) };
        background.AddChild(root);

        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        var title = Hud.Write(Loc.GetString("cmu-yautja-hall-title").ToUpperInvariant(), _title, Hud.Bright);
        title.VerticalAlignment = VAlignment.Center;
        title.Margin = new Thickness(0, 0, 18, 0);
        header.AddChild(title);
        AddTab(header, YautjaClanHallPage.Clan, "cmu-yautja-hall-tab-clan", BuildClanPage());
        AddTab(header, YautjaClanHallPage.Leaderboard, "cmu-yautja-hall-tab-boards", BuildBoardsPage());
        AddTab(header, YautjaClanHallPage.Hunter, "cmu-yautja-hall-tab-hunter", BuildHunterPage());
        AddTab(header, YautjaClanHallPage.Whitelists, "cmu-yautja-hall-tab-whitelists", BuildWhitelistPage());
        AddTab(header, YautjaClanHallPage.Round, "cmu-yautja-hall-tab-round", BuildRoundPage());
        header.AddChild(new Control { HorizontalExpand = true });
        var close = new HudButton("X", _label);
        close.Activated += Close;
        header.AddChild(close);
        root.AddChild(header);

        var area = new Control { VerticalExpand = true, HorizontalExpand = true };
        foreach (var page in _pages.Values)
            area.AddChild(page);
        root.AddChild(area);

        _status = Hud.Write(string.Empty, _small, Hud.Muted, clip: true);
        _status.HorizontalExpand = true;
        root.AddChild(_status);

        ShowPage(YautjaClanHallPage.Clan);
    }

    private void AddTab(BoxContainer header, YautjaClanHallPage page, string loc, Control content)
    {
        var button = new HudButton(Loc.GetString(loc).ToUpperInvariant(), _label);
        button.Activated += () => ShowPage(page);
        header.AddChild(button);
        _tabButtons[page] = button;

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true, Visible = false };
        content.HorizontalExpand = true;
        scroll.AddChild(content);
        _pages[page] = scroll;
    }

    private void ShowPage(YautjaClanHallPage page)
    {
        _page = page;
        foreach (var (key, control) in _pages)
            control.Visible = key == page;
        foreach (var (key, button) in _tabButtons)
            button.Selected = key == page;
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        if (relativeMousePos.X >= Size.X - ResizeGrip && relativeMousePos.Y >= Size.Y - ResizeGrip)
            return DragMode.Bottom | DragMode.Right;

        return DragMode.Move;
    }

    private void Request(EuiMessageBase message)
    {
        Send?.Invoke(message);
    }

    private void OpenClan(int clanId)
    {
        ShowPage(YautjaClanHallPage.Clan);
        if (_state?.Clan?.Id != clanId)
            Request(new YautjaClanHallShowMessage(YautjaClanHallPage.Clan, clanId));
    }

    private void OpenHunter(NetUserId hunter)
    {
        _selectedHunter = hunter;
        ShowPage(YautjaClanHallPage.Hunter);
        if (_state?.Hunter?.PlayerId != hunter)
            Request(new YautjaClanHallShowMessage(YautjaClanHallPage.Hunter, hunter: hunter));
    }

    public void UpdateState(YautjaClanHallEuiState state)
    {
        var first = _state == null;
        _state = state;

        _tabButtons[YautjaClanHallPage.Whitelists].Visible = state.Whitelist != null;
        _tabButtons[YautjaClanHallPage.Round].Visible = state.Round != null;

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

            if (page == YautjaClanHallPage.Clan && state.Clan == null)
                page = YautjaClanHallPage.Leaderboard;

            ShowPage(page);
        }

        _status.Text = state.Status;
        _status.FontColorOverride = state.StatusIsError ? Hud.Hot : Hud.Muted;
    }

    // shared bits the pages use

    private static Texture? Frame0(SpriteSpecifier specifier)
    {
        try
        {
            return IoCManager.Resolve<IEntityManager>().System<SpriteSystem>().Frame0(specifier);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static TextureRect RankIcon(YautjaRank rank, float size = 22)
    {
        var state = rank switch
        {
            YautjaRank.Unblooded => "unblooded",
            YautjaRank.YoungBlood => "youngblood",
            YautjaRank.Elite => "elite",
            YautjaRank.Elder => "elder",
            YautjaRank.Leader => "leader",
            YautjaRank.Ancient => "ancient",
            _ => "blooded",
        };

        return Icon(Frame0(new SpriteSpecifier.Rsi(RankIcons, state)), size);
    }

    private static Texture? SigilTexture(string sigil)
    {
        return Frame0(new SpriteSpecifier.Rsi(Masks / $"{YautjaClanSigils.OrDefault(sigil)}.rsi", "icon"));
    }

    private static TextureRect Icon(Texture? texture, float size)
    {
        return new TextureRect
        {
            Texture = texture,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            SetSize = new Vector2(size, size),
            TextureScale = new Vector2(size / 32f, size / 32f),
            VerticalAlignment = VAlignment.Center,
        };
    }

    private static string RankName(YautjaRank rank)
    {
        return Loc.GetString(YautjaRankMetadata.For(rank).LocalizedName);
    }

    private string ClanName(int? clanId)
    {
        if (clanId == null)
            return Loc.GetString("cmu-yautja-hall-clanless");

        return _state?.Clans.FirstOrDefault(c => c.Id == clanId)?.Name ?? Loc.GetString("cmu-yautja-hall-clan-unknown");
    }

    private static BoxContainer HBox(int separation = 6)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = separation };
    }

    private static BoxContainer VBox(int separation = 6)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = separation };
    }

    private static Control Gap()
    {
        return new Control { HorizontalExpand = true };
    }

    private HudButton Button(string loc, Action onPressed, bool confirm = false)
    {
        var button = new HudButton(Loc.GetString(loc), _small, confirm);
        button.Activated += onPressed;
        return button;
    }

    private HudFrame Frame(string? titleLoc)
    {
        return new HudFrame(titleLoc == null ? null : Loc.GetString(titleLoc), _small);
    }

    private Label Caption(string text)
    {
        var label = Hud.Write(text.ToUpperInvariant(), _small, Hud.Muted);
        label.VerticalAlignment = VAlignment.Center;
        return label;
    }

    private Control Empty(string loc)
    {
        return Hud.Write(Loc.GetString(loc), _body, Hud.Muted);
    }

    // a hunter on a list: rank icon, name, clan if asked, honor, lit dot when online. click opens them
    private Control HunterRow(YautjaClanHallHunterRow hunter, string? prefix = null, bool showClan = false, bool compact = false)
    {
        var row = new HudRow { Selected = hunter.PlayerId == _selectedHunter };
        var line = HBox(8);
        line.Margin = new Thickness(8, 3, 4, 3);
        if (prefix != null)
        {
            var number = Hud.Write(prefix, _label, Hud.Muted);
            number.MinWidth = 28;
            number.VerticalAlignment = VAlignment.Center;
            line.AddChild(number);
        }

        line.AddChild(RankIcon(hunter.Rank));
        var name = Hud.Write(hunter.Name, _bodyBold, hunter.PlayerId == _state?.Viewer.PlayerId ? Hud.Hot : Hud.Text, clip: true);
        name.HorizontalExpand = true;
        name.VerticalAlignment = VAlignment.Center;
        line.AddChild(name);
        if (showClan)
        {
            var clan = Hud.Write(ClanName(hunter.ClanId), _body, Hud.Muted, clip: true);
            clan.MinWidth = 150;
            clan.VerticalAlignment = VAlignment.Center;
            line.AddChild(clan);
        }

        // compact rows (join requests) give the room to the name and the buttons beside it
        if (!compact)
        {
            var honor = Hud.Write(hunter.Honor.ToString(), _label, Hud.Label);
            honor.MinWidth = 56;
            honor.Align = Label.AlignMode.Right;
            honor.VerticalAlignment = VAlignment.Center;
            line.AddChild(honor);
            line.AddChild(new OnlineDot(hunter.Online));
        }
        row.AddChild(line);

        var id = hunter.PlayerId;
        row.OnPressed += _ => OpenHunter(id);
        return row;
    }

    private sealed class OnlineDot : Control
    {
        private readonly bool _online;

        public OnlineDot(bool online)
        {
            _online = online;
            MinSize = new Vector2(10, 10);
            VerticalAlignment = VAlignment.Center;
            MouseFilter = MouseFilterMode.Pass;
            ToolTip = Loc.GetString(online ? "cmu-yautja-hall-online" : "cmu-yautja-hall-offline");
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            var c = PixelSize / 2;
            if (_online)
            {
                handle.DrawCircle(c, 4 * UIScale, Hud.Bright.WithAlpha(0.25f));
                handle.DrawCircle(c, 2.2f * UIScale, Hud.Bright);
            }
            else
            {
                handle.DrawCircle(c, 2.2f * UIScale, Hud.Dim, filled: false);
            }
        }
    }
}
