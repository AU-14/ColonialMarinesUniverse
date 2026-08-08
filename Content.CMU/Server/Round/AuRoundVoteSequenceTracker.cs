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
        SequenceId++;
        CancelActive();
        Running = false;
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

    public bool IsRunning(int sequenceId)
    {
        return Running && IsCurrent(sequenceId);
    }

    public bool TryFinish(int sequenceId)
    {
        if (!IsRunning(sequenceId))
            return false;

        Running = false;
        return true;
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
