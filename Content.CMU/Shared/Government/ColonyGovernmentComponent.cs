// SPDX-License-Identifier: AGPL-3.0-only
namespace Content.Shared.CMU14.Government;

// The state of the foreign policy of the colony for the round: kept by the server only (the window gets what it shows in the state of the interface). One entity of the round has it (made when first wanted).
[RegisterComponent]
public sealed partial class ColonyGovernmentComponent : Component
{
    // What each faction thinks of the colony (-100 to 100), and the treaties that are in force.
    public readonly Dictionary<string, int> Attitude = new();
    public readonly List<ActiveTreaty> Treaties = new();

    // The part of a point of influence that a treaty has earned and not yet paid to the ambassador.
    public readonly Dictionary<string, float> PointsOwed = new();

    public readonly List<Petition> Petitions = new();
    public int NextPetitionId = 1;

    public TimeSpan NextMinute;
    public TimeSpan NextPetition;
    public TimeSpan NextDrift;
    public bool Started;
}

public sealed class ActiveTreaty(string id, string faction, TimeSpan until)
{
    public readonly string Id = id;
    public readonly string Faction = faction;
    public readonly TimeSpan Until = until;
}

// What a faction has asked and not been answered yet: the reward and the cost are kept here because a bargain changes them.
public sealed class Petition(int id, string proto, string faction, TimeSpan deadline, int cost, int reward)
{
    public readonly int Id = id;
    public readonly string Proto = proto;
    public readonly string Faction = faction;
    public readonly TimeSpan Deadline = deadline;
    public int Cost = cost;
    public int Reward = reward;
    public bool Bargained;
}
