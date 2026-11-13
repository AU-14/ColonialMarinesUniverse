using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// The ascension vote's two windows. They are EUIs, not bound interfaces: a voter can be anywhere on the map, far
// from the Ascension Stone, and during founding grace there is no Stone at all.

/// <summary>
/// The answer to "claim the Lodge?".
/// </summary>
[Serializable, NetSerializable]
public sealed class BadBloodAscensionPromptMessage(bool accept) : EuiMessageBase
{
    public bool Accept = accept;
}

[Serializable, NetSerializable]
public sealed class BadBloodAscensionPromptState(bool fromBracer) : EuiStateBase
{
    /// <summary>
    /// Founding grace: the offer comes over the bracer, nobody stands on a Stone.
    /// </summary>
    public bool FromBracer = fromBracer;
}

[Serializable, NetSerializable]
public sealed class BadBloodAscensionCandidate(NetEntity entity, string name, int kills, int votes)
{
    public NetEntity Entity = entity;
    public string Name = name;

    /// <summary>
    /// Worthy kills. The higher count wins a tie.
    /// </summary>
    public int Kills = kills;

    public int Votes = votes;
}

[Serializable, NetSerializable]
public sealed class BadBloodAscensionVoteState : EuiStateBase
{
    /// <summary>
    /// Earliest candidate first.
    /// </summary>
    public List<BadBloodAscensionCandidate> Candidates = new();

    /// <summary>
    /// Server time the vote closes.
    /// </summary>
    public TimeSpan EndsAt;

    public NetEntity? VotedFor;
}

[Serializable, NetSerializable]
public sealed class BadBloodAscensionVoteMessage(NetEntity candidate) : EuiMessageBase
{
    public NetEntity Candidate = candidate;
}
