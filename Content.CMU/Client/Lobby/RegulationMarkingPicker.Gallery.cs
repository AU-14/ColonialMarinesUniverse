using System.Linq;
using Content.Client.CMU14.Lobby;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Humanoid;

public sealed partial class RegulationMarkingPicker
{
    private CmuAdaptiveGrid? _workspaceMarkings;
    private CmuAdaptiveGrid? _workspaceColors;
    private Func<(bool Female, Color Skin, Color Eye)>? _workspaceAppearance;

    public void EnableWorkspaceGallery(Func<(bool Female, Color Skin, Color Eye)> appearance)
    {
        _workspaceAppearance = appearance;
        if (_workspaceMarkings != null) return;
        CmuLobbyPresentation.StyleInput(_markingSearch);
        _markingList.Visible = false;
        _colorList.Visible = false;
        _colorSearch.Visible = false;
        VerticalExpand = true;
        SeparationOverride = 10;
        _workspaceMarkings = new CmuAdaptiveGrid(190) { HorizontalExpand = true };
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true, MinHeight = 150 };
        scroll.AddChild(_workspaceMarkings);
        AddChild(scroll);
        _workspaceColors = new CmuAdaptiveGrid(140) { HorizontalExpand = true };
        AddChild(_workspaceColors);
        RefreshWorkspaceMarkings();
        RefreshWorkspaceColors();
    }

    private void RefreshWorkspaceMarkings()
    {
        if (_workspaceMarkings == null) return;
        _workspaceMarkings.RemoveAllChildren();
        var entries = _markings.Values.OrderBy(marking => Loc.GetString($"marking-{marking.ID}"));
        AddMarking(DefaultMarkingId, null);
        foreach (var marking in entries)
            if (marking.ID != DefaultMarkingId) AddMarking(marking.ID, marking);

        void AddMarking(string id, Content.Shared.Humanoid.Markings.MarkingPrototype? marking)
        {
            var text = Loc.GetString($"marking-{id}");
            if (!text.Contains(_markingSearch.Text.Trim(), StringComparison.OrdinalIgnoreCase)) return;
            var button = CmuLobbyPresentation.Action(string.Empty);
            CmuLobbyPresentation.Style(button, selected: id == _selectedMarking);
            var preview = new CmuHeadPreview(_sprite, marking?.Sprites.Select(s => _sprite.Frame0(s)).ToList() ?? [], () =>
            {
                var appearance = _workspaceAppearance!();
                return (appearance.Female, appearance.Skin, appearance.Eye, _selectedColor);
            });
            CmuLobbyPresentation.SetMarkingCardContent(button, preview, text);
            button.OnPressed += _ =>
            {
                _selectedMarking = id;
                OnMarkingChanged?.Invoke(id);
                RefreshWorkspaceMarkings();
                RefreshWorkspaceColors();
            };
            _workspaceMarkings.AddChild(button);
        }
    }

    private void RefreshWorkspaceColors()
    {
        if (_workspaceColors == null) return;
        _colorSearch.Visible = false;
        _colorList.Visible = false;
        _workspaceColors.RemoveAllChildren();
        _workspaceColors.Visible = _selectedMarking != DefaultMarkingId;
        foreach (var (name, color) in DropdownColors)
        {
            var button = CmuLobbyPresentation.Action(GetColorDisplayName(name), color);
            button.MinWidth = 130;
            CmuLobbyPresentation.Style(button, color, color == _selectedColor);
            button.OnPressed += _ =>
            {
                _selectedColor = color;
                OnColorChanged?.Invoke(color);
                RefreshWorkspaceColors();
            };
            _workspaceColors.AddChild(button);
        }
    }
}
