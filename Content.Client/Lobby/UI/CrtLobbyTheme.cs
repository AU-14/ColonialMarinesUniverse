using System.Linq;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Lobby.UI;

internal static class CrtLobbyTheme
{
    public static void Apply(Control root, bool includeChat = false, bool useCrtTypography = true)
    {
        if (!StyleNano.CrtUiEnabled)
            return;

        ApplyControl(root, useCrtTypography);

        if (!includeChat && root is ChatBox)
            return;

        foreach (var child in root.Children.ToArray())
        {
            Apply(child, includeChat, useCrtTypography);
        }
    }

    public static void ApplyWindow(DefaultWindow window, bool includeChat = false, bool useCrtTypography = false)
    {
        if (!StyleNano.CrtUiEnabled)
            return;

        AddClass(window, StyleNano.StyleClassCrtWindow);
        window.HeaderClass = StyleNano.StyleClassCrtWindowHeader;
        window.TitleClass = StyleNano.StyleClassCrtWindowTitle;
        Apply(window, includeChat, useCrtTypography);
    }

    public static void ApplyToOptionButton(OptionButton option)
    {
        if (!StyleNano.CrtUiEnabled)
            return;

        AddClass(option, StyleNano.StyleClassCrtButton);

        if (!option.OptionStyleClasses.Contains(StyleNano.StyleClassCrtButton))
            option.OptionStyleClasses.Add(StyleNano.StyleClassCrtButton);
    }

    private static void ApplyControl(Control control, bool useCrtTypography)
    {
        if (!useCrtTypography)
            RemoveTypography(control);

        switch (control)
        {
            case Button button:
                AddClass(button, StyleNano.StyleClassCrtButton);
                if (useCrtTypography)
                {
                    button.Label.RemoveStyleClass(StyleNano.StyleClassCrtNativeButtonLabel);
                    AddClass(button.Label, StyleNano.StyleClassCrtButtonLabel);
                }
                else
                {
                    AddClass(button.Label, StyleNano.StyleClassCrtNativeButtonLabel);
                }
                break;
            case OptionButton option:
                ApplyToOptionButton(option);
                break;
            case ContainerButton containerButton:
                AddClass(containerButton, StyleNano.StyleClassCrtButton);
                break;
        }

        switch (control)
        {
            case Label label when useCrtTypography:
                ApplyLabel(label);
                break;
            case RichTextLabel richText when useCrtTypography:
                AddClass(richText, StyleNano.StyleClassCrtRichText);
                break;
            case LineEdit lineEdit:
                if (useCrtTypography)
                {
                    lineEdit.RemoveStyleClass(StyleNano.StyleClassCrtNativeLineEdit);
                    AddClass(lineEdit, StyleNano.StyleClassCrtLineEdit);
                }
                else
                {
                    AddClass(lineEdit, StyleNano.StyleClassCrtNativeLineEdit);
                }
                break;
            case Slider slider:
                AddClass(slider, StyleNano.StyleClassCrtSlider);
                break;
            case ProgressBar progressBar:
                AddClass(progressBar, StyleNano.StyleClassCrtProgressBar);
                break;
            case TabContainer tab:
                AddClass(tab, StyleNano.StyleClassCrtTabContainer);
                break;
            case TextureButton textureButton:
                AddClass(textureButton, StyleNano.StyleClassCrtIconButton);
                break;
            case ItemList itemList when useCrtTypography:
                AddClass(itemList, StyleNano.StyleClassCrtItemList);
                break;
            case ScrollBar scrollBar:
                AddClass(scrollBar, StyleNano.StyleClassCrtScrollBar);
                break;
            case StripeBack stripeBack:
                AddClass(stripeBack, StyleNano.StyleClassCrtStripeBack);
                break;
            case PanelContainer panel when panel.Parent is NanoHeading:
                AddClass(panel, StyleNano.StyleClassCrtHeaderPanel);
                break;
        }
    }

    private static void ApplyLabel(Label label)
    {
        if (label.HasStyleClass(StyleNano.StyleClassCrtButtonLabel) ||
            label.HasStyleClass(StyleNano.StyleClassCrtText) ||
            label.HasStyleClass(StyleNano.StyleClassCrtDimText) ||
            label.HasStyleClass(StyleNano.StyleClassCrtHeading) ||
            label.HasStyleClass(StyleNano.StyleClassCrtHeadingBig))
            return;

        if (label.HasStyleClass(StyleNano.StyleClassLabelHeadingBigger))
        {
            AddClass(label, StyleNano.StyleClassCrtHeadingBig);
            return;
        }

        if (label.HasStyleClass(StyleBase.StyleClassLabelHeading))
        {
            AddClass(label, StyleNano.StyleClassCrtHeading);
            return;
        }

        if (label.HasStyleClass(StyleBase.StyleClassLabelSubText))
        {
            AddClass(label, StyleNano.StyleClassCrtDimText);
            return;
        }

        AddClass(label, StyleNano.StyleClassCrtText);
    }

    private static void AddClass(Control control, string styleClass)
    {
        if (!control.HasStyleClass(styleClass))
            control.AddStyleClass(styleClass);
    }

    private static void RemoveTypography(Control control)
    {
        control.RemoveStyleClass(StyleNano.StyleClassCrtButtonLabel);
        control.RemoveStyleClass(StyleNano.StyleClassCrtText);
        control.RemoveStyleClass(StyleNano.StyleClassCrtDimText);
        control.RemoveStyleClass(StyleNano.StyleClassCrtHeading);
        control.RemoveStyleClass(StyleNano.StyleClassCrtHeadingBig);
        control.RemoveStyleClass(StyleNano.StyleClassCrtRichText);
        control.RemoveStyleClass(StyleNano.StyleClassCrtLineEdit);
        control.RemoveStyleClass(StyleNano.StyleClassCrtItemList);
    }
}
