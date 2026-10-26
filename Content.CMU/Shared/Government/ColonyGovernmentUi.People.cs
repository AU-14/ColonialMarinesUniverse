// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Government;

// What the mayor and the console of the people show of the rewards, the orders, the appeals, the votes and the deals, and what they send. The entries are the same in the two windows.

[Serializable, NetSerializable]
public sealed class AwardTypeEntry(string id, AwardCategory category, string name, string description, int cost, int cash, bool medal)
{
    public readonly string Id = id;
    public readonly AwardCategory Category = category;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly int Cost = cost;
    public readonly int Cash = cash;
    public readonly bool Medal = medal;
}

[Serializable, NetSerializable]
public sealed class AwardEntry(string award, string holder, string category, string reason)
{
    public readonly string Award = award;
    public readonly string Holder = holder;
    public readonly string Category = category;
    public readonly string Reason = reason;
}

[Serializable, NetSerializable]
public sealed class OrderKindEntry(string id, string name, string description, int unitPrice, int maxAmount)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly int UnitPrice = unitPrice;
    public readonly int MaxAmount = maxAmount;
}

[Serializable, NetSerializable]
public sealed class OrderEntry(int id, string kind, string description, int total, int left, int price, int leftSeconds, string contractor)
{
    public readonly int Id = id;
    public readonly string Kind = kind;
    public readonly string Description = description;
    public readonly int Total = total;
    public readonly int Left = left;
    public readonly int Price = price;
    public readonly int LeftSeconds = leftSeconds;

    // The one who won the tender for it (empty for an order that is open to all).
    public readonly string Contractor = contractor;
}

[Serializable, NetSerializable]
public sealed class AppealEntry(int id, string author, string text, AppealStatus status, string reply)
{
    public readonly int Id = id;
    public readonly string Author = author;
    public readonly string Text = text;
    public readonly AppealStatus Status = status;
    public readonly string Reply = reply;
}

[Serializable, NetSerializable]
public sealed class PollEntry(int id, string question, List<string> options, List<int> counts, int leftSeconds, bool closed, List<string> voted)
{
    public readonly int Id = id;
    public readonly string Question = question;
    public readonly List<string> Options = options;
    public readonly List<int> Counts = counts;
    public readonly int LeftSeconds = leftSeconds;
    public readonly bool Closed = closed;

    // The names of those who have voted (not what they voted): the window tells the one who looks that his vote is in.
    public readonly List<string> Voted = voted;
}

[Serializable, NetSerializable]
public sealed class ContractKindEntry(string id, string name, string description, ContractMode mode, int fee)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly ContractMode Mode = mode;
    public readonly int Fee = fee;
}

[Serializable, NetSerializable]
public sealed class PlotChoiceEntry(int id, string label, string holder)
{
    public readonly int Id = id;
    public readonly string Label = label;
    public readonly string Holder = holder;
}

[Serializable, NetSerializable]
public sealed class ProposalEntry(int id, string kind, NetEntity first, string firstName, NetEntity second, string secondName, int sum, string subject, string plot, int leftSeconds)
{
    public readonly int Id = id;
    public readonly string Kind = kind;
    public readonly NetEntity First = first;
    public readonly string FirstName = firstName;
    public readonly NetEntity Second = second;
    public readonly string SecondName = secondName;
    public readonly int Sum = sum;
    public readonly string Subject = subject;
    public readonly string Plot = plot;
    public readonly int LeftSeconds = leftSeconds;
}

[Serializable, NetSerializable]
public sealed class ContractEntry(int number, string kind, string first, string second, int sum, string subject, int fee)
{
    public readonly int Number = number;
    public readonly string Kind = kind;
    public readonly string First = first;
    public readonly string Second = second;
    public readonly int Sum = sum;
    public readonly string Subject = subject;
    public readonly int Fee = fee;
}

// What the window of the mayor shows of all this.
[Serializable, NetSerializable]
public sealed class PeopleState(
    List<AwardTypeEntry> awards, List<AwardEntry> history, List<NearbyEntry> nearby,
    List<OrderKindEntry> orderKinds, List<OrderEntry> orders,
    List<AppealEntry> appeals, PollEntry? poll, List<PollEntry> pastPolls,
    List<ContractEntry> registry, int notaryTotal)
{
    public readonly List<AwardTypeEntry> Awards = awards;
    public readonly List<AwardEntry> History = history;
    public readonly List<NearbyEntry> Nearby = nearby;
    public readonly List<OrderKindEntry> OrderKinds = orderKinds;
    public readonly List<OrderEntry> Orders = orders;
    public readonly List<AppealEntry> Appeals = appeals;
    public readonly PollEntry? Poll = poll;
    public readonly List<PollEntry> PastPolls = pastPolls;
    public readonly List<ContractEntry> Registry = registry;

    // What the notary has taken in fees, for the round.
    public readonly int NotaryTotal = notaryTotal;
}

// ---------------------------------------------------------------- From the window of the mayor

[Serializable, NetSerializable]
public sealed class GrantAwardMessage(NetEntity target, string award, string reason) : BoundUserInterfaceMessage
{
    public readonly NetEntity Target = target;
    public readonly string Award = award;
    public readonly string Reason = reason;
}

[Serializable, NetSerializable]
public sealed class CreateOrderMessage(string kind, int amount, int price) : BoundUserInterfaceMessage
{
    public readonly string Kind = kind;
    public readonly int Amount = amount;
    public readonly int Price = price;
}

[Serializable, NetSerializable]
public sealed class CancelOrderMessage(int order) : BoundUserInterfaceMessage
{
    public readonly int Order = order;
}

[Serializable, NetSerializable]
public sealed class AnswerAppealMessage(int appeal, bool accept, string reply) : BoundUserInterfaceMessage
{
    public readonly int Appeal = appeal;
    public readonly bool Accept = accept;
    public readonly string Reply = reply;
}

[Serializable, NetSerializable]
public sealed class StartPollMessage(string question, List<string> options, int minutes) : BoundUserInterfaceMessage
{
    public readonly string Question = question;
    public readonly List<string> Options = options;
    public readonly int Minutes = minutes;
}

[Serializable, NetSerializable]
public sealed class ClosePollMessage : BoundUserInterfaceMessage;
