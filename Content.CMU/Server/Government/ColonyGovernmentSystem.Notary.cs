// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.Government;

namespace Content.Server.CMU14.Government;

// The notary of the city. Two players who stand by the console of the people make a deal (a lease of a room, the sale of a plot, a pledge): the first offers it, the second signs it. The sum goes from the
// second to the first, the two share the fee of the notary, which goes to the budget, and each gets a contract with the seal of the city. A sale of a plot also changes the holder of the plot. The mayor sees
// the register and what the notary has taken in his window.
public sealed partial class ColonyGovernmentSystem
{
    public const int MaxSubjectLength = 100;
    public const int MaxContractSum = 100000;
    public const int ProposalLifetimeMinutes = 3;
    public const int MaxRegister = 40;

    #region The deals

    private void OnProposeContract(Entity<CitizenConsoleComponent> console, ref ProposeContractMessage msg)
    {
        var comp = EnsureGovernment().Comp;
        var actor = msg.Actor;
        var partner = GetEntity(msg.Partner);
        if (!_proto.TryIndex<ColonyContractKindPrototype>(msg.Kind, out var kind) || partner == actor || !Near(console.Owner, actor) || !Near(console.Owner, partner))
            return;

        var subject = Clean(msg.Subject, MaxSubjectLength);
        if (subject.Length < 3 || msg.Sum < 0 || msg.Sum > MaxContractSum || comp.Proposals.Any(p => p.First == actor))
        {
            _popup.PopupEntity(Loc.GetString("rumc-notary-refused"), console.Owner, actor);
            return;
        }

        var plotId = 0;
        if (kind.Mode == ContractMode.PlotSale)
        {
            var requested = msg.Plot;
            if (comp.Plots.FirstOrDefault(p => p.Id == requested) is not { } plot || plot.HolderEntity != actor)
            {
                _popup.PopupEntity(Loc.GetString("rumc-notary-not-yours"), console.Owner, actor);
                return;
            }

            plotId = plot.Id;
        }

        comp.Proposals.Add(new ContractProposal(comp.NextProposalId++, kind.ID, actor, MetaData(actor).EntityName, partner, MetaData(partner).EntityName,
            msg.Sum, subject, plotId, _timing.CurTime + TimeSpan.FromMinutes(ProposalLifetimeMinutes)));

        _popup.PopupEntity(Loc.GetString("rumc-notary-offered", ("name", MetaData(partner).EntityName)), console.Owner, actor);
        _popup.PopupEntity(Loc.GetString("rumc-notary-offered-to-you", ("name", MetaData(actor).EntityName)), console.Owner, partner);
        Refresh();
    }

    // Why a deal cannot be sealed now (null if it can).
    private string? ContractBlocked(ColonyGovernmentComponent comp, EntityUid console, ContractProposal proposal, ColonyContractKindPrototype kind)
    {
        if (!Near(console, proposal.First) || !Near(console, proposal.Second))
            return Loc.GetString("rumc-notary-both-here");

        var firstShare = kind.Fee / 2;
        var secondShare = kind.Fee - firstShare;
        if (_bank.FindOwnedCard(proposal.First) == null || _bank.FindOwnedCard(proposal.Second) == null)
            return Loc.GetString("rumc-notary-no-account");

        if (BalanceOf(proposal.Second) < proposal.Sum + secondShare)
            return Loc.GetString("rumc-notary-second-poor", ("sum", proposal.Sum + secondShare));

        if (BalanceOf(proposal.First) + proposal.Sum < firstShare)
            return Loc.GetString("rumc-notary-first-poor", ("sum", firstShare));

        if (proposal.Plot != 0)
        {
            var plotId = proposal.Plot;
            var plot = comp.Plots.FirstOrDefault(p => p.Id == plotId);
            if (plot == null || plot.HolderEntity != proposal.First)
                return Loc.GetString("rumc-notary-plot-gone");

            var secondName = proposal.SecondName;
            if (comp.Plots.Count(p => p.Holder == secondName) >= MaxPlotsPerPerson)
                return Loc.GetString("rumc-notary-plot-limit", ("max", MaxPlotsPerPerson));
        }

        return null;
    }

    private void OnSignContract(Entity<CitizenConsoleComponent> console, ref SignContractMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var comp = government.Comp;
        var actor = msg.Actor;
        var proposalId = msg.Proposal;
        if (comp.Proposals.FirstOrDefault(p => p.Id == proposalId) is not { } proposal || proposal.Second != actor
            || !_proto.TryIndex<ColonyContractKindPrototype>(proposal.Kind, out var kind))
            return;

        if (ContractBlocked(comp, console.Owner, proposal, kind) is { } why)
        {
            _popup.PopupEntity(why, console.Owner, actor);
            return;
        }

        var fee = kind.Fee;
        var firstShare = fee / 2;
        var secondShare = fee - firstShare;

        // The sum goes from the second to the first, and each pays his half of the fee into the budget.
        Pay(proposal.Second, -(proposal.Sum + secondShare));
        Pay(proposal.First, proposal.Sum - firstShare);
        _budget.AddToBudget(fee);
        comp.NotaryTotal += fee;

        string? plotLabel = null;
        if (proposal.Plot != 0)
        {
            var plotId = proposal.Plot;
            if (comp.Plots.FirstOrDefault(p => p.Id == plotId) is { } plot)
            {
                plotLabel = PlotLabel(comp, plot);
                if (plot.Card is { } oldCard && TryComp(oldCard, out CityLicenseCardComponent? oldCardComp))
                    oldCardComp.Revoked = true;

                plot.Holder = proposal.SecondName;
                plot.HolderEntity = proposal.Second;
                plot.Missed = 0;

                if (comp.Zones.FirstOrDefault(z => z.Id == plot.Zone) is { } zone)
                {
                    plot.Card = PrintDeed(plot, zone, console.Owner);
                    _hands.TryPickupAnyHand(proposal.Second, plot.Card.Value);
                }
            }
        }

        var number = comp.NextContractNumber++;
        var kindName = Loc.GetString(kind.Name);
        var lines = new List<string>
        {
            "[color=#a07000][bold]" + Loc.GetString("rumc-notary-paper-title", ("number", number)) + "[/bold][/color]",
            string.Empty,
            Loc.GetString("rumc-notary-paper-kind", ("kind", kindName)),
            Loc.GetString("rumc-notary-paper-parties", ("first", proposal.FirstName), ("second", proposal.SecondName)),
            Loc.GetString("rumc-notary-paper-sum", ("sum", proposal.Sum)),
            Loc.GetString("rumc-notary-paper-subject", ("subject", proposal.Subject)),
        };

        if (plotLabel != null)
            lines.Add(Loc.GetString("rumc-notary-paper-plot", ("plot", plotLabel)));

        lines.Add(Loc.GetString("rumc-notary-paper-fee", ("fee", fee)));
        lines.Add(string.Empty);
        lines.Add("[color=#aa0000][bold]" + Loc.GetString("rumc-notary-paper-seal") + "[/bold][/color]");

        var text = string.Join("\n", lines);
        var paperName = Loc.GetString("rumc-notary-paper-name", ("number", number));
        PrintPaper(paperName, text, proposal.First);
        PrintPaper(paperName, text, proposal.Second);

        comp.Contracts.Insert(0, new ContractRecord(number, kind.ID, proposal.FirstName, proposal.SecondName, proposal.Sum, proposal.Subject, fee, _timing.CurTime));
        if (comp.Contracts.Count > MaxRegister)
            comp.Contracts.RemoveRange(MaxRegister, comp.Contracts.Count - MaxRegister);

        comp.Proposals.Remove(proposal);
        AddProgress(comp, proposal.First, PlanKind.Deals, 1);
        AddProgress(comp, proposal.Second, PlanKind.Deals, 1);
        Refresh();
    }

    private void OnCancelContract(Entity<CitizenConsoleComponent> console, ref CancelContractMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var actor = msg.Actor;
        var proposalId = msg.Proposal;
        if (government.Comp.Proposals.FirstOrDefault(p => p.Id == proposalId) is not { } proposal || (proposal.First != actor && proposal.Second != actor))
            return;

        government.Comp.Proposals.Remove(proposal);
        Refresh();
    }

    private void UpdateProposals(ColonyGovernmentComponent comp, TimeSpan now)
    {
        comp.Proposals.RemoveAll(p => p.Until <= now || !Exists(p.First) || !Exists(p.Second));
    }

    #endregion

    #region The state

    private string PlotLabel(ColonyGovernmentComponent comp, LandPlot plot)
    {
        var zone = comp.Zones.FirstOrDefault(z => z.Id == plot.Zone);
        if (zone == null)
            return Loc.GetString("rumc-notary-plot-label-lost", ("id", plot.Id));

        var corner = PlotCorner(zone, plot.Cell);
        return Loc.GetString("rumc-notary-plot-label", ("id", plot.Id), ("zone", ZoneName(zone)), ("x", corner.X), ("y", corner.Y));
    }

    private List<ContractEntry> RegistryEntries(ColonyGovernmentComponent comp)
    {
        return comp.Contracts
            .Select(c => new ContractEntry(c.Number, _proto.TryIndex<ColonyContractKindPrototype>(c.Kind, out var kind) ? Loc.GetString(kind.Name) : c.Kind,
                c.FirstName, c.SecondName, c.Sum, c.Subject, c.Fee))
            .ToList();
    }

    #endregion
}
