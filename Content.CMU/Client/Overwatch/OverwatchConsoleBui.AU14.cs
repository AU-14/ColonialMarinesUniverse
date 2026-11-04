using Content.Shared.CMU14.Callsigns;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RMC14.Overwatch;

public sealed partial class OverwatchConsoleBui
{
    private const string AU14CommsDirectoryButtonName = "AU14CommsDirectoryButton";

    private void CMUAddCommsDirectoryButton(OverwatchSquadView view)
    {
        // Squad views own their buttons. Recreated views get a fresh button from the
        // same setup hook, so closed consoles need no entity or child-tree polling.
        if (!EntMan.HasComponent<AU14CallsignConsoleComponent>(Owner))
            return;

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
        view.TabButtonsContainer.AddChild(button);
        Content.Client.CMU14.Overwatch.CMUOverwatchCrt.SetLit(button, false);
    }
}
