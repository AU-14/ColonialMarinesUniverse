namespace Content.Server.AU14.Round;

[Flags]
internal enum AuRoundVoteBranch : byte
{
    None = 0,
    Govfor = 1 << 0,
    Opfor = 1 << 1,
}

internal sealed class AuRoundVoteCompletionState
{
    private int _sequenceId;
    private AuRoundVoteBranch _required;
    private AuRoundVoteBranch _completed;
    private bool _signalled;

    public void Begin(int sequenceId, AuRoundVoteBranch required)
    {
        _sequenceId = sequenceId;
        _required = required;
        _completed = AuRoundVoteBranch.None;
        _signalled = false;
    }

    public bool Complete(int sequenceId, AuRoundVoteBranch branch)
    {
        if (sequenceId != _sequenceId ||
            _signalled ||
            (_required & branch) == AuRoundVoteBranch.None)
        {
            return false;
        }

        _completed |= branch;
        if ((_completed & _required) != _required)
            return false;

        _signalled = true;
        return true;
    }
}
