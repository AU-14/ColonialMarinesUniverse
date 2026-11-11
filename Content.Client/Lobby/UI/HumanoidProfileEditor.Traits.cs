using System.Linq;
using Content.Client.Lobby.UI.Roles;
using Content.Client.Stylesheets;
using Content.Shared.Traits;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private static readonly ProtoId<TraitCategoryPrototype> SpeechTraitsCategory = "SpeechTraits";

    // CMU14 method: build on demand and retain selectors for selection-only updates.
    private void RebuildTraitSelectors()
    {
        _traitsNeedRefresh = false;
        _traitSelectionGroups.Clear();
        TraitsList.ReleaseChildren();

        var traits = _prototypeManager.EnumeratePrototypes<TraitPrototype>().OrderBy(t => Loc.GetString(t.Name)).ToList();
        TabContainer.SetTabTitle(TraitsTabIndex, Loc.GetString("humanoid-profile-editor-traits-tab"));

        if (traits.Count < 1)
        {
            TraitsList.AddChild(new Label
            {
                Text = Loc.GetString("humanoid-profile-editor-no-traits"),
                FontColorOverride = Color.Gray,
            });
            return;
        }

        // Setup model
        Dictionary<string, List<string>> traitGroups = new();
        List<string> defaultTraits = new();
        traitGroups.Add(TraitCategoryPrototype.Default, defaultTraits);

        foreach (var trait in traits)
        {
            if (trait.Category == null)
            {
                defaultTraits.Add(trait.ID);
                continue;
            }

            if (!_prototypeManager.HasIndex(trait.Category))
                continue;

            var group = traitGroups.GetOrNew(trait.Category);
            group.Add(trait.ID);
        }

        // Create UI view from model
        foreach (var (categoryId, categoryTraits) in traitGroups)
        {
            TraitCategoryPrototype? category = null;

            if (categoryId != TraitCategoryPrototype.Default)
            {
                category = _prototypeManager.Index<TraitCategoryPrototype>(categoryId);
                // Label
                TraitsList.AddChild(new Label
                {
                    Text = Loc.GetString(category.Name),
                    Margin = new Thickness(0, 10, 0, 0),
                    StyleClasses = { StyleClass.LabelHeading },
                });
            }

            var selectors = new List<(TraitPreferenceSelector Selector, bool IsLanguageTrait, string Id)>();
            var selectionCount = 0;

            foreach (var traitProto in categoryTraits)
            {
                var trait = _prototypeManager.Index<TraitPrototype>(traitProto);
                var selector = new TraitPreferenceSelector(trait);

                selector.Preference = Profile?.TraitPreferences.Contains(trait.ID) == true;
                if (selector.Preference)
                    selectionCount += trait.Cost;

                selector.PreferenceChanged += preference =>
                {
                    if (preference)
                    {
                        Profile = Profile?.WithTraitPreference(trait.ID, _prototypeManager);
                    }
                    else
                    {
                        Profile = Profile?.WithoutTraitPreference(trait.ID, _prototypeManager);
                    }

                    SetDirty();
                    // Restore rejected selections and counters without rebuilding the control tree.
                    RefreshTraitSelections();
                };
                selectors.Add((selector, trait.Language != null, trait.ID));
            }

            // Selection counter
            // Retain the counter alongside its selectors for selection-only updates.
            Label? counter = null;
            if (category is { MaxTraitPoints: >= 0 })
            {
                counter = new Label
                {
                    Text = Loc.GetString("humanoid-profile-editor-trait-count-hint", ("current", selectionCount), ("max", category.MaxTraitPoints)),
                    FontColorOverride = Color.Gray
                };
                TraitsList.AddChild(counter);
            }
            _traitSelectionGroups.Add((category, selectors, counter));

            if (categoryId == SpeechTraitsCategory)
            {
                AddTraitSelectors(
                    selectors.Where(entry => entry.IsLanguageTrait).Select(entry => entry.Selector).ToList(),
                    selectionCount,
                    category,
                    "rmc-trait-group-languages");
                AddTraitSelectors(
                    selectors.Where(entry => !entry.IsLanguageTrait).Select(entry => entry.Selector).ToList(),
                    selectionCount,
                    category,
                    "rmc-trait-group-other-speech");
            }
            else
            {
                AddTraitSelectors(selectors.Select(entry => entry.Selector).ToList(), selectionCount, category);
            }
        }

        void AddTraitSelectors(
            List<TraitPreferenceSelector> selectors,
            int selectionCount,
            TraitCategoryPrototype? category,
            string? groupLabel = null)
        {
            if (selectors.Count == 0)
                return;

            if (groupLabel != null)
            {
                TraitsList.AddChild(new Label
                {
                    Text = Loc.GetString(groupLabel),
                    Margin = new Thickness(8, 6, 0, 0),
                    FontColorOverride = Color.LightGray,
                });
            }

            foreach (var selector in selectors)
            {
                if (category is { MaxTraitPoints: >= 0 } &&
                    selector.Cost + selectionCount > category.MaxTraitPoints)
                {
                    selector.Checkbox.Label.FontColorOverride = Color.Red;
                }

                TraitsList.AddChild(selector);
            }
        }
    }
}
