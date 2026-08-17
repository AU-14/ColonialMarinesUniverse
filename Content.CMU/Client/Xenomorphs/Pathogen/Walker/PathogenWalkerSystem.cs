using Content.Shared._CMU14.Xenomorphs.Pathogen.Walker;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using System.Numerics;
using Robust.Client.UserInterface;


namespace Content.Client._CMU14.Xenomorphs.Pathogen.Walker;

public sealed class CMUPathogenWalkerSystem : EntitySystem
{
    private CMUPathogenWalkerWindow? _window;

    public override void Initialize()
    {
        SubscribeNetworkEvent<CMUPathogenWalkerOfferEvent>(OnOffer);
        SubscribeNetworkEvent<CMUPathogenWalkerReviveWarningEvent>(OnReviveWarning);
    }

    private void OnOffer(CMUPathogenWalkerOfferEvent ev)
    {
        _window?.Close();
        _window = new CMUPathogenWalkerWindow();
        _window.SetTimeout(ev.TimeoutSeconds);

        _window.OnAccept += () =>
        {
            RaiseNetworkEvent(new CMUPathogenWalkerAcceptNetEvent(ev.Target));
            _window?.Close();
        };

        _window.OnDecline += () =>
        {
            RaiseNetworkEvent(new CMUPathogenWalkerDeclineNetEvent(ev.Target));
            _window?.Close();
        };

        _window.OpenCentered();
    }

    private void OnReviveWarning(CMUPathogenWalkerReviveWarningEvent ev)
    {
        var popup = new DefaultWindow
        {
            Title = Loc.GetString("cmu14-walker-revive-warning-title"),
            MinSize = new Vector2(350, 120),
        };
        popup.Contents.AddChild(new Label
        {
            Text = Loc.GetString("cmu14-walker-revive-warning-body", ("seconds", (int) ev.Seconds)),
            HorizontalAlignment = Control.HAlignment.Center,
            Margin = new Thickness(12),
        });
        popup.OpenCentered();
    }
}