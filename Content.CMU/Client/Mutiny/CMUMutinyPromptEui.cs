using Content.Client.Eui;
using Content.Shared.CMU14.Mutiny;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Mutiny;

[UsedImplicitly]
public sealed class CMUMutinyPromptEui : BaseEui
{
    private CMUMutinyPromptWindow? _window;
    private bool _answered;

    public override void HandleState(EuiStateBase state)
    {
        if (state is not CMUMutinyPromptEuiState prompt)
            return;

        _window?.Close();
        _window = new CMUMutinyPromptWindow(prompt);
        _window.OnChoice += choice =>
        {
            if (_answered)
                return;

            _answered = true;
            SendMessage(new CMUMutinyPromptChoiceMessage(choice));
            _window?.Close();
        };

        // closing the window is an answer too, the server fills in the fallback
        _window.OnClose += () =>
        {
            if (_answered)
                return;

            _answered = true;
            SendMessage(new CloseEuiMessage());
        };

        _window.OpenCentered();
        IoCManager.Resolve<IClyde>().RequestWindowAttention();
    }

    public override void Closed()
    {
        // server already resolved it, don't echo a close back
        _answered = true;
        _window?.Close();
    }
}
