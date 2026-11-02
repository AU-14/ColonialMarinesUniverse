// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using Content.Client.CMU14.WorldMarket;
using Content.Shared.CMU14.Government;
using static Content.Client.CMU14.WorldMarket.TermKit;

namespace Content.Client.CMU14.Government;

// The tab of the subsidies in the window of the mayor: the applications that wait (approved or turned down), and the subsidies that are being paid in tranches.
public sealed partial class ColonyGovernmentWindow
{
    private static string SubsidiesSignature(ColonyGovernmentState state)
    {
        var subsidy = state.Subsidy;
        return "y|" + string.Join(";", subsidy.Applications.Select(a => $"{a.Id}:{a.Blocked.Length > 0}"))
               + "|" + string.Join(";", subsidy.Active.Select(s => $"{s.Id}:{s.Paid}:{s.Stage}"));
    }

    private void BuildSubsidies(ColonyGovernmentState state)
    {
        var subsidy = state.Subsidy;

        var head = Column(6);
        head.AddChild(Gold(Loc.GetString("rumc-subsidy-title")));
        head.AddChild(Wrapped(Loc.GetString("rumc-subsidy-hint"), TermPalette.Muted));
        _page.AddChild(Card(head));

        var applications = Column(6);
        applications.AddChild(Gold(Loc.GetString("rumc-subsidy-applications")));
        if (subsidy.Applications.Count == 0)
            applications.AddChild(Muted(Loc.GetString("rumc-subsidy-applications-none")));

        foreach (var application in subsidy.Applications)
        {
            var id = application.Id;
            applications.AddChild(Rule());
            applications.AddChild(Text(Loc.GetString("rumc-subsidy-application-line", ("name", application.Name), ("business", application.Business),
                ("form", Loc.GetString(application.Form == SubsidyForm.Loan ? "rumc-subsidy-form-loan" : "rumc-subsidy-form-grant")), ("sum", Money(application.Amount))), TermPalette.Text));

            var business = subsidy.Businesses.FirstOrDefault(b => b.Name == application.Business);
            if (business != null)
                applications.AddChild(Muted(Loc.GetString("rumc-subsidy-plan-line", ("upfront", business.Upfront), ("plan", business.Plan), ("time", Time(business.DeadlineSeconds)))));

            if (application.Blocked.Length > 0)
                applications.AddChild(Text(application.Blocked, TermPalette.Bad));

            var buttons = Row(8);
            buttons.AddChild(Action(Loc.GetString("rumc-subsidy-approve"), () => OnApproveSubsidy?.Invoke(id), application.Blocked.Length == 0, 170));
            buttons.AddChild(Danger(Loc.GetString("rumc-subsidy-reject"), () => OnRejectSubsidy?.Invoke(id), true, 150));
            applications.AddChild(buttons);
        }

        _page.AddChild(Card(applications));

        var active = Column(6);
        active.AddChild(Gold(Loc.GetString("rumc-subsidy-active")));
        if (subsidy.Active.Count == 0)
            active.AddChild(Muted(Loc.GetString("rumc-subsidy-active-none")));

        foreach (var entry in subsidy.Active)
        {
            var id = entry.Id;
            active.AddChild(Rule());
            active.AddChild(Text(Loc.GetString("rumc-subsidy-active-line", ("name", entry.Name), ("business", entry.Business),
                ("form", Loc.GetString(entry.Form == SubsidyForm.Loan ? "rumc-subsidy-form-loan" : "rumc-subsidy-form-grant"))), TermPalette.Text));
            active.AddChild(Muted(Loc.GetString("rumc-subsidy-money-line", ("paid", Money(entry.Paid)), ("total", Money(entry.Total)), ("held", Money(entry.Held)))));
            active.AddChild(Live(s =>
            {
                var current = s.Subsidy.Active.FirstOrDefault(x => x.Id == id);
                return current == null ? string.Empty : Loc.GetString("rumc-subsidy-stage-line", ("stage", current.Stage), ("progress", current.Progress), ("target", current.Target),
                    ("time", Time(current.LeftSeconds)));
            }, TermPalette.Gold));
            active.AddChild(Danger(Loc.GetString("rumc-subsidy-cancel"), () => OnCancelSubsidy?.Invoke(id), true, 190));
        }

        _page.AddChild(Card(active));

        var catalog = Column(4);
        catalog.AddChild(Gold(Loc.GetString("rumc-subsidy-catalog")));
        foreach (var business in subsidy.Businesses)
        {
            catalog.AddChild(Rule());
            catalog.AddChild(Text(business.Name, TermPalette.Text));
            catalog.AddChild(Muted(Loc.GetString("rumc-subsidy-catalog-line", ("zone", business.Zone), ("grant", Money(business.Grant)), ("loan", Money(business.Loan)), ("upfront", business.Upfront))));
            catalog.AddChild(Wrapped(business.Plan, TermPalette.Muted));
        }

        _page.AddChild(Card(catalog));
    }
}
