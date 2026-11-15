// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using Content.Client.CMU14.WorldMarket;
using Content.Shared.CMU14.Government;
using static Content.Client.CMU14.WorldMarket.TermKit;

namespace Content.Client.CMU14.Government;

// The tab of the subsidies in the console of the people: the kinds of business that a subsidy can start, the application, and how the plan of the subsidy that was approved is going.
public sealed partial class CitizenConsoleWindow
{
    private void BuildSubsidies(CitizenConsoleState state)
    {
        var subsidy = state.Subsidy;
        var (me, myName) = Me();

        var head = Column(6);
        head.AddChild(Gold(Loc.GetString("rumc-subsidy-title")));
        head.AddChild(Wrapped(Loc.GetString("rumc-citizen-subsidy-hint"), TermPalette.Muted));
        _page.AddChild(Card(head));

        // My subsidy that is paid, and how the plan goes.
        var mine = subsidy.Active.FirstOrDefault(s => s.Name == myName);
        if (mine != null)
        {
            var id = mine.Id;
            var active = Column(6);
            active.AddChild(Gold(Loc.GetString("rumc-citizen-subsidy-yours")));
            active.AddChild(Text(Loc.GetString("rumc-subsidy-active-line", ("name", mine.Name), ("business", mine.Business),
                ("form", Loc.GetString(mine.Form == SubsidyForm.Loan ? "rumc-subsidy-form-loan" : "rumc-subsidy-form-grant"))), TermPalette.Text));
            active.AddChild(Muted(Loc.GetString("rumc-subsidy-money-line", ("paid", Money(mine.Paid)), ("total", Money(mine.Total)), ("held", Money(mine.Held)))));
            active.AddChild(Live(s =>
            {
                var current = s.Subsidy.Active.FirstOrDefault(x => x.Id == id);
                return current == null ? string.Empty : Loc.GetString("rumc-subsidy-stage-line", ("stage", current.Stage), ("progress", current.Progress), ("target", current.Target),
                    ("time", Time(current.LeftSeconds)));
            }, TermPalette.Gold));
            active.AddChild(Wrapped(Loc.GetString("rumc-citizen-subsidy-warning"), TermPalette.Muted));
            _page.AddChild(Card(active));
        }

        // My application that waits.
        var waiting = subsidy.Applications.FirstOrDefault(a => a.Person == me);
        if (waiting != null)
        {
            var id = waiting.Id;
            var column = Column(6);
            column.AddChild(Gold(Loc.GetString("rumc-citizen-subsidy-waiting")));
            column.AddChild(Text(Loc.GetString("rumc-subsidy-application-line", ("name", waiting.Name), ("business", waiting.Business),
                ("form", Loc.GetString(waiting.Form == SubsidyForm.Loan ? "rumc-subsidy-form-loan" : "rumc-subsidy-form-grant")), ("sum", Money(waiting.Amount))), TermPalette.Text));
            column.AddChild(Danger(Loc.GetString("rumc-citizen-subsidy-withdraw"), () => OnWithdrawApplication?.Invoke(id), true, 190));
            _page.AddChild(Card(column));
        }

        // The kinds of business, and the buttons to apply.
        var canApply = mine == null && waiting == null;
        foreach (var business in subsidy.Businesses)
        {
            var id = business.Id;
            var column = Column(4);
            column.AddChild(Text(business.Name, TermPalette.Text));
            column.AddChild(Wrapped(business.Description, TermPalette.Muted));
            column.AddChild(Muted(Loc.GetString("rumc-citizen-subsidy-premises", ("zone", business.Zone))));
            column.AddChild(Muted(Loc.GetString("rumc-subsidy-plan-line", ("upfront", business.Upfront), ("plan", business.Plan), ("time", Time(business.DeadlineSeconds)))));

            var buttons = Row(8);
            buttons.AddChild(Action(Loc.GetString("rumc-citizen-subsidy-apply-grant", ("sum", Money(business.Grant))), () => OnApplySubsidy?.Invoke(id, SubsidyForm.Grant), canApply, 280));
            buttons.AddChild(Action(Loc.GetString("rumc-citizen-subsidy-apply-loan", ("sum", Money(business.Loan))), () => OnApplySubsidy?.Invoke(id, SubsidyForm.Loan), canApply, 280));
            column.AddChild(buttons);
            _page.AddChild(Card(column));
        }
    }
}
