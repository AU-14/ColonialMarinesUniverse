using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Mutiny;

[Serializable, NetSerializable]
public enum CMUMutinyPromptKind : byte
{
    // a leader asks a marine to swear in
    Invite,
    // the leader confirms going loud
    Begin,
    // everyone else on the side picks one when it goes loud
    ChooseSide,
}

[Serializable, NetSerializable]
public enum CMUMutinyChoice : byte
{
    Decline,
    Accept,
    Mutineer,
    Loyalist,
    NonCombatant,
}

[Serializable, NetSerializable]
public sealed class CMUMutinyRosterEntry
{
    public readonly string Name;
    public readonly string? Job;
    public readonly bool Alive;

    public CMUMutinyRosterEntry(string name, string? job, bool alive)
    {
        Name = name;
        Job = job;
        Alive = alive;
    }
}

[Serializable, NetSerializable]
public sealed class CMUMutinyPromptEuiState : EuiStateBase
{
    public readonly CMUMutinyPromptKind Kind;
    public readonly string LeaderName;
    public readonly string? LeaderJob;
    // invites after the mutiny went loud make you a mutineer on the spot instead of swearing you in
    public readonly bool Begun;
    public readonly List<CMUMutinyRosterEntry> Roster;
    public readonly List<CMUMutinyChoice> Disabled;
    public readonly TimeSpan? Deadline;
    public readonly TimeSpan Duration;

    public CMUMutinyPromptEuiState(
        CMUMutinyPromptKind kind,
        string leaderName,
        string? leaderJob,
        bool begun,
        List<CMUMutinyRosterEntry> roster,
        List<CMUMutinyChoice> disabled,
        TimeSpan? deadline,
        TimeSpan duration)
    {
        Kind = kind;
        LeaderName = leaderName;
        LeaderJob = leaderJob;
        Begun = begun;
        Roster = roster;
        Disabled = disabled;
        Deadline = deadline;
        Duration = duration;
    }
}

[Serializable, NetSerializable]
public sealed class CMUMutinyPromptChoiceMessage : EuiMessageBase
{
    public readonly CMUMutinyChoice Choice;

    public CMUMutinyPromptChoiceMessage(CMUMutinyChoice choice)
    {
        Choice = choice;
    }
}
