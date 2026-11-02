// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Government;

// What the mayor and the players have done together this round: the rewards given, the orders, the appeals and the votes, and the deals that were sealed.
public sealed partial class ColonyGovernmentComponent
{
    public readonly List<AwardRecord> Awards = new();

    public readonly List<MunicipalOrder> Orders = new();
    public int NextOrderId = 1;

    public readonly List<Appeal> Appeals = new();
    public int NextAppealId = 1;

    // The vote that goes now (one at a time) and the votes that are over.
    public Poll? Poll;
    public readonly List<Poll> PastPolls = new();
    public int NextPollId = 1;

    public readonly List<ContractProposal> Proposals = new();
    public int NextProposalId = 1;
    public readonly List<ContractRecord> Contracts = new();
    public int NextContractNumber = 1;

    // What the notary has taken in fees, for the round.
    public int NotaryTotal;
}

public sealed class AwardRecord(string award, string holder, AwardCategory category, string reason, TimeSpan at)
{
    public readonly string Award = award;
    public readonly string Holder = holder;
    public readonly AwardCategory Category = category;
    public readonly string Reason = reason;
    public readonly TimeSpan At = at;
}

// An order: how much of the thing is wanted and how much is left, the price for each, and until when. The money for all of it was taken out of the budget at once, and what is left is given back when
// the order is cancelled or runs out.
public sealed class MunicipalOrder(int id, string kind, int total, int price, TimeSpan until)
{
    public readonly int Id = id;
    public readonly string Kind = kind;
    public readonly int Total = total;
    public readonly int Price = price;
    public readonly TimeSpan Until = until;
    public int Left = total;

    // The one who won the tender for it: only he may fill it (nothing for an order that is open to all).
    public string? Contractor;
}

[Serializable, NetSerializable]
public enum AppealStatus : byte
{
    Open,
    Accepted,
    Rejected,
}

// A letter of a player to the mayor, and the answer.
public sealed class Appeal(int id, string author, EntityUid? authorEntity, string text, TimeSpan at)
{
    public readonly int Id = id;
    public readonly string Author = author;
    public readonly EntityUid? AuthorEntity = authorEntity;
    public readonly string Text = text;
    public readonly TimeSpan At = at;
    public AppealStatus Status;
    public string Reply = string.Empty;
}

// A vote of the players: the question, the answers, who has voted (by the account, so that one cannot vote twice with two bodies), and when it ends.
public sealed class Poll(int id, string question, List<string> options, TimeSpan until)
{
    public readonly int Id = id;
    public readonly string Question = question;
    public readonly List<string> Options = options;
    public readonly TimeSpan Until = until;
    public readonly Dictionary<NetUserId, int> Votes = new();
    public readonly Dictionary<NetUserId, string> VoterNames = new();
}

// A deal that one of the two has offered at the console and the other has not yet signed. The sum goes from the second to the first when it is signed.
public sealed class ContractProposal(int id, string kind, EntityUid first, string firstName, EntityUid second, string secondName, int sum, string subject, int plot, TimeSpan until)
{
    public readonly int Id = id;
    public readonly string Kind = kind;
    public readonly EntityUid First = first;
    public readonly string FirstName = firstName;
    public readonly EntityUid Second = second;
    public readonly string SecondName = secondName;
    public readonly int Sum = sum;
    public readonly string Subject = subject;

    // The plot that is sold (0 if the deal is not a sale of a plot).
    public readonly int Plot = plot;
    public readonly TimeSpan Until = until;
}

// A deal that was sealed, for the register of the notary.
public sealed class ContractRecord(int number, string kind, string firstName, string secondName, int sum, string subject, int fee, TimeSpan at)
{
    public readonly int Number = number;
    public readonly string Kind = kind;
    public readonly string FirstName = firstName;
    public readonly string SecondName = secondName;
    public readonly int Sum = sum;
    public readonly string Subject = subject;
    public readonly int Fee = fee;
    public readonly TimeSpan At = at;
}
