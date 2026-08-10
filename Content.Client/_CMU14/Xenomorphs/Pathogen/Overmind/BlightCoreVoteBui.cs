using Content.Shared._CMU14.Xenomorphs.Pathogen.Overmind;
using Robust.Client.UserInterface;

namespace Content.Client._CMU14.Xenomorphs.Pathogen.Overmind;

public sealed class BlightCoreVoteBui : BoundUserInterface
{
    private BlightCoreVoteWindow? _window;

    public BlightCoreVoteBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = new BlightCoreVoteWindow();

        _window.OnVote += votedFor =>
        {
            SendMessage(new BlightCoreVoteMessage(votedFor));
        };

        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not BlightCoreVoteBuiState voteState || _window == null)
            return;

        _window.SetEndTime(voteState.EndsAt);
        _window.UpdateCandidates(voteState.Candidates);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _window?.Dispose();
    }
}