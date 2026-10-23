using Content.Client.Options.UI;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.UserInterface.Options;

/// <summary>
///     The parts of the options menu's look that differ from the shared CRT theme: underlined tabs,
///     theme-coloured checkboxes and one width for every dropdown.
/// </summary>
public static class CmuOptionsLook
{
    /// <summary>Wide enough for the longest value, "Automatic (100%)", to clear the arrow.</summary>
    public const float DropDownWidth = 210;

    public static void Apply(Control root)
    {
        switch (root)
        {
            case TabContainer tabs:
                AddClass(tabs, StyleNano.StyleClassCmuOptionsTabs);
                break;
            case CheckBox checkBox:
                AddClass(checkBox.TextureRect, StyleNano.StyleClassCmuOptionCheck);
                break;
            case OptionDropDown dropDown:
                dropDown.Button.MinWidth = DropDownWidth;
                break;
        }

        foreach (var child in root.Children)
        {
            Apply(child);
        }
    }

    private static void AddClass(Control control, string styleClass)
    {
        if (!control.HasStyleClass(styleClass))
            control.AddStyleClass(styleClass);
    }
}
