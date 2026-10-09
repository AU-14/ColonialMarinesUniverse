using Content.Client.Options.UI.Tabs;

namespace Content.Client.UserInterface.Systems.EscapeMenu;

public sealed partial class OptionsUIController
{
    public void OpenKeybinds()
    {
        OpenWindow();
        for (var i = 0; i < _optionsWindow.Tabs.ChildCount; i++)
        {
            if (_optionsWindow.Tabs.GetChild(i) is not KeyRebindTab)
                continue;
            _optionsWindow.Tabs.CurrentTab = i;
            break;
        }
    }
}
