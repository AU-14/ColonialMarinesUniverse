using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._CMU14.Medical.Treatment.FieldCare;

public sealed partial class CMUMedicalFieldCraftingMenu : RadialMenu
{
    public CMUMedicalFieldCraftingMenu()
    {
        RobustXamlLoader.Load(this);
    }
}
