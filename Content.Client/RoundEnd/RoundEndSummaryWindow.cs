using System.Linq;
using System.Numerics;
using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Shared.GameTicking;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.Controls.BoxContainer;
using RoundEndPlayerInfo = Content.Shared.GameTicking.RoundEndMessageEvent.RoundEndPlayerInfo;

namespace Content.Client.RoundEnd;

/// <summary>
/// Window displaying the CMU round report and filterable player manifest.
/// </summary>
public sealed partial class RoundEndSummaryWindow : DefaultWindow
{
    private const string ManifestPlayerCardName = "RoundEndPlayerCard";

    [Dependency] private IEntityManager _entityManager = default!;

    private readonly RoundEndPlayerInfo[] _playersInfo;
    private readonly List<SortButton> _sortButtons = [];
    private BoxContainer _playerList = null!;
    private Label _manifestTitle = null!;
    private string _searchText = string.Empty;
    private SortField _currentSortField = SortField.PlayerType;
    private bool _sortDescending;

    public int RoundId;

    internal enum SortField
    {
        ICName,
        Role,
        PlayerType,
        OOCName,
    }

    public RoundEndSummaryWindow(
        string gamemode,
        string roundEnd,
        TimeSpan roundDuration,
        int roundId,
        RoundEndPlayerInfo[] playersInfo,
        RoundEndSummaryStats summaryStats)
    {
        IoCManager.InjectDependencies(this);
        _playersInfo = playersInfo;

        MinSize = new Vector2(820, 700);
        SetSize = new Vector2(900, 760);
        Title = Loc.GetString("round-end-summary-window-title");
        RoundId = roundId;

        var roundEndTabs = new TabContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        roundEndTabs.AddChild(MakeRoundEndSummaryTab(
            gamemode,
            roundEnd,
            roundDuration,
            roundId,
            playersInfo,
            summaryStats));
        roundEndTabs.AddChild(MakePlayerManifestTab());

        ContentsContainer.AddChild(roundEndTabs);
        CrtLobbyTheme.ApplyWindow(this, useCrtTypography: true);

        OpenCenteredRight();
        MoveToFront();
    }

    private BoxContainer MakePlayerManifestTab()
    {
        var playerManifestTab = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Name = Loc.GetString("round-end-summary-window-player-manifest-tab-title"),
            SeparationOverride = 10,
        };

        var controlsPanel = MakePanel(CardQuiet, Border);
        controlsPanel.Name = "ManifestControlsPanel";

        var controlsContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(12, 10),
            SeparationOverride = 8,
            HorizontalExpand = true,
        };

        var searchContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        searchContainer.AddChild(new Label
        {
            Text = Loc.GetString("round-end-summary-window-player-manifest-tab-filter"),
            VerticalAlignment = VAlignment.Center,
        });

        var searchBar = new LineEdit
        {
            Name = "ManifestSearch",
            PlaceHolder = Loc.GetString("round-end-summary-window-player-manifest-tab-search-placeholder"),
            HorizontalExpand = true,
            MinSize = new Vector2(200, 1),
        };
        searchBar.OnTextChanged += OnSearchTextChanged;
        searchContainer.AddChild(searchBar);
        controlsContainer.AddChild(searchContainer);

        var sortContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 5,
            HorizontalExpand = true,
        };

        var icNameButton = CreateSortButton(
            "round-end-summary-window-player-manifest-tab-sort-character",
            SortField.ICName);
        var roleButton = CreateSortButton(
            "round-end-summary-window-player-manifest-tab-sort-role",
            SortField.Role);
        var playerTypeButton = CreateSortButton(
            "round-end-summary-window-player-manifest-tab-sort-player-type",
            SortField.PlayerType);
        var oocNameButton = CreateSortButton(
            "round-end-summary-window-player-manifest-tab-sort-player",
            SortField.OOCName);

        playerTypeButton.SetSortIndicator(true);
        sortContainer.AddChild(icNameButton);
        sortContainer.AddChild(roleButton);
        sortContainer.AddChild(playerTypeButton);
        sortContainer.AddChild(oocNameButton);
        controlsContainer.AddChild(sortContainer);
        controlsPanel.AddChild(controlsContainer);
        playerManifestTab.AddChild(controlsPanel);

        var manifestHeader = MakePanel(CardQuiet, MarineBlue.WithAlpha(AccentBorderAlpha));
        _manifestTitle = new Label
        {
            FontColorOverride = Text,
            Margin = new Thickness(12, 10),
            StyleClasses = { StyleBase.StyleClassLabelHeading },
        };
        manifestHeader.AddChild(_manifestTitle);
        playerManifestTab.AddChild(manifestHeader);

        var scrollContainer = new ScrollContainer
        {
            VerticalExpand = true,
            Margin = new Thickness(12, 0, 12, 12),
            HScrollEnabled = false,
        };

        _playerList = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 10,
            HorizontalExpand = true,
        };

        RefreshPlayerList();
        scrollContainer.AddChild(_playerList);
        playerManifestTab.AddChild(scrollContainer);

        return playerManifestTab;
    }

    private SortButton CreateSortButton(string text, SortField field)
    {
        var button = new SortButton(Loc.GetString(text), field)
        {
            Name = $"ManifestSort{field}",
        };
        button.OnPressed += _ => SortBy(field);
        _sortButtons.Add(button);
        return button;
    }

    internal void SortBy(SortField field)
    {
        if (_currentSortField == field)
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _currentSortField = field;
            _sortDescending = false;
        }

        foreach (var button in _sortButtons)
            button.SetSortIndicator(button.Field == _currentSortField, _sortDescending);

        RefreshPlayerList();
    }

    private void RefreshPlayerList()
    {
        _playerList.RemoveAllChildren();

        var sortedPlayers = GetSortedPlayers().ToArray();
        _manifestTitle.Text = sortedPlayers.Length == _playersInfo.Length
            ? Loc.GetString("round-end-summary-window-manifest-title", ("players", _playersInfo.Length))
            : Loc.GetString(
                "round-end-summary-window-manifest-filtered-title",
                ("visible", sortedPlayers.Length),
                ("players", _playersInfo.Length));

        foreach (var playerInfo in sortedPlayers)
        {
            var card = MakePlayerCard(playerInfo);
            _playerList.AddChild(card);
            CrtLobbyTheme.Apply(card, useCrtTypography: true);
        }
    }

    private IEnumerable<RoundEndPlayerInfo> GetSortedPlayers()
    {
        var filteredPlayers = string.IsNullOrEmpty(_searchText)
            ? _playersInfo
            : _playersInfo.Where(PlayerMatchesSearch);

        static string GetIcKey(RoundEndPlayerInfo player) =>
            (player.PlayerICName ?? player.PlayerOOCName).ToLowerInvariant();

        static string GetOocKey(RoundEndPlayerInfo player) =>
            player.PlayerOOCName.ToLowerInvariant();

        static string GetRoleKey(RoundEndPlayerInfo player) =>
            GetPlayerRole(player).ToLowerInvariant();

        static int GetPlayerTypeSortKey(RoundEndPlayerInfo player) =>
            player.Antag ? 1 : player.Observer ? 3 : 2;

        return _currentSortField switch
        {
            SortField.ICName => ApplySort(filteredPlayers, GetIcKey, _sortDescending),
            SortField.OOCName => ApplySort(filteredPlayers, GetOocKey, _sortDescending),
            SortField.Role => ApplySort(filteredPlayers, GetRoleKey, _sortDescending),
            SortField.PlayerType => ApplySort(filteredPlayers, GetPlayerTypeSortKey, _sortDescending),
            _ => filteredPlayers,
        };
    }

    private static IEnumerable<RoundEndPlayerInfo> ApplySort<TKey>(
        IEnumerable<RoundEndPlayerInfo> players,
        Func<RoundEndPlayerInfo, TKey> primaryKey,
        bool descending)
    {
        static string SecondaryKey(RoundEndPlayerInfo player) =>
            (player.PlayerICName ?? player.PlayerOOCName).ToLowerInvariant();

        return descending
            ? players.OrderByDescending(primaryKey).ThenByDescending(SecondaryKey)
            : players.OrderBy(primaryKey).ThenBy(SecondaryKey);
    }

    private bool PlayerMatchesSearch(RoundEndPlayerInfo playerInfo)
    {
        if (string.IsNullOrEmpty(_searchText))
            return true;

        if (!string.IsNullOrEmpty(playerInfo.PlayerICName) &&
            playerInfo.PlayerICName.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (playerInfo.PlayerOOCName.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
            return true;

        if (playerInfo.Role.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
            GetPlayerRole(playerInfo).Contains(_searchText, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return GetPlayerTypeText(playerInfo).Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    private void OnSearchTextChanged(LineEdit.LineEditEventArgs args)
    {
        _searchText = args.Text;
        RefreshPlayerList();
    }

    private static string GetPlayerTypeText(RoundEndPlayerInfo playerInfo)
    {
        if (playerInfo.Observer)
            return Loc.GetString("round-end-summary-window-player-manifest-tab-sort-player-type-observer");

        if (playerInfo.Antag)
            return Loc.GetString("round-end-summary-window-player-manifest-tab-sort-player-type-antag");

        return Loc.GetString("round-end-summary-window-player-manifest-tab-sort-player-type-crew");
    }

    private sealed class SortButton : Button
    {
        private readonly Label _sortIndicator;

        public SortField Field { get; }

        public SortButton(string text, SortField field)
        {
            Field = field;
            HorizontalExpand = true;

            var container = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
            };

            container.AddChild(new Label
            {
                Text = text,
                HorizontalExpand = true,
            });

            _sortIndicator = new Label
            {
                HorizontalAlignment = HAlignment.Right,
                MinSize = new Vector2(15, 1),
            };
            container.AddChild(_sortIndicator);
            AddChild(container);
        }

        public void SetSortIndicator(bool active, bool descending = false)
        {
            _sortIndicator.Text = active
                ? descending ? "\u25BC" : "\u25B2"
                : string.Empty;
        }
    }
}
