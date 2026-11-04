using System.Numerics;
using Content.Shared.CMU14.Telephone;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.CMU14.Telephone;

/// <summary>
/// The 911 tab shown on every phone, and the service picker it opens.
/// </summary>
public static class CMUEmergencyCallTab
{
    public static Control Create(Action<CMUEmergencyService> onCall)
    {
        var box = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 6,
        };

        box.AddChild(new Label { Text = Loc.GetString("cmu-911-tab-info") });

        var callButton = new Button
        {
            Text = Loc.GetString("cmu-911-call-button"),
            StyleClasses = { "OpenBoth", "Caution" },
        };
        callButton.OnPressed += _ => OpenServicePicker(onCall);
        box.AddChild(callButton);

        return box;
    }

    private static void OpenServicePicker(Action<CMUEmergencyService> onCall)
    {
        var window = new DefaultWindow
        {
            Title = Loc.GetString("cmu-911-picker-title"),
            MinSize = new Vector2(340, 110),
        };

        var box = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 6,
        };
        box.AddChild(new Label { Text = Loc.GetString("cmu-911-picker-question") });

        var buttons = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6,
        };

        void AddService(CMUEmergencyService service, string text)
        {
            var button = new Button
            {
                Text = Loc.GetString(text),
                HorizontalExpand = true,
            };
            button.OnPressed += _ =>
            {
                onCall(service);
                window.Close();
            };
            buttons.AddChild(button);
        }

        AddService(CMUEmergencyService.FireMedical, "cmu-911-service-fire-medical");
        AddService(CMUEmergencyService.LawEnforcement, "cmu-911-service-law");

        box.AddChild(buttons);
        window.Contents.AddChild(box);
        window.OpenCentered();
    }
}
