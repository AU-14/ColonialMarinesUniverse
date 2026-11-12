using Content.Server.EUI;
using Content.Shared.CMU14.Mutiny;
using Content.Shared.Eui;

namespace Content.Server.CMU14.Mutiny;

// one window for every mutiny question. the callback runs exactly once: on a button, on the
// deadline, or with the fallback answer when the window or the session goes away
public sealed class CMUMutinyPromptEui : BaseEui
{
    public readonly EntityUid Recipient;
    public readonly CMUMutinyPromptEuiState PromptState;

    private readonly CMUMutinyChoice _fallback;
    private readonly HashSet<CMUMutinyChoice> _allowed;
    private readonly Action<CMUMutinyPromptEui, CMUMutinyChoice> _onResolved;
    private bool _resolved;

    public CMUMutinyPromptEui(
        EntityUid recipient,
        CMUMutinyPromptEuiState state,
        IEnumerable<CMUMutinyChoice> allowed,
        CMUMutinyChoice fallback,
        Action<CMUMutinyPromptEui, CMUMutinyChoice> onResolved)
    {
        Recipient = recipient;
        PromptState = state;
        _allowed = new HashSet<CMUMutinyChoice>(allowed);
        _allowed.ExceptWith(state.Disabled);
        _allowed.Add(fallback);
        _fallback = fallback;
        _onResolved = onResolved;
    }

    public override void Opened()
    {
        base.Opened();
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        return PromptState;
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not CMUMutinyPromptChoiceMessage choice)
            return;

        // a synth clicking a disabled mutineer card through a modified client just gets the fallback
        Resolve(_allowed.Contains(choice.Choice) ? choice.Choice : _fallback);
        Close();
    }

    public override void Closed()
    {
        base.Closed();
        Resolve(_fallback);
    }

    public void Expire()
    {
        Resolve(_fallback);
        if (!IsShutDown)
            Close();
    }

    private void Resolve(CMUMutinyChoice choice)
    {
        if (_resolved)
            return;

        _resolved = true;
        _onResolved(this, choice);
    }
}
