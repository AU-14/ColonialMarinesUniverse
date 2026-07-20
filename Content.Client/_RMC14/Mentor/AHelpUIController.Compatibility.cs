using Content.Client.Stylesheets;

namespace Content.Client.UserInterface.Systems.Bwoink;

public sealed partial class AHelpUIController
{
    public void UnreadMHelpReceived()
    {
        GameAHelpButton?.StyleClasses.Add(StyleClass.Negative);
        LobbyAHelpButton?.StyleClasses.Add(StyleClass.Negative);
    }

    public void UnreadMHelpRead()
    {
        GameAHelpButton?.StyleClasses.Remove(StyleClass.Negative);
        LobbyAHelpButton?.StyleClasses.Remove(StyleClass.Negative);
    }
}
