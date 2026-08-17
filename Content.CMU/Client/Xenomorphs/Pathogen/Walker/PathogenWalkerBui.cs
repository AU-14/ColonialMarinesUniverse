using Content.Shared._CMU14.Xenomorphs.Pathogen.Walker;
using Robust.Client.UserInterface;

namespace Content.Client._CMU14.Xenomorphs.Pathogen.Walker;

public sealed class CMUPathogenWalkerBui : BoundUserInterface
{
    private CMUPathogenWalkerWindow? _window;

    public CMUPathogenWalkerBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUPathogenWalkerWindow>();
        _window.OnAccept += () => SendMessage(new CMUPathogenWalkerAcceptMsg());
        _window.OnDecline += () => SendMessage(new CMUPathogenWalkerDeclineMsg());
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is CMUPathogenWalkerBuiState s)
            _window?.SetTimeout(s.TimeoutSeconds);
    }
}