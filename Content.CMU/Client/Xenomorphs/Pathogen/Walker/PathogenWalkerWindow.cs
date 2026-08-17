using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using System.Numerics;

namespace Content.Client._CMU14.Xenomorphs.Pathogen.Walker;

public sealed class CMUPathogenWalkerWindow : DefaultWindow
{
    public event Action? OnAccept;
    public event Action? OnDecline;

    private readonly Label _timerLabel;

    public CMUPathogenWalkerWindow()
    {
        Title = Loc.GetString("cmu14-walker-offer-title");
        MinSize = new Vector2(400, 180);

        var vbox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(12) };

        vbox.AddChild(new Label
        {
            Text = Loc.GetString("cmu14-walker-offer-body"),
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });

        _timerLabel = new Label
        {
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12)
        };
        vbox.AddChild(_timerLabel);

        var hbox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true };

        var accept = new Button { Text = Loc.GetString("cmu14-walker-offer-accept"), HorizontalExpand = true };
        accept.OnPressed += _ => { OnAccept?.Invoke(); Close(); };

        var decline = new Button { Text = Loc.GetString("cmu14-walker-offer-decline"), HorizontalExpand = true };
        decline.OnPressed += _ => { OnDecline?.Invoke(); Close(); };

        hbox.AddChild(accept);
        hbox.AddChild(decline);
        vbox.AddChild(hbox);

        Contents.AddChild(vbox);
    }

    public void SetTimeout(double seconds)
    {
        _timerLabel.Text = Loc.GetString("cmu14-walker-offer-timeout", ("seconds", (int)seconds));
    }
}