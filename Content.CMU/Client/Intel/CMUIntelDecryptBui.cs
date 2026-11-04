using Content.Shared.CMU14.Intel;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Intel;

public sealed class CMUIntelDecryptBui : BoundUserInterface
{
    private CMUIntelDecryptWindow? _window;

    public CMUIntelDecryptBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUIntelDecryptWindow>();
        _window.OnGuess += guess => SendMessage(new CMUIntelDecryptGuessMessage(guess));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not CMUIntelDecryptBuiState s)
            return;

        _window.UpdateState(s);
    }
}
