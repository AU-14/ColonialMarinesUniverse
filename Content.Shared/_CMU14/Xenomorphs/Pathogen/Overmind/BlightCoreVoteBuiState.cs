using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Overmind;

[NetSerializable, Serializable]
public enum BlightCoreUiKey
{
    Accept,
    Vote
}

/// <summary>
/// One entry in the ascension vote candidate list.
/// </summary>
[Serializable, NetSerializable]
public sealed class BlightCoreVoteCandidate
{
    public NetEntity Candidate;
    public string Name;
    public int Votes;

    public BlightCoreVoteCandidate(NetEntity candidate, string name, int votes)
    {
        Candidate = candidate;
        Name = name;
        Votes = votes;
    }
}

/// <summary>
/// Shared state broadcast to every hive member while an Overmind ascension vote is active.
/// </summary>
[Serializable, NetSerializable]
public sealed class BlightCoreVoteBuiState : BoundUserInterfaceState
{
    public NetEntity Core;
    public TimeSpan EndsAt;
    public List<BlightCoreVoteCandidate> Candidates = new();
}

/// <summary>
/// Sent by a hive member to cast (or change) their vote.
/// IMPORTANT: must use NetEntity for networking.
/// </summary>
[Serializable, NetSerializable]
public sealed class BlightCoreVoteMessage : BoundUserInterfaceMessage
{
    public NetEntity VotedFor;

    public BlightCoreVoteMessage(NetEntity votedFor)
    {
        VotedFor = votedFor;
    }
}