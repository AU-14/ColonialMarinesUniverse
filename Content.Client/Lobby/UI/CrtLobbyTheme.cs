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

    /// <summary>
    ///     Swaps an <see cref="OutputPanel"/>'s scrollbar to the chat log's bracket-capped thumb
    ///     instead of the plain <see cref="StyleNano.StyleClassCrtScrollBar"/> every other scrollbar
    ///     gets from the general tree walk - for panels like AHelp/Mentor Help whose message log
    ///     should read as the same instrument as the main chat.
    /// </summary>
    public static void ApplyChatScrollBar(OutputPanel output)
    {
        if (!StyleNano.CrtUiEnabled)
            return;

        if (output.Children.OfType<VScrollBar>().FirstOrDefault() is not { } scrollBar)
            return;

        scrollBar.RemoveStyleClass(StyleNano.StyleClassCrtScrollBar);
        AddClass(scrollBar, StyleNano.StyleClassCrtChatScrollBar);
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
                // Withheld from the ready toggle, which brings its own box. Adding this too would
                // leave two rules matching the same control at the same specificity, and that has
                // no defined winner - the toggle would look like an ordinary button on some runs
                // and not others. Its label still gets the shared typography below.
                // Command cells are withheld for the same reason: they carry their own borderless box
                // and the standard one would tie with it at equal specificity.
                if (!button.HasStyleClass(StyleNano.StyleClassCrtReadyToggle) &&
                    !button.HasStyleClass(StyleNano.StyleClassCrtReadyToggleOn) &&
                    !button.HasStyleClass(StyleNano.StyleClassCrtCommandCell))
                    AddClass(button, StyleNano.StyleClassCrtButton);

                // Centring a button label takes two things, and the stylesheet can only do one of
                // them. AlignMode centres the text inside the Label's own box; this makes that box
                // actually span the button. Without it the Label is exactly text-width, AlignMode
                // has nothing to centre within, and the label sits left in any button whose width
                // comes from a MinWidth or a shared column - AHELP, CUSTOMIZE, CALL VOTE.
                // HorizontalExpand is a plain property, not a style property, so it cannot be set
                // from a rule and has to happen here.
                button.Label.HorizontalExpand = true;

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
            // Must come before ContainerButton: CheckBox derives from it, not from Button, so it
            // used to fall into the arm below and get the full button box. That is what turned every
            // row of the options menu into a wide filled bar.
            case CheckBox checkBox:
                AddClass(checkBox, StyleNano.StyleClassCrtCheckBox);
                break;
            case ContainerButton containerButton:
                // A section heading is a button so it can be clicked to fold, but it already has its
                // own banded box. Handing it the standard one too would tie two rules of equal
                // specificity, which has no defined winner.
                if (!containerButton.HasStyleClass(StyleNano.StyleClassCrtSectionHeader) &&
                    !containerButton.HasStyleClass(StyleNano.StyleClassCrtCommandCell))
                    AddClass(containerButton, StyleNano.StyleClassCrtButton);
                break;
        }

        switch (control)
        {
            case Label label when useCrtTypography:
                ApplyLabel(label);
                break;
            case RichTextLabel richText when useCrtTypography:
                // Controls that already carry a more specific CRT rich-text class style themselves.
                if (!richText.HasStyleClass(StyleNano.StyleClassCrtServerInfoText) &&
                    !richText.HasStyleClass(StyleNano.StyleClassCrtCharacterSummary))
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
            case SpinBox spinBox:
                // A SpinBox is a BoxContainer, so the walk reaches its +/- buttons and gives them
                // the CRT button class - but they already carry NanoUI's spinbox-left/middle/right,
                // and two rules of equal specificity have no defined winner. That's why the colour
                // picker's steppers render as unthemed nubs inside a CRT window. Dropping the Nano
                // classes leaves exactly one rule matching.
                foreach (var child in spinBox.Children)
                {
                    if (child is not Button stepper)
                        continue;

                    stepper.RemoveStyleClass(SpinBox.LeftButtonStyle);
                    stepper.RemoveStyleClass(SpinBox.MiddleButtonStyle);
                    stepper.RemoveStyleClass(SpinBox.RightButtonStyle);
                }
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
                // Left alone if ApplyChatScrollBar already opted this one into the chat's bracket-
                // capped thumb - both classes match VScrollBar at equal specificity, so having both
                // present has no defined winner (same trap as the SpinBox steppers above).
                if (!scrollBar.HasStyleClass(StyleNano.StyleClassCrtChatScrollBar))
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
            label.HasStyleClass(StyleNano.StyleClassCrtHeadingBig) ||
            // The lobby countdown swaps to these as it runs down, and Apply re-runs on a palette
            // change - which could land while the countdown is amber and hand it a second font rule.
            label.HasStyleClass(StyleNano.StyleClassCrtHeadingBigWarning) ||
            label.HasStyleClass(StyleNano.StyleClassCrtHeadingBigDanger) ||
            // Same reason as the headings: the clock swaps between these three as it runs down, and
            // Apply re-runs on a palette change, which could land while it is amber.
            label.HasStyleClass(StyleNano.StyleClassCrtClock) ||
            label.HasStyleClass(StyleNano.StyleClassCrtClockWarning) ||
            label.HasStyleClass(StyleNano.StyleClassCrtClockDanger))
            return;

        if (label.HasStyleClass(StyleClass.LabelHeadingBigger))
        {
            AddClass(label, StyleNano.StyleClassCrtHeadingBig);
            return;
        }

        if (label.HasStyleClass(StyleClass.LabelHeading))
        {
            AddClass(label, StyleNano.StyleClassCrtHeading);
            return;
        }

        if (label.HasStyleClass(StyleClass.LabelSubText))
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
        control.RemoveStyleClass(StyleNano.StyleClassCrtHeadingBigWarning);
        control.RemoveStyleClass(StyleNano.StyleClassCrtHeadingBigDanger);
        control.RemoveStyleClass(StyleNano.StyleClassCrtClock);
        control.RemoveStyleClass(StyleNano.StyleClassCrtClockWarning);
        control.RemoveStyleClass(StyleNano.StyleClassCrtClockDanger);
        control.RemoveStyleClass(StyleNano.StyleClassCrtRichText);
        control.RemoveStyleClass(StyleNano.StyleClassCrtLineEdit);
        control.RemoveStyleClass(StyleNano.StyleClassCrtItemList);
    }
}
