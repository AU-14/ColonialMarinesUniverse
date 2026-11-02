// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.Government;
using Robust.Server.GameObjects;

namespace Content.Server.CMU14.Government;

// The console of the people: the window of it is for the townsfolk (the players). What is sent to it is made here; what the players do in it is in .Appeals.cs, .People.cs and .Notary.cs.
public sealed partial class ColonyGovernmentSystem
{
    private void OnCitizenOpened(Entity<CitizenConsoleComponent> console, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is not CitizenConsoleUi)
            return;

        var government = EnsureGovernment();
        _ui.SetUiState(console.Owner, CitizenConsoleUi.Key, BuildCitizen(government.Comp, console.Owner));
    }

    // The state is made for every console of the people that is open, with the people who stand by it.
    private void RefreshCitizenUis(ColonyGovernmentComponent comp)
    {
        var consoles = EntityQueryEnumerator<CitizenConsoleComponent>();
        while (consoles.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, CitizenConsoleUi.Key))
                _ui.SetUiState(uid, CitizenConsoleUi.Key, BuildCitizen(comp, uid));
        }
    }

    private CitizenConsoleState BuildCitizen(ColonyGovernmentComponent comp, EntityUid console)
    {
        var now = _timing.CurTime;

        var kinds = _proto.EnumeratePrototypes<ColonyContractKindPrototype>()
            .OrderBy(k => k.Mode)
            .Select(k => new ContractKindEntry(k.ID, Loc.GetString(k.Name), Loc.GetString(k.Description), k.Mode, k.Fee))
            .ToList();

        var plots = comp.Plots.Select(p => new PlotChoiceEntry(p.Id, PlotLabel(comp, p), p.Holder)).ToList();

        var proposals = comp.Proposals
            .Select(p => new ProposalEntry(p.Id, _proto.TryIndex<ColonyContractKindPrototype>(p.Kind, out var kind) ? Loc.GetString(kind.Name) : p.Kind,
                GetNetEntity(p.First), p.FirstName, GetNetEntity(p.Second), p.SecondName, p.Sum, p.Subject,
                p.Plot != 0 && comp.Plots.FirstOrDefault(x => x.Id == p.Plot) is { } plot ? PlotLabel(comp, plot) : string.Empty,
                Math.Max(0, (int) Math.Ceiling((p.Until - now).TotalSeconds))))
            .ToList();

        return new CitizenConsoleState(
            AppealEntries(comp, 30), PollEntryOrNull(comp.Poll, now, false), comp.PastPolls.Select(p => PollEntryOf(p, now, true)).ToList(),
            OrderEntries(comp, now), kinds, NearbyPeople(console), plots, proposals, RegistryEntries(comp).Take(10).ToList(),
            MaxAppealLength, MaxSubjectLength,
            new CitizenAdminState(AuctionEntries(comp, now), TenderEntries(comp, now), LoanOfferEntries(comp), PlayerLoanEntries(comp, now)),
            BuildSubsidy(comp), BuildTrade(comp), BuildWorldShop(comp));
    }
}
