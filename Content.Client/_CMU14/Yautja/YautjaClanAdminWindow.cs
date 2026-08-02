using System;
using System.Linq;
using System.Numerics;
using Content.Client._RMC14.UserInterface;
using Content.Shared._CMU14.Yautja;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Network;

namespace Content.Client._CMU14.Yautja;

public sealed class YautjaClanAdminWindow : DefaultWindow
{
    public static readonly Vector2 DefaultWindowSize = new(760, 560);
    public const int RosterMaxHeight = 180;
    public const int ClanlessMaxHeight = 160;

    private readonly LineEdit _clanName;
    private readonly LineEdit _clanDescription;
    private readonly LineEdit _clanColor;
    private readonly Label _clanFormHeader;
    private readonly Button _submitClan;
    private readonly Button _cancelClan;
    private readonly LineEdit _player;
    private readonly LineEdit _clanId;
    private readonly OptionButton _membershipRank;
    private readonly OptionButton _rank;
    private readonly OptionButton _whitelist;
    private readonly BoxContainer _clans;
    private readonly Label _inspection;
    private readonly Label _status;
    private readonly YautjaClanAdminEditorState _editor = new();
    private ConfirmationWindow? _deleteConfirmation;
    private int? _expandedClanId;

    private static readonly YautjaRank[] PersistentRanks =
        YautjaClanPolicy.GetNormalAssignableRanks().Append(YautjaRank.Ancient).ToArray();

    private static readonly (YautjaWhitelistFlags Flag, string LocalizationKey)[] WhitelistDisplayFlags =
    [
        (YautjaWhitelistFlags.Yautja, "cmu-yautja-clan-admin-whitelist-yautja"),
        (YautjaWhitelistFlags.Legacy, "cmu-yautja-clan-admin-whitelist-legacy"),
        (YautjaWhitelistFlags.Council, "cmu-yautja-clan-admin-whitelist-council"),
        (YautjaWhitelistFlags.CouncilLegacy, "cmu-yautja-clan-admin-whitelist-council-legacy"),
        (YautjaWhitelistFlags.Leader, "cmu-yautja-clan-admin-whitelist-leader"),
    ];

    public event Action? OnRefresh;
    public event Action<string, string, string>? OnCreateClan;
    public event Action<int, string, string, string>? OnUpdateClan;
    public event Action<int>? OnDeleteClan;
    public event Action<NetUserId>? OnRemoveMember;
    public event Action<NetUserId>? OnClearWhitelist;
    public event Action<string, string, YautjaRank>? OnSetMembership;
    public event Action<string, YautjaRank>? OnSetRank;
    public event Action<string, YautjaWhitelistFlags>? OnSetWhitelist;
    public event Action<string>? OnInspect;

    public YautjaClanAdminWindow()
    {
        Title = Loc.GetString("cmu-yautja-clan-admin-title");
        Resizable = true;
        SetSize = DefaultWindowSize;
        MinSize = new Vector2(680, 500);

        var windowScroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
        };
        Contents.AddChild(windowScroll);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Margin = new Thickness(6),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        windowScroll.AddChild(root);

        var leftPane = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            MinWidth = 340,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        root.AddChild(leftPane);

        var rightPane = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            MinWidth = 340,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        root.AddChild(rightPane);

        var clanSection = YautjaBracerUiStyle.Section(
            Loc.GetString("cmu-yautja-clan-admin-section-clan"),
            out var clanBody,
            YautjaBracerUiStyle.HotRed);
        clanSection.VerticalExpand = false;
        clanBody.Margin = new Thickness(7);
        _clanFormHeader = CreateHeader("cmu-yautja-clan-admin-create-header");
        clanBody.AddChild(_clanFormHeader);
        _clanName = CreateLineEdit(
            "cmu-yautja-clan-admin-name",
            "cmu-yautja-clan-admin-name-tooltip");
        clanBody.AddChild(CreateField("cmu-yautja-clan-admin-name", _clanName));
        _clanDescription = CreateLineEdit(
            "cmu-yautja-clan-admin-description",
            "cmu-yautja-clan-admin-description-tooltip");
        clanBody.AddChild(CreateField("cmu-yautja-clan-admin-description", _clanDescription));
        _clanColor = CreateLineEdit(
            "cmu-yautja-clan-admin-color",
            "cmu-yautja-clan-admin-color-tooltip");
        clanBody.AddChild(CreateField("cmu-yautja-clan-admin-color", _clanColor));

        var clanActions = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        _submitClan = CreateButton(
            "cmu-yautja-clan-admin-create",
            "cmu-yautja-clan-admin-submit-tooltip");
        _submitClan.OnPressed += _ => SubmitClan();
        clanActions.AddChild(_submitClan);
        _cancelClan = CreateButton(
            "cmu-yautja-clan-admin-cancel",
            "cmu-yautja-clan-admin-cancel-tooltip");
        _cancelClan.MinWidth = 110;
        _cancelClan.OnPressed += _ =>
        {
            _editor.Cancel();
            SyncEditorControls();
        };
        clanActions.AddChild(_cancelClan);
        clanBody.AddChild(clanActions);
        leftPane.AddChild(clanSection);
        SyncEditorControls();

        var playerSection = YautjaBracerUiStyle.Section(
            Loc.GetString("cmu-yautja-clan-admin-section-player"),
            out var playerBody,
            YautjaBracerUiStyle.Amber);
        playerSection.VerticalExpand = false;
        playerBody.Margin = new Thickness(7);
        _player = CreateLineEdit(
            "cmu-yautja-clan-admin-player",
            "cmu-yautja-clan-admin-player-tooltip");
        playerBody.AddChild(CreateField("cmu-yautja-clan-admin-player", _player));

        var membership = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        _clanId = CreateLineEdit(
            "cmu-yautja-clan-admin-clan-id",
            "cmu-yautja-clan-admin-clan-id-tooltip");
        membership.AddChild(CreateField("cmu-yautja-clan-admin-clan-id", _clanId));
        _membershipRank = CreateRankOption();
        membership.AddChild(CreateField("cmu-yautja-clan-admin-membership-rank", _membershipRank));
        var setMembership = CreateButton(
            "cmu-yautja-clan-admin-set-membership",
            "cmu-yautja-clan-admin-set-membership-tooltip");
        setMembership.MinWidth = 135;
        setMembership.OnPressed += _ => OnSetMembership?.Invoke(
            _player.Text,
            _clanId.Text,
            (YautjaRank) _membershipRank.SelectedId);
        membership.AddChild(setMembership);
        playerBody.AddChild(membership);

        var rankRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        _rank = CreateRankOption();
        rankRow.AddChild(CreateField("cmu-yautja-clan-admin-set-rank", _rank));
        var setRank = CreateButton(
            "cmu-yautja-clan-admin-set-rank",
            "cmu-yautja-clan-admin-set-rank-tooltip");
        setRank.MinWidth = 125;
        setRank.OnPressed += _ => OnSetRank?.Invoke(_player.Text, (YautjaRank) _rank.SelectedId);
        rankRow.AddChild(setRank);
        playerBody.AddChild(rankRow);

        var whitelistRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        _whitelist = new OptionButton { HorizontalExpand = true };
        AddWhitelistOptions(_whitelist);
        whitelistRow.AddChild(CreateField("cmu-yautja-clan-admin-set-whitelist", _whitelist));
        var setWhitelist = CreateButton(
            "cmu-yautja-clan-admin-set-whitelist",
            "cmu-yautja-clan-admin-set-whitelist-tooltip");
        setWhitelist.MinWidth = 125;
        setWhitelist.OnPressed += _ => OnSetWhitelist?.Invoke(_player.Text, (YautjaWhitelistFlags) _whitelist.SelectedId);
        whitelistRow.AddChild(setWhitelist);
        playerBody.AddChild(whitelistRow);

        var inspect = CreateButton(
            "cmu-yautja-clan-admin-inspect",
            "cmu-yautja-clan-admin-inspect-tooltip");
        inspect.OnPressed += _ => OnInspect?.Invoke(_player.Text);
        playerBody.AddChild(inspect);

        _inspection = new Label
        {
            HorizontalExpand = true,
            FontColorOverride = YautjaBracerUiStyle.Muted,
        };
        playerBody.AddChild(YautjaBracerUiStyle.Wrap(
            _inspection,
            YautjaBracerUiStyle.DeepCard,
            YautjaBracerUiStyle.MutedBorder,
            new Thickness(5, 3)));
        leftPane.AddChild(playerSection);

        var clansSection = YautjaBracerUiStyle.Section(
            Loc.GetString("cmu-yautja-clan-admin-section-existing"),
            out var clansBody,
            YautjaBracerUiStyle.HotRed);
        clansSection.VerticalExpand = true;
        clansBody.Margin = new Thickness(7);
        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            HScrollEnabled = false,
        };
        _clans = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
        };
        scroll.AddChild(_clans);
        clansBody.AddChild(scroll);

        var bottom = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        _status = new Label { HorizontalExpand = true };
        bottom.AddChild(_status);
        var refresh = CreateButton(
            "cmu-yautja-clan-admin-refresh",
            "cmu-yautja-clan-admin-refresh-tooltip");
        refresh.MinWidth = 110;
        refresh.OnPressed += _ => OnRefresh?.Invoke();
        bottom.AddChild(refresh);
        clansBody.AddChild(bottom);
        rightPane.AddChild(clansSection);
    }

    public void UpdateState(YautjaClanAdminEuiState state)
    {
        _editor.CaptureDraft(_clanName.Text, _clanDescription.Text, _clanColor.Text);
        _editor.ApplyState(state);
        SyncEditorControls();

        _inspection.Text = string.IsNullOrWhiteSpace(state.InspectedSummary)
            ? Loc.GetString("cmu-yautja-clan-admin-no-inspection")
            : $"{state.InspectedPlayer}: {state.InspectedSummary}";
        _status.Text = state.StatusMessage;

        if (_expandedClanId is { } expandedClanId && !state.Clans.Any(clan => clan.Id == expandedClanId))
            _expandedClanId = null;

        _clans.RemoveAllChildren();
        foreach (var clan in state.Clans)
            _clans.AddChild(BuildClanCard(clan, state));
        _clans.AddChild(BuildClanlessSection(state.ClanlessPlayers));
    }

    private Control BuildClanCard(YautjaClanAdminClanState clan, YautjaClanAdminEuiState state)
    {
        var card = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
        };
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        var info = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        info.AddChild(new Label
        {
            Text = Loc.GetString(
                "cmu-yautja-clan-admin-clan-row-title",
                ("id", clan.Id),
                ("name", clan.Name)),
            FontColorOverride = YautjaBracerUiStyle.Text,
            HorizontalExpand = true,
        });
        info.AddChild(new Label
        {
            Text = Loc.GetString(
                "cmu-yautja-clan-admin-clan-row-meta",
                ("members", clan.MemberCount),
                ("honor", clan.Honor),
                ("color", clan.Color)),
            FontColorOverride = YautjaBracerUiStyle.Muted,
            HorizontalExpand = true,
        });
        row.AddChild(info);

        var edit = CreateButton(
            "cmu-yautja-clan-admin-edit",
            "cmu-yautja-clan-admin-edit-tooltip");
        edit.MinWidth = 120;
        edit.HorizontalExpand = false;
        edit.StyleBoxOverride = YautjaBracerUiStyle.Flat(
            YautjaBracerUiStyle.DeepCard,
            YautjaBracerUiStyle.MutedBorder);
        edit.OnPressed += _ =>
        {
            _editor.BeginEdit(clan);
            SyncEditorControls();
        };
        row.AddChild(edit);

        var delete = CreateButton(
            "cmu-yautja-clan-admin-delete",
            "cmu-yautja-clan-admin-delete-tooltip");
        delete.MinWidth = 100;
        delete.HorizontalExpand = false;
        delete.StyleBoxOverride = YautjaBracerUiStyle.Flat(
            YautjaBracerUiStyle.DeepCard,
            YautjaBracerUiStyle.HotRed);
        delete.OnPressed += _ => OpenDeleteConfirmation(clan);
        row.AddChild(delete);
        card.AddChild(row);

        var rosterToggle = CreateButton(
            _expandedClanId == clan.Id
                ? "cmu-yautja-clan-admin-roster-collapse"
                : "cmu-yautja-clan-admin-roster-expand",
            "cmu-yautja-clan-admin-roster-tooltip");
        rosterToggle.MinHeight = 26;
        rosterToggle.OnPressed += _ =>
        {
            _expandedClanId = ToggleExpandedClan(_expandedClanId, clan.Id);
            UpdateState(state);
        };
        card.AddChild(rosterToggle);

        if (_expandedClanId == clan.Id)
            card.AddChild(BuildRoster(clan));

        return YautjaBracerUiStyle.Wrap(
            card,
            YautjaBracerUiStyle.DeepCard,
            YautjaBracerUiStyle.MutedBorder,
            new Thickness(6, 4));
    }

    private Control BuildRoster(YautjaClanAdminClanState clan)
    {
        var scroll = CreateBoundedRosterScroll(RosterMaxHeight);
        var roster = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
            HorizontalExpand = true,
        };
        if (clan.Members.Count == 0)
        {
            roster.AddChild(new Label
            {
                Text = Loc.GetString("cmu-yautja-clan-admin-roster-empty"),
                FontColorOverride = YautjaBracerUiStyle.Muted,
                HorizontalExpand = true,
            });
        }
        else
        {
            foreach (var member in clan.Members)
                roster.AddChild(BuildMemberRow(member, true));
        }

        scroll.AddChild(roster);
        return scroll;
    }

    private Control BuildClanlessSection(List<YautjaClanAdminMemberState> players)
    {
        var section = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
        };
        section.AddChild(new Label
        {
            Text = Loc.GetString("cmu-yautja-clan-admin-clanless-header"),
            FontColorOverride = YautjaBracerUiStyle.Amber,
            HorizontalExpand = true,
        });

        var scroll = CreateBoundedRosterScroll(ClanlessMaxHeight);
        var roster = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
            HorizontalExpand = true,
        };
        if (players.Count == 0)
        {
            roster.AddChild(new Label
            {
                Text = Loc.GetString("cmu-yautja-clan-admin-clanless-empty"),
                FontColorOverride = YautjaBracerUiStyle.Muted,
                HorizontalExpand = true,
            });
        }
        else
        {
            foreach (var player in players)
                roster.AddChild(BuildMemberRow(player, false));
        }

        scroll.AddChild(roster);
        section.AddChild(scroll);
        return YautjaBracerUiStyle.Wrap(
            section,
            YautjaBracerUiStyle.DeepCard,
            YautjaBracerUiStyle.MutedBorder,
            new Thickness(6, 4));
    }

    private Control BuildMemberRow(YautjaClanAdminMemberState member, bool includeRemove)
    {
        var rank = Loc.GetString(YautjaRankMetadata.For(member.Rank).LocalizedName);
        var status = Loc.GetString(member.Online
            ? "cmu-yautja-clan-admin-roster-online"
            : "cmu-yautja-clan-admin-roster-offline");
        var whitelist = GetWhitelistLabel(member.WhitelistFlags);
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            HorizontalExpand = true,
        };
        row.AddChild(new Label
        {
            Text = Loc.GetString(
                "cmu-yautja-clan-admin-roster-member",
                ("name", member.Name),
                ("rank", rank),
                ("status", status)) + " · " + Loc.GetString(
                    "cmu-yautja-clan-admin-roster-whitelist",
                    ("flags", whitelist)),
            FontColorOverride = member.Online
                ? YautjaBracerUiStyle.Text
                : YautjaBracerUiStyle.Muted,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        });

        if (includeRemove)
        {
            var remove = CreateButton(
                "cmu-yautja-clan-admin-remove-member",
                "cmu-yautja-clan-admin-remove-member-tooltip");
            remove.MinWidth = 105;
            remove.MinHeight = 24;
            remove.HorizontalExpand = false;
            remove.StyleBoxOverride = YautjaBracerUiStyle.Flat(
                YautjaBracerUiStyle.DeepCard,
                YautjaBracerUiStyle.HotRed);
            remove.OnPressed += _ => OnRemoveMember?.Invoke(GetRosterActionTarget(member));
            row.AddChild(remove);
        }

        var clearWhitelist = CreateButton(
            "cmu-yautja-clan-admin-clear-whitelist",
            "cmu-yautja-clan-admin-clear-whitelist-tooltip");
        clearWhitelist.MinWidth = 105;
        clearWhitelist.MinHeight = 24;
        clearWhitelist.Disabled = !CanClearWhitelist(member.WhitelistFlags);
        clearWhitelist.HorizontalExpand = false;
        clearWhitelist.StyleBoxOverride = YautjaBracerUiStyle.Flat(
            YautjaBracerUiStyle.Row,
            YautjaBracerUiStyle.MutedBorder);
        clearWhitelist.OnPressed += _ => OnClearWhitelist?.Invoke(GetRosterActionTarget(member));
        row.AddChild(clearWhitelist);

        return YautjaBracerUiStyle.Wrap(
            row,
            YautjaBracerUiStyle.Row,
            YautjaBracerUiStyle.MutedBorder,
            new Thickness(5, 3));
    }

    internal static NetUserId GetRosterActionTarget(YautjaClanAdminMemberState member)
    {
        return member.PlayerId;
    }

    internal static bool CanClearWhitelist(YautjaWhitelistFlags flags)
    {
        return flags != YautjaWhitelistFlags.None;
    }

    internal static string GetWhitelistLabel(YautjaWhitelistFlags flags)
    {
        if (flags == YautjaWhitelistFlags.None)
            return Loc.GetString("cmu-yautja-clan-admin-whitelist-none");

        var labels = WhitelistDisplayFlags
            .Where(entry => (flags & entry.Flag) != 0)
            .Select(entry => Loc.GetString(entry.LocalizationKey))
            .ToArray();
        return labels.Length == 0 ? flags.ToString() : string.Join(", ", labels);
    }

    internal static ScrollContainer CreateBoundedRosterScroll(int maxHeight)
    {
        return new ScrollContainer
        {
            MaxHeight = maxHeight,
            ReturnMeasure = true,
            HorizontalExpand = true,
            HScrollEnabled = false,
        };
    }

    internal static int? ToggleExpandedClan(int? expandedClanId, int clanId)
    {
        return expandedClanId == clanId ? null : clanId;
    }

    [System.Obsolete]
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _deleteConfirmation?.Close();

        base.Dispose(disposing);
    }

    private void SubmitClan()
    {
        if (_editor.EditingClanId is { } clanId)
        {
            OnUpdateClan?.Invoke(clanId, _clanName.Text, _clanDescription.Text, _clanColor.Text);
            return;
        }

        OnCreateClan?.Invoke(_clanName.Text, _clanDescription.Text, _clanColor.Text);
    }

    private void SyncEditorControls()
    {
        _clanName.Text = _editor.Name;
        _clanDescription.Text = _editor.Description;
        _clanColor.Text = _editor.Color;
        _clanFormHeader.Text = Loc.GetString(_editor.IsEditing
            ? "cmu-yautja-clan-admin-edit-header"
            : "cmu-yautja-clan-admin-create-header");
        _submitClan.Text = Loc.GetString(_editor.IsEditing
            ? "cmu-yautja-clan-admin-save"
            : "cmu-yautja-clan-admin-create");
        _cancelClan.Visible = _editor.IsEditing;
    }

    private void OpenDeleteConfirmation(YautjaClanAdminClanState clan)
    {
        _deleteConfirmation?.Close();

        var confirmation = new ConfirmationWindow();
        _deleteConfirmation = confirmation;
        confirmation.Setup(
            Loc.GetString("cmu-yautja-clan-admin-delete-title"),
            Loc.GetString("cmu-yautja-clan-admin-delete-text", ("name", clan.Name)),
            Loc.GetString("cmu-yautja-clan-admin-delete-accept"),
            Loc.GetString("cmu-yautja-clan-admin-delete-deny"));
        confirmation.OnClose += () =>
        {
            if (_deleteConfirmation == confirmation)
                _deleteConfirmation = null;
        };
        confirmation.DenyButton.OnPressed += _ => confirmation.Close();
        confirmation.AcceptButton.OnPressed += _ =>
        {
            OnDeleteClan?.Invoke(clan.Id);
            confirmation.Close();
        };
        confirmation.OpenCentered();
    }

    private static Label CreateHeader(string localization)
    {
        var label = YautjaBracerUiStyle.Label(
            Loc.GetString(localization),
            YautjaBracerUiStyle.HotRed,
            "LabelHeading");
        label.HorizontalExpand = true;
        return label;
    }

    private static Label CreateHint(string localization)
    {
        return new Label
        {
            Text = Loc.GetString(localization),
            FontColorOverride = YautjaBracerUiStyle.Muted,
            HorizontalExpand = true,
            ClipText = false,
        };
    }

    private static BoxContainer CreateField(string localization, Control control)
    {
        control.HorizontalExpand = true;
        var field = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
            HorizontalExpand = true,
        };
        field.AddChild(new Label
        {
            Text = Loc.GetString(localization),
            FontColorOverride = YautjaBracerUiStyle.Muted,
            HorizontalExpand = true,
        });
        field.AddChild(control);
        return field;
    }

    private static LineEdit CreateLineEdit(string localization, string tooltipLocalization)
    {
        var line = new LineEdit
        {
            PlaceHolder = Loc.GetString(localization),
            HorizontalExpand = true,
        };
        ApplyTooltip(line, tooltipLocalization);
        return line;
    }

    private static Button CreateButton(string localization, string tooltipLocalization)
    {
        var button = new Button
        {
            Text = Loc.GetString(localization),
            HorizontalExpand = true,
            MinHeight = 28,
        };
        ApplyTooltip(button, tooltipLocalization);
        return button;
    }

    internal static void ApplyTooltip(Control control, string localization)
    {
        control.ToolTip = Loc.GetString(localization);
    }

    private static OptionButton CreateRankOption()
    {
        var option = new OptionButton { HorizontalExpand = true };
        option.OnItemSelected += args => ApplySelectorSelection(option, args);
        foreach (var rank in PersistentRanks)
        {
            option.AddItem(Loc.GetString(YautjaRankMetadata.For(rank).LocalizedName), (int) rank);
        }

        option.SelectId((int) YautjaRank.Blooded);
        return option;
    }

    private static void AddWhitelistOptions(OptionButton option)
    {
        option.OnItemSelected += args => ApplySelectorSelection(option, args);
        option.AddItem(Loc.GetString("cmu-yautja-clan-admin-whitelist-none"), (int) YautjaWhitelistFlags.None);
        option.AddItem(Loc.GetString("cmu-yautja-clan-admin-whitelist-yautja"), (int) YautjaWhitelistFlags.Yautja);
        option.AddItem(Loc.GetString("cmu-yautja-clan-admin-whitelist-legacy"), (int) YautjaWhitelistFlags.Legacy);
        option.AddItem(Loc.GetString("cmu-yautja-clan-admin-whitelist-council"), (int) YautjaWhitelistFlags.Council);
        option.AddItem(Loc.GetString("cmu-yautja-clan-admin-whitelist-council-legacy"), (int) YautjaWhitelistFlags.CouncilLegacy);
        option.AddItem(Loc.GetString("cmu-yautja-clan-admin-whitelist-leader"), (int) YautjaWhitelistFlags.Leader);
        option.SelectId((int) YautjaWhitelistFlags.None);
    }

    internal static void ApplySelectorSelection(OptionButton option, OptionButton.ItemSelectedEventArgs args)
    {
        option.SelectId(args.Id);
    }
}
