// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.WorldMarket;

// Key of the window of the console of the colony administration.
[Serializable, NetSerializable]
public enum ColonyConsoleUi : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class IndexEntry(string id, string name, Color color, float value, float change, List<float> history)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly Color Color = color;
    public readonly float Value = value;
    public readonly float Change = change;
    public readonly List<float> History = history;
}

[Serializable, NetSerializable]
public sealed class NewsEntry(string text, int agoSeconds)
{
    public readonly string Text = text;
    public readonly int AgoSeconds = agoSeconds;
}

// What a company of the world pays and asks now, in percent of its usual prices; the embargo that is on it, in seconds (0 for none), and the agreement that is signed with it.
[Serializable, NetSerializable]
public sealed class CompanyEntry(string company, string categories, int sellPercent, int buyPercent, int dutyPercent, int embargoSeconds, string? agreement)
{
    public readonly string Company = company;
    public readonly string Categories = categories;
    public readonly int SellPercent = sellPercent;
    public readonly int BuyPercent = buyPercent;
    public readonly int DutyPercent = dutyPercent;
    public readonly int EmbargoSeconds = embargoSeconds;
    public readonly string? Agreement = agreement;
}

[Serializable, NetSerializable]
public sealed class AgreementEntry(string id, string name, string description, string company, int cost, int durationSeconds, int dutyCut, int sellBonus, bool active, int leftSeconds, bool canAfford)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly string Company = company;
    public readonly int Cost = cost;
    public readonly int DurationSeconds = durationSeconds;
    public readonly int DutyCut = dutyCut;
    public readonly int SellBonus = sellBonus;
    public readonly bool Active = active;
    public readonly int LeftSeconds = leftSeconds;
    public readonly bool CanAfford = canAfford;
}

[Serializable, NetSerializable]
public sealed class QuotaEntry(string id, string text, int amount, int progress, int leftSeconds, int reward, int fine)
{
    public readonly string Id = id;
    public readonly string Text = text;
    public readonly int Amount = amount;
    public readonly int Progress = progress;
    public readonly int LeftSeconds = leftSeconds;
    public readonly int Reward = reward;
    public readonly int Fine = fine;
}

[Serializable, NetSerializable]
public sealed class QuotaRecordEntry(string text, bool done, int agoSeconds)
{
    public readonly string Text = text;
    public readonly bool Done = done;
    public readonly int AgoSeconds = agoSeconds;
}

[Serializable, NetSerializable]
public sealed class VoyageEntry(int nextSeconds, int intervalSeconds, int delayedSeconds, int urgentCost, int urgentCooldownSeconds, bool canUrgent)
{
    public readonly int NextSeconds = nextSeconds;
    public readonly int IntervalSeconds = intervalSeconds;
    public readonly int DelayedSeconds = delayedSeconds;
    public readonly int UrgentCost = urgentCost;
    public readonly int UrgentCooldownSeconds = urgentCooldownSeconds;
    public readonly bool CanUrgent = canUrgent;
}

[Serializable, NetSerializable]
public sealed class WareEntry(string id, string name, int price, int heat, int incoming)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly int Price = price;
    public readonly int Heat = heat;

    // How many of it are on their way with the next voyage.
    public readonly int Incoming = incoming;
}

[Serializable, NetSerializable]
public sealed class OrderEntry(string id, string text, int amount, int pricePerUnit, int heat, int bonus, bool taken, int progress, int leftSeconds)
{
    public readonly string Id = id;
    public readonly string Text = text;
    public readonly int Amount = amount;
    public readonly int PricePerUnit = pricePerUnit;
    public readonly int Heat = heat;
    public readonly int Bonus = bonus;
    public readonly bool Taken = taken;
    public readonly int Progress = progress;
    public readonly int LeftSeconds = leftSeconds;
}

[Serializable, NetSerializable]
public sealed class OrgEntry(string id, string name, string description, Color color, int standing, int heat, List<WareEntry> wares, List<OrderEntry> orders)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly Color Color = color;
    public readonly int Standing = standing;
    public readonly int Heat = heat;
    public readonly List<WareEntry> Wares = wares;
    public readonly List<OrderEntry> Orders = orders;
}

[Serializable, NetSerializable]
public sealed class PartyEntry(string id, string name, int cost, bool called)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly int Cost = cost;
    public readonly bool Called = called;
}

// Everything that the console shows: the treasury and the taxes, the indices and the news, the companies and the agreements, the quotas, the voyages, the black market and the services.
[Serializable, NetSerializable]
public sealed class ColonyConsoleState(
    float budget,
    float salesTax,
    float incomeTax,
    float tradeTax,
    int dutyPercent,
    int standing,
    int inspectionSeconds,
    List<string> factionEmbargoes,
    List<string> factionPacts,
    List<IndexEntry> indices,
    List<NewsEntry> news,
    List<CompanyEntry> companies,
    List<AgreementEntry> agreements,
    List<QuotaEntry> quotas,
    List<QuotaRecordEntry> quotaLog,
    VoyageEntry voyage,
    List<OrgEntry> orgs,
    List<PartyEntry> parties) : BoundUserInterfaceState
{
    public readonly float Budget = budget;
    public readonly float SalesTax = salesTax;
    public readonly float IncomeTax = incomeTax;
    public readonly float TradeTax = tradeTax;

    // The import duty that the colony pays now, the standing with the mother company, and how long the inspection goes on.
    public readonly int DutyPercent = dutyPercent;
    public readonly int Standing = standing;
    public readonly int InspectionSeconds = inspectionSeconds;

    // The embargoes and the trade pacts that the ambassadors of the factions have made (they were on the old console too).
    public readonly List<string> FactionEmbargoes = factionEmbargoes;
    public readonly List<string> FactionPacts = factionPacts;

    public readonly List<IndexEntry> Indices = indices;
    public readonly List<NewsEntry> News = news;
    public readonly List<CompanyEntry> Companies = companies;
    public readonly List<AgreementEntry> Agreements = agreements;
    public readonly List<QuotaEntry> Quotas = quotas;
    public readonly List<QuotaRecordEntry> QuotaLog = quotaLog;
    public readonly VoyageEntry Voyage = voyage;
    public readonly List<OrgEntry> Orgs = orgs;
    public readonly List<PartyEntry> Parties = parties;
}

// From the window.
[Serializable, NetSerializable]
public sealed class SignAgreementMessage(string agreement) : BoundUserInterfaceMessage
{
    public readonly string Agreement = agreement;
}

[Serializable, NetSerializable]
public sealed class BreakAgreementMessage(string agreement) : BoundUserInterfaceMessage
{
    public readonly string Agreement = agreement;
}

[Serializable, NetSerializable]
public sealed class UrgentVoyageMessage : BoundUserInterfaceMessage;


[Serializable, NetSerializable]
public sealed class BuyWareMessage(string org, string ware) : BoundUserInterfaceMessage
{
    public readonly string Org = org;
    public readonly string Ware = ware;
}

[Serializable, NetSerializable]
public sealed class TakeOrderMessage(string org, string order) : BoundUserInterfaceMessage
{
    public readonly string Org = org;
    public readonly string Order = order;
}
