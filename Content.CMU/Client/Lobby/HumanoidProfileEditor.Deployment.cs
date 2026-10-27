using System.Linq;
using Content.Client.CMU14.Lobby;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void BuildDeploymentPages()
    {
        // These selectors retain their native preference callbacks and mode-specific data.
        // Give threats and their role choices one vertical reading/scrolling order.
        foreach (var (tabs, preferences, jobs) in new[]
        {
            (ColonyFallTabs, ColonyThreatPreferenceList, ColonyThreatJobList),
            (DistressSignalTabs, DistressThreatPreferenceList, DistressThreatJobList),
        })
        {
            preferences.Orphan();
            jobs.Orphan();
            var page = (BoxContainer) tabs.GetChild(1);
            page.RemoveAllChildren();
            page.Orientation = LayoutOrientation.Vertical;
            var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
            var content = WorkspaceColumn();
            content.AddChild(preferences);
            content.AddChild(jobs);
            scroll.AddChild(content);
            page.AddChild(scroll);
        }

        // The old annotation was positioned over the native tab strip, which is now hidden.
        foreach (var label in InsurgencyTabs.Parent!.Children.OfType<Label>())
            label.Visible = false;

        foreach (var list in GetGamemodeJobLists())
            list.SeparationOverride = 8;
    }
}
