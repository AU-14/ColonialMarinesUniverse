using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._RMC14.Xenonids.Alchemist;

public sealed partial class XenoAlchemistMenu : RadialMenu
{
    public XenoAlchemistMenu()
    {
        RobustXamlLoader.Load(this);
    }
}
