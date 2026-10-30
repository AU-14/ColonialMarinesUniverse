using Content.Client.Lobby.UI.Roles;
using Content.Shared.Traits;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private bool _traitRefreshSubscribed;
    private bool _traitsNeedRefresh;
    private readonly List<(TraitCategoryPrototype? Category,
        List<(TraitPreferenceSelector Selector, bool IsLanguageTrait, string Id)> Selectors,
        Label? Counter)> _traitSelectionGroups = new();

    public void RefreshTraits()
    {
        if (!_traitRefreshSubscribed)
        {
            TabContainer.SetTabTitle(TraitsTabIndex, Loc.GetString("humanoid-profile-editor-traits-tab"));
            TabContainer.OnTabChanged += OnTraitsTabChanged;
            _traitRefreshSubscribed = true;
        }

        // Profile initialization can refresh several times before the editor is opened.
        // Keep only the latest model and build these controls when their tab is selected.
        _traitsNeedRefresh = true;
        OnTraitsTabChanged(TabContainer.CurrentTab);
    }

    private void OnTraitsTabChanged(int tab)
    {
        if (tab == TraitsTabIndex && _traitsNeedRefresh)
            RebuildTraitSelectors();
    }

    private void RefreshTraitSelections()
    {
        foreach (var (category, selectors, counter) in _traitSelectionGroups)
        {
            var selectionCount = 0;
            foreach (var (selector, _, id) in selectors)
            {
                selector.Preference = Profile?.TraitPreferences.Contains(id) == true;
                if (selector.Preference)
                    selectionCount += selector.Cost;
            }

            if (counter != null && category is { MaxTraitPoints: >= 0 })
            {
                counter.Text = Loc.GetString("humanoid-profile-editor-trait-count-hint",
                    ("current", selectionCount), ("max", category.MaxTraitPoints));
            }

            foreach (var (selector, _, _) in selectors)
            {
                selector.Checkbox.Label.FontColorOverride = category is { MaxTraitPoints: >= 0 } &&
                    selector.Cost + selectionCount > category.MaxTraitPoints ? Color.Red : null;
            }
        }
    }
}
