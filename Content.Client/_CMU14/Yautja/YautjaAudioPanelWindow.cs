using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using Content.Shared._CMU14.Yautja;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._CMU14.Yautja;

public sealed class YautjaAudioPanelWindow : DefaultWindow
{
    private readonly Label _cooldownLabel;
    private readonly BoxContainer _tabs;
    private readonly BoxContainer _entries;
    private YautjaAudioPanelState? _state;
    private string? _selectedCategory;

    public event Action<string>? OnEmote;

    public YautjaAudioPanelWindow()
    {
        Title = Loc.GetString("cmu-yautja-audio-panel-title");
        SetSize = new Vector2(420, 380);
        MinSize = new Vector2(360, 320);

        var rootPanel = YautjaBracerUiStyle.Panel(YautjaBracerUiStyle.Surface, YautjaBracerUiStyle.Border, new Thickness(2));
        AddChild(rootPanel);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 7,
            Margin = new Thickness(7),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        rootPanel.AddChild(root);

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 7,
            HorizontalExpand = true,
        };
        root.AddChild(YautjaBracerUiStyle.Wrap(header, YautjaBracerUiStyle.DeepCard, YautjaBracerUiStyle.MutedBorder, new Thickness(7, 5)));

        var titleColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        header.AddChild(titleColumn);
        titleColumn.AddChild(YautjaBracerUiStyle.Label(Loc.GetString("cmu-yautja-audio-panel-header"), YautjaBracerUiStyle.HotRed, "LabelHeading"));

        _cooldownLabel = YautjaBracerUiStyle.Label(string.Empty, YautjaBracerUiStyle.Muted, "LabelSubText");
        titleColumn.AddChild(_cooldownLabel);

        var close = YautjaBracerUiStyle.CloseButton();
        close.OnPressed += _ => Close();
        header.AddChild(close);

        _tabs = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 5,
            HorizontalExpand = true,
        };
        root.AddChild(_tabs);

        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        root.AddChild(scroll);

        _entries = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 7,
            HorizontalExpand = true,
        };
        scroll.AddChild(_entries);
    }

    public void UpdateState(YautjaAudioPanelState state)
    {
        _state = state;
        _cooldownLabel.Text = state.CooldownRemaining > TimeSpan.Zero
            ? Loc.GetString("cmu-yautja-audio-panel-cooldown", ("seconds", MathF.Ceiling((float) state.CooldownRemaining.TotalSeconds)))
            : Loc.GetString("cmu-yautja-audio-panel-ready");
        _cooldownLabel.FontColorOverride = state.CooldownRemaining > TimeSpan.Zero
            ? YautjaBracerUiStyle.Amber
            : YautjaBracerUiStyle.Muted;

        var categories = state.Entries
            .Select(entry => entry.Category)
            .Distinct()
            .ToList();

        if (_selectedCategory == null || !categories.Contains(_selectedCategory))
            _selectedCategory = categories.FirstOrDefault();

        RebuildTabs(categories);
        RebuildEntries();
    }

    private void RebuildTabs(IReadOnlyList<string> categories)
    {
        _tabs.DisposeAllChildren();
        foreach (var category in categories)
        {
            var selected = category == _selectedCategory;
            var button = new Button
            {
                Text = category,
                HorizontalExpand = true,
                MinHeight = 30,
                StyleBoxOverride = YautjaBracerUiStyle.Flat(
                    selected ? YautjaBracerUiStyle.Row : YautjaBracerUiStyle.DeepCard,
                    selected ? YautjaBracerUiStyle.HotRed : YautjaBracerUiStyle.MutedBorder),
            };

            button.OnPressed += _ =>
            {
                _selectedCategory = category;
                RebuildTabs(categories);
                RebuildEntries();
            };

            _tabs.AddChild(button);
        }
    }

    private void RebuildEntries()
    {
        _entries.DisposeAllChildren();
        if (_state == null || _selectedCategory == null)
            return;

        foreach (var entry in _state.Entries.Where(entry => entry.Category == _selectedCategory))
        {
            var button = new Button
            {
                Text = entry.Name,
                HorizontalExpand = true,
                MinHeight = 34,
                StyleBoxOverride = YautjaBracerUiStyle.Flat(YautjaBracerUiStyle.DeepCard, YautjaBracerUiStyle.MutedBorder),
            };
            var emoteId = entry.EmoteId;
            button.OnPressed += _ => OnEmote?.Invoke(emoteId);
            _entries.AddChild(button);
        }
    }
}
