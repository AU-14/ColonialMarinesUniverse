using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Yautja.ClanHall;
using Content.Shared.CMU14.Yautja;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Yautja;

// the mask's sound board. categories across the top, sounds as a two-column board, the recharge in the header
public sealed class YautjaAudioPanelWindow : YautjaHudWindow
{
    private readonly Label _cooldownLabel;
    private readonly BoxContainer _tabs;
    private readonly GridContainer _entries;
    private YautjaAudioPanelState? _state;
    private string? _selectedCategory;
    private bool _recharging;

    public event Action<string>? OnEmote;

    public YautjaAudioPanelWindow()
        : base(Loc.GetString("cmu-yautja-audio-panel-title"), new Vector2(520, 420), new Vector2(420, 320))
    {
        _cooldownLabel = Reading(Hud.Muted);
        Header.AddChild(_cooldownLabel);
        AddClose();

        _tabs = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        Body.AddChild(_tabs);

        var scroll = new ScrollContainer { HScrollEnabled = false, HorizontalExpand = true, VerticalExpand = true };
        _entries = new GridContainer
        {
            Columns = 2,
            HSeparationOverride = 6,
            VSeparationOverride = 6,
            HorizontalExpand = true,
        };
        scroll.AddChild(_entries);
        Body.AddChild(scroll);
    }

    public void UpdateState(YautjaAudioPanelState state)
    {
        _state = state;
        // the state only arrives on change, so the buttons never lock: the server turns down an early press
        _recharging = state.CooldownRemaining > TimeSpan.Zero;
        _cooldownLabel.Text = (_recharging
            ? Loc.GetString("cmu-yautja-audio-panel-cooldown", ("seconds", MathF.Ceiling((float) state.CooldownRemaining.TotalSeconds)))
            : Loc.GetString("cmu-yautja-audio-panel-ready")).ToUpperInvariant();
        _cooldownLabel.FontColorOverride = _recharging ? Hud.Hot : Hud.Text;

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
        _tabs.RemoveAllChildren();
        foreach (var category in categories)
        {
            var button = new HudButton(category.ToUpperInvariant(), SmallFont)
            {
                Selected = category == _selectedCategory,
            };

            button.Activated += () =>
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
        _entries.RemoveAllChildren();
        if (_state == null || _selectedCategory == null)
            return;

        foreach (var entry in _state.Entries.Where(entry => entry.Category == _selectedCategory))
        {
            var button = new HudButton(entry.Name, BodyFont)
            {
                HorizontalExpand = true,
                MinHeight = 30,
            };
            var emoteId = entry.EmoteId;
            button.Activated += () => OnEmote?.Invoke(emoteId);
            _entries.AddChild(button);
        }
    }
}
