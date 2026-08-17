using Content.Shared._CMU14.Xenomorphs.Pathogen.Walker;

namespace Content.Client._CMU14.Xenomorphs.Pathogen.Walker;

public sealed class CMUPathogenWalkerSystem : EntitySystem
{
    private CMUPathogenWalkerWindow? _window;

    public override void Initialize()
    {
        SubscribeNetworkEvent<CMUPathogenWalkerOfferEvent>(OnOffer);
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
}