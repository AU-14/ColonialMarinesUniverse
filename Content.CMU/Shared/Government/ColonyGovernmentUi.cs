// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Government;

// Key of the window of the foreign policy of the colony, which the console of the administration opens.
[Serializable, NetSerializable]
public enum ColonyGovernmentUi : byte
{
    Key,
}

// A faction that has an ambassador on the line, and what it thinks of the colony.
[Serializable, NetSerializable]
public sealed class FactionEntry(string name, int attitude)
{
    public readonly string Name = name;
    public readonly int Attitude = attitude;
}

// A treaty that can be proposed: what it gives and costs, and why it cannot be signed with a given faction (the server answers that on the signing; the window shows what is known).
[Serializable, NetSerializable]
public sealed class TreatyEntry(string id, TreatyKind kind, string name, string description, int moneyCost, int durationSeconds, int minAttitude, string effects)
{
    public readonly string Id = id;
    public readonly TreatyKind Kind = kind;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly int MoneyCost = moneyCost;
    public readonly int DurationSeconds = durationSeconds;
    public readonly int MinAttitude = minAttitude;
    public readonly string Effects = effects;
}

[Serializable, NetSerializable]
public sealed class ActiveTreatyEntry(string id, string name, string faction, int leftSeconds)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string Faction = faction;
    public readonly int LeftSeconds = leftSeconds;
}

[Serializable, NetSerializable]
public sealed class PetitionEntry(int id, bool ultimatum, string faction, string text, int leftSeconds, int cost, int reward, bool canBargain, bool canAfford)
{
    public readonly int Id = id;
    public readonly bool Ultimatum = ultimatum;
    public readonly string Faction = faction;
    public readonly string Text = text;
    public readonly int LeftSeconds = leftSeconds;
    public readonly int Cost = cost;
    public readonly int Reward = reward;
    public readonly bool CanBargain = canBargain;
    public readonly bool CanAfford = canAfford;
}

// Everything that the window shows.
[Serializable, NetSerializable]
public sealed class ColonyGovernmentState(
    float budget, float incomePerMinute, int dutyCut, int callDiscount, int treatySlots, int treatiesInForce,
    List<FactionEntry> factions, List<TreatyEntry> treaties, List<ActiveTreatyEntry> active, List<PetitionEntry> petitions,
    CreditState credit, CitiesState cities, LicensesState licenses, LandState land, PeopleState people, AdminState admin, GrowthState growth, QuotaState quota, SubsidyState subsidy) : BoundUserInterfaceState
{
    public readonly float Budget = budget;

    // What the treaties in force bring together: the money for a minute, the points off the duty, the share off the calls, and how many treaties may be in force.
    public readonly float IncomePerMinute = incomePerMinute;
    public readonly int DutyCut = dutyCut;
    public readonly int CallDiscount = callDiscount;
    public readonly int TreatySlots = treatySlots;
    public readonly int TreatiesInForce = treatiesInForce;

    public readonly List<FactionEntry> Factions = factions;
    public readonly List<TreatyEntry> Treaties = treaties;
    public readonly List<ActiveTreatyEntry> Active = active;
    public readonly List<PetitionEntry> Petitions = petitions;

    // The credit and the investments, the cities of the map, the licences, the land, and what is done with the townsfolk.
    public readonly CreditState Credit = credit;
    public readonly CitiesState Cities = cities;
    public readonly LicensesState Licenses = licenses;
    public readonly LandState Land = land;
    public readonly PeopleState People = people;
    public readonly AdminState Admin = admin;
    public readonly GrowthState Growth = growth;
    public readonly QuotaState Quota = quota;
    public readonly SubsidyState Subsidy = subsidy;
}

// From the window.
[Serializable, NetSerializable]
public sealed class ProposeTreatyMessage(string treaty, string faction) : BoundUserInterfaceMessage
{
    public readonly string Treaty = treaty;
    public readonly string Faction = faction;
}

[Serializable, NetSerializable]
public sealed class BreakTreatyMessage(string treaty, string faction) : BoundUserInterfaceMessage
{
    public readonly string Treaty = treaty;
    public readonly string Faction = faction;
}

[Serializable, NetSerializable]
public enum PetitionAnswer : byte
{
    Accept,
    Decline,
    Bargain,
}

[Serializable, NetSerializable]
public sealed class AnswerPetitionMessage(int petition, PetitionAnswer answer) : BoundUserInterfaceMessage
{
    public readonly int Petition = petition;
    public readonly PetitionAnswer Answer = answer;
}
