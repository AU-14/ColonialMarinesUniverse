using Content.Shared._CMU14.Xenomorphs.Pathogen.Overmind;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.Client._CMU14.Xenomorphs.Pathogen.Overmind;

public sealed class BlightCoreAcceptBui : BoundUserInterface
{
    [Dependency] private readonly Robust.Client.Player.IPlayerManager _player = default!;

    private BlightCoreAcceptWindow? _window;
    
    public BlightCoreAcceptBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = new BlightCoreAcceptWindow();

        IoCManager.InjectDependencies(this);

        _window.OnAccept += () =>
        {
            var xeno = _player.LocalSession?.AttachedEntity;
            if (xeno == null) return;
            SendMessage(new BlightCoreAcceptMessage { Candidate = EntMan.GetNetEntity(xeno.Value) });
            _window.Close();
        };

        _window.OnDecline += () =>
        {
            SendMessage(new BlightCoreDeclineMessage());
            _window.Close();
        };

        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not BlightCoreBuiState acceptState || _window == null)
            return;

        _window.SetEndTime(acceptState.EndsAt);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _window?.Dispose();
    }
}