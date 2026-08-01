using Content.Shared._AU14.Callsigns;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

// AU14 partial half of the upstream OverwatchConsoleBui. Lives in _AU14 so the
// RMC file stays byte-identical to upstream and never merge-conflicts; partial
// classes share private members, so the squad views are reachable from here.
namespace Content.Client._RMC14.Overwatch;

public sealed partial class OverwatchConsoleBui
{
    private const string AU14CommsDirectoryButtonName = "AU14CommsDirectoryButton";

    // consoles that also carry the callsign directory get a tab under fireteams
    // that pops it open without hunting down a standalone terminal. squad views
    // are rebuilt by RefreshState, so this is called repeatedly and idempotent
    public void AU14EnsureCommsDirectoryButtons()
    {
        foreach (var view in _squadViews.Values)
        {
            var container = view.TabButtonsContainer;
            var exists = false;

            foreach (var child in container.Children)
            {
                if (child.Name == AU14CommsDirectoryButtonName)
                {
                    exists = true;
                    break;
                }
            }

            if (exists)
                continue;

            var button = new Button
            {
                Name = AU14CommsDirectoryButtonName,
                Text = Loc.GetString("au14-overwatch-console-comms-directory"),
                StyleClasses = { "ActionButton" },
                VerticalAlignment = Control.VAlignment.Top,
                Margin = new Thickness(0, 10, 0, 0),
                MinWidth = 250,
            };

            button.OnPressed += _ => SendMessage(new AU14CallsignOpenDirectoryMsg());
            container.AddChild(button);
        }
    }
}
