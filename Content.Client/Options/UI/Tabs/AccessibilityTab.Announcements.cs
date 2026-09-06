using System.Collections.Generic;
using Content.Shared._RMC14.Announce;
using Content.Shared._RMC14.CCVar;

namespace Content.Client.Options.UI.Tabs;

public sealed partial class AccessibilityTab
{
    private void RegisterAnnouncementOptions()
    {
        var announcementEntries = new List<OptionDropDownCVar<AnnouncementDisplayPreference>.ValueOption>
        {
            new(AnnouncementDisplayPreference.Stylized, Loc.GetString("rmc-ui-options-announcements-style-stylized")),
            new(AnnouncementDisplayPreference.Default, Loc.GetString("rmc-ui-options-announcements-style-default")),
            new(AnnouncementDisplayPreference.Simplified, Loc.GetString("rmc-ui-options-announcements-style-simplified")),
            new(AnnouncementDisplayPreference.Disabled, Loc.GetString("rmc-ui-options-announcements-style-disabled"))
        };

        Control.AddOptionDropDown(RMCCVars.RMCAnnouncementStyle, AnnouncementStyleDropDown, announcementEntries);
    }
}
