// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Government;

// Key of the window of the console of the people, which is for the townsfolk (the players): they write to the mayor, vote, take the orders of the city and make deals under the seal of the notary.
[Serializable, NetSerializable]
public enum CitizenConsoleUi : byte
{
    Key,
}

// Everything that the window of the console of the people shows. The appeals and the votes are public (everyone who stands by the console reads them), and the one who looks is told from the others by
// the window, which knows its own body.
[Serializable, NetSerializable]
public sealed class CitizenConsoleState(
    List<AppealEntry> appeals, PollEntry? poll, List<PollEntry> pastPolls, List<OrderEntry> orders,
    List<ContractKindEntry> kinds, List<NearbyEntry> nearby, List<PlotChoiceEntry> plots, List<ProposalEntry> proposals, List<ContractEntry> registry,
    int maxAppealLength, int maxSubjectLength, CitizenAdminState admin, SubsidyState subsidy, TradeState trade, WorldShopState world) : BoundUserInterfaceState
{
    public readonly List<AppealEntry> Appeals = appeals;
    public readonly PollEntry? Poll = poll;
    public readonly List<PollEntry> PastPolls = pastPolls;
    public readonly List<OrderEntry> Orders = orders;
    public readonly List<ContractKindEntry> Kinds = kinds;
    public readonly List<NearbyEntry> Nearby = nearby;
    public readonly List<PlotChoiceEntry> Plots = plots;
    public readonly List<ProposalEntry> Proposals = proposals;
    public readonly List<ContractEntry> Registry = registry;
    public readonly int MaxAppealLength = maxAppealLength;
    public readonly int MaxSubjectLength = maxSubjectLength;
    public readonly CitizenAdminState Admin = admin;
    public readonly SubsidyState Subsidy = subsidy;
    public readonly TradeState Trade = trade;
    public readonly WorldShopState World = world;
}

// ---------------------------------------------------------------- From the window of the people

[Serializable, NetSerializable]
public sealed class SubmitAppealMessage(string text) : BoundUserInterfaceMessage
{
    public readonly string Text = text;
}

[Serializable, NetSerializable]
public sealed class VoteMessage(int poll, int option) : BoundUserInterfaceMessage
{
    public readonly int Poll = poll;
    public readonly int Option = option;
}

[Serializable, NetSerializable]
public sealed class DeliverOrderMessage(int order) : BoundUserInterfaceMessage
{
    public readonly int Order = order;
}

[Serializable, NetSerializable]
public sealed class ProposeContractMessage(string kind, NetEntity partner, int sum, string subject, int plot) : BoundUserInterfaceMessage
{
    public readonly string Kind = kind;
    public readonly NetEntity Partner = partner;
    public readonly int Sum = sum;
    public readonly string Subject = subject;
    public readonly int Plot = plot;
}

[Serializable, NetSerializable]
public sealed class SignContractMessage(int proposal) : BoundUserInterfaceMessage
{
    public readonly int Proposal = proposal;
}

[Serializable, NetSerializable]
public sealed class CancelContractMessage(int proposal) : BoundUserInterfaceMessage
{
    public readonly int Proposal = proposal;
}
