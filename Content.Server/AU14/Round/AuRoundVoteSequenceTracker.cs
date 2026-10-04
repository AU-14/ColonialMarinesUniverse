using Content.Server.Voting;
using Content.Server.Voting.Managers;

namespace Content.Server.AU14.Round;

internal sealed class AuRoundVoteSequenceTracker
{
    private readonly List<IVoteHandle> _activeVoteHandles = new();

    public bool Running { get; set; }
    public int SequenceId { get; private set; }

    public void Reset()
    {
        CancelActive();
        Running = false;
        SequenceId = 0;
    }

    public int Restart()
    {
        SequenceId++;
        CancelActive();
        Running = false;
        return SequenceId;
    }

    public bool IsCurrent(int sequenceId)
    {
        return sequenceId == SequenceId;
    }

    public void Track(IVoteHandle handle)
    {
        _activeVoteHandles.Add(handle);

        handle.OnFinished += RemoveTrackedVote;
        handle.OnCancelled += RemoveTrackedVote;
    }

    public void CancelActive()
    {
        foreach (var handle in _activeVoteHandles.ToArray())
        {
            if (!handle.Finished)
                handle.Cancel();
        }

        _activeVoteHandles.Clear();
    }

    private void RemoveTrackedVote(IVoteHandle handle, VoteFinishedEventArgs args)
    {
        RemoveTrackedVote(handle);
    }

    private void RemoveTrackedVote(IVoteHandle handle)
    {
        _activeVoteHandles.Remove(handle);
        handle.OnFinished -= RemoveTrackedVote;
        handle.OnCancelled -= RemoveTrackedVote;
    }
}
