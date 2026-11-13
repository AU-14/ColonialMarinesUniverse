using Content.Server.EUI;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Eui;

namespace Content.Server.CMU14.Yautja.BadBlood;

// "Claim the Lodge?" for someone standing on the Ascension Stone, or over the bracer during founding grace.
public sealed class BadBloodAscensionPromptEui(BadBloodAscensionSystem system, EntityUid candidate, bool fromBracer) : BaseEui
{
    private bool _answered;

    public EntityUid Candidate => candidate;

    public override void Opened()
    {
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        return new BadBloodAscensionPromptState(fromBracer);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        if (msg is BadBloodAscensionPromptMessage answer)
        {
            if (!_answered)
            {
                _answered = true;
                system.OnPromptAnswered(this, answer.Accept);
            }

            if (!IsShutDown)
                Close();

            return;
        }

        base.HandleMessage(msg);
    }

    public override void Closed()
    {
        system.OnPromptClosed(this);
    }
}

// the vote window: the candidates with their kills and votes, and a button to vote for each
public sealed class BadBloodAscensionVoteEui(BadBloodAscensionSystem system) : BaseEui
{
    public override void Opened()
    {
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        return system.BuildVoteState(Player);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        if (msg is BadBloodAscensionVoteMessage vote)
        {
            system.OnVoteUiVoted(this, vote.Candidate);
            return;
        }

        base.HandleMessage(msg);
    }

    public override void Closed()
    {
        system.OnVoteUiClosed(this);
    }
}
