using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared._CMU14.Yautja;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.Yautja;

public sealed class YautjaClanInfoWindow : DefaultWindow
{
    private static readonly ResPath RankIcons = new("/Textures/_CMU14/Yautja/hud_yautja.rsi");
    private readonly BoxContainer _members;
    private readonly Label _clanLabel;
    private readonly Label _descriptionLabel;
    private readonly Label _viewerLabel;
    private readonly Label _statusLabel;
    private readonly OptionButton _clanSelector;
    private readonly LineEdit _nameInput;
    private readonly LineEdit _descriptionInput;
    private readonly LineEdit _colorInput;
    private readonly LineEdit _honorInput;
    private readonly Button _saveDescription;
    private readonly Button _saveAppearance;
    private readonly Button _saveHonor;
    private readonly Button _deleteClan;
    private int? _selectedClanId;
    private List<YautjaClanInfoOption> _clanOptions = [];

    public event Action? OnInitialize;
    public event Action? OnRefresh;
    public event Action<Robust.Shared.Network.NetUserId, YautjaRank>? OnSetRank;
    public event Action<Robust.Shared.Network.NetUserId, bool>? OnSetAncient;
    public event Action<Robust.Shared.Network.NetUserId, int?>? OnMoveMember;
    public event Action<int?>? OnSelectClan;
    public event Action<int, string>? OnUpdateDescription;
    public event Action<int, string, string>? OnUpdateAppearance;
    public event Action<int, int>? OnSetHonor;
    public event Action<Robust.Shared.Network.NetUserId>? OnPurgeMember;
    public event Action<int>? OnDeleteClan;

    public YautjaClanInfoWindow()
    {
        Title = Loc.GetString("cmu-yautja-clan-info-title");
        SetSize = MinSize = new Vector2(760, 540);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(8),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        Contents.AddChild(root);

        _clanSelector = new OptionButton { HorizontalExpand = true };
        _clanSelector.OnItemSelected += args =>
        {
            if (args.Id < 0 || args.Id >= _clanOptions.Count)
                return;

            var clanId = _clanOptions[args.Id].ClanId;
            if (clanId == _selectedClanId)
                return;

            _selectedClanId = clanId;
            OnSelectClan?.Invoke(clanId);
        };
        root.AddChild(_clanSelector);

        _clanLabel = new Label { HorizontalExpand = true };
        root.AddChild(_clanLabel);
        _descriptionLabel = new Label { HorizontalExpand = true };
        root.AddChild(_descriptionLabel);
        _viewerLabel = new Label { HorizontalExpand = true };
        root.AddChild(_viewerLabel);

        var metadata = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        _nameInput = new LineEdit { HorizontalExpand = true };
        _descriptionInput = new LineEdit { HorizontalExpand = true };
        _colorInput = new LineEdit { HorizontalExpand = true };
        _honorInput = new LineEdit { HorizontalExpand = true };
        metadata.AddChild(_nameInput);
        metadata.AddChild(_descriptionInput);
        metadata.AddChild(_colorInput);
        metadata.AddChild(_honorInput);
        root.AddChild(metadata);

        var metadataButtons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        _saveDescription = new Button { Text = Loc.GetString("cmu-yautja-clan-info-save-description") };
        _saveDescription.OnPressed += _ =>
        {
            if (_selectedClanId is { } clanId)
                OnUpdateDescription?.Invoke(clanId, _descriptionInput.Text);
        };
        metadataButtons.AddChild(_saveDescription);
        _saveAppearance = new Button { Text = Loc.GetString("cmu-yautja-clan-info-save-appearance") };
        _saveAppearance.OnPressed += _ =>
        {
            if (_selectedClanId is { } clanId)
                OnUpdateAppearance?.Invoke(clanId, _nameInput.Text, _colorInput.Text);
        };
        metadataButtons.AddChild(_saveAppearance);
        _saveHonor = new Button { Text = Loc.GetString("cmu-yautja-clan-info-save-honor") };
        _saveHonor.OnPressed += _ =>
        {
            if (_selectedClanId is { } clanId && int.TryParse(_honorInput.Text, out var honor))
                OnSetHonor?.Invoke(clanId, honor);
        };
        metadataButtons.AddChild(_saveHonor);
        _deleteClan = new Button { Text = Loc.GetString("cmu-yautja-clan-info-delete-clan") };
        _deleteClan.OnPressed += _ =>
        {
            if (_selectedClanId is { } clanId)
                OnDeleteClan?.Invoke(clanId);
        };
        metadataButtons.AddChild(_deleteClan);
        root.AddChild(metadataButtons);

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        var initialize = new Button { Text = Loc.GetString("cmu-yautja-clan-info-initialize") };
        initialize.OnPressed += _ => OnInitialize?.Invoke();
        buttons.AddChild(initialize);
        var refresh = new Button { Text = Loc.GetString("cmu-yautja-clan-info-refresh") };
        refresh.OnPressed += _ => OnRefresh?.Invoke();
        buttons.AddChild(refresh);
        root.AddChild(buttons);

        root.AddChild(new Label
        {
            Text = Loc.GetString("cmu-yautja-clan-info-members"),
            FontColorOverride = Color.LightGray,
        });

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            HScrollEnabled = false,
        };
        _members = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
        };
        scroll.AddChild(_members);
        root.AddChild(scroll);

        _statusLabel = new Label { HorizontalExpand = true };
        root.AddChild(_statusLabel);
    }

    public void UpdateState(YautjaClanInfoEuiState state)
    {
        _selectedClanId = state.ClanId;
        _clanOptions = state.AvailableClans;
        _clanSelector.Clear();
        for (var i = 0; i < _clanOptions.Count; i++)
            _clanSelector.AddItem(_clanOptions[i].Name, i);

        var selectedOption = _clanOptions.FindIndex(option => option.ClanId == state.ClanId);
        if (selectedOption >= 0)
            _clanSelector.SelectId(selectedOption);
        _clanSelector.Disabled = _clanOptions.Count < 2;

        var clanName = string.IsNullOrWhiteSpace(state.ClanName)
            ? Loc.GetString("cmu-yautja-clan-info-no-clan")
            : state.ClanName;
        _clanLabel.Text = Loc.GetString("cmu-yautja-clan-info-clan", ("clan", clanName), ("honor", state.ClanHonor));
        _descriptionLabel.Text = string.IsNullOrWhiteSpace(state.ClanDescription)
            ? Loc.GetString("cmu-yautja-clan-info-no-description")
            : state.ClanDescription;
        _nameInput.Text = state.ClanName;
        _descriptionInput.Text = state.ClanDescription;
        _colorInput.Text = state.ClanColor;
        _honorInput.Text = state.ClanHonor.ToString();
        _nameInput.Editable = state.CanEditAppearance;
        _colorInput.Editable = state.CanEditAppearance;
        _descriptionInput.Editable = state.CanEditDescription;
        _honorInput.Editable = state.CanSetHonor;
        _saveDescription.Disabled = !state.CanEditDescription;
        _saveAppearance.Disabled = !state.CanEditAppearance;
        _saveHonor.Disabled = !state.CanSetHonor;
        _deleteClan.Disabled = !state.CanDeleteClan;
        _viewerLabel.Text = Loc.GetString(
            "cmu-yautja-clan-info-viewer",
            ("rank", Loc.GetString(YautjaRankMetadata.For(state.ViewerRank).LocalizedName)),
            ("permissions", state.ViewerPermissions));
        _statusLabel.Text = state.StatusMessage;

        _members.RemoveAllChildren();
        foreach (var member in state.Members)
        {
            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 6,
                HorizontalExpand = true,
            };
            row.AddChild(new TextureRect
            {
                SetSize = new Vector2(32, 32),
                TextureScale = new Vector2(2, 2),
                VerticalAlignment = VAlignment.Center,
                Texture = IoCManager.Resolve<IEntitySystemManager>()
                    .GetEntitySystem<SpriteSystem>()
                    .Frame0(new SpriteSpecifier.Rsi(RankIcons, member.RankIconState)),
            });
            row.AddChild(new Label
            {
                Text = Loc.GetString(
                    "cmu-yautja-clan-info-member",
                    ("name", member.Name),
                    ("rank", Loc.GetString(YautjaRankMetadata.For(member.Rank).LocalizedName)),
                    ("honor", member.Honor),
                    ("online", member.Online ? "online" : "offline")),
                MinWidth = 300,
                VerticalAlignment = VAlignment.Center,
            });

            var rankButton = new OptionButton { HorizontalExpand = true };
            rankButton.OnItemSelected += args => rankButton.SelectId(args.Id);
            foreach (var rank in YautjaClanPolicy.GetNormalAssignableRanks())
            {
                rankButton.AddItem(Loc.GetString(YautjaRankMetadata.For(rank).LocalizedName), (int) rank);
            }
            var selectedRank = YautjaClanPolicy.GetNormalAssignableRanks().Contains(member.Rank)
                ? member.Rank
                : YautjaRank.Blooded;
            rankButton.SelectId((int) selectedRank);
            rankButton.Disabled = !member.CanManage;
            row.AddChild(rankButton);

            var apply = new Button
            {
                Text = Loc.GetString("cmu-yautja-clan-info-apply-rank"),
                Disabled = !member.CanManage,
            };
            apply.OnPressed += _ =>
            {
                if (Enum.IsDefined((YautjaRank) rankButton.SelectedId))
                    OnSetRank?.Invoke(member.PlayerId, (YautjaRank) rankButton.SelectedId);
            };
            row.AddChild(apply);

            if (member.CanSetAncient)
            {
                var ancient = new Button
                {
                    Text = Loc.GetString(member.Rank == YautjaRank.Ancient
                        ? "cmu-yautja-clan-info-demote-ancient"
                        : "cmu-yautja-clan-info-make-ancient"),
                };
                ancient.OnPressed += _ => OnSetAncient?.Invoke(member.PlayerId, member.Rank != YautjaRank.Ancient);
                row.AddChild(ancient);
            }

            if (member.CanMove)
            {
                var destinations = new OptionButton { HorizontalExpand = true };
                var destinationIds = new List<int?> { null };
                destinations.AddItem(Loc.GetString("cmu-yautja-clan-info-remove-member"), 0);
                for (var i = 0; i < state.AvailableClans.Count; i++)
                {
                    var option = state.AvailableClans[i];
                    if (option.ClanId == null || option.ClanId == state.ClanId)
                        continue;

                    destinationIds.Add(option.ClanId);
                    destinations.AddItem(option.Name, destinationIds.Count - 1);
                }

                var move = new Button
                {
                    Text = Loc.GetString("cmu-yautja-clan-info-move-member"),
                };
                move.OnPressed += _ =>
                {
                    var selected = destinations.SelectedId;
                    if (selected >= 0 && selected < destinationIds.Count)
                        OnMoveMember?.Invoke(member.PlayerId, destinationIds[selected]);
                };
                row.AddChild(destinations);
                row.AddChild(move);
            }

            if (state.CanPurge && member.CanMove)
            {
                var purge = new Button
                {
                    Text = Loc.GetString("cmu-yautja-clan-info-purge-member"),
                };
                purge.OnPressed += _ => OnPurgeMember?.Invoke(member.PlayerId);
                row.AddChild(purge);
            }

            _members.AddChild(row);
        }
    }
}
