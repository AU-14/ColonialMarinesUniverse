// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.Government;

namespace Content.Server.CMU14.Government;

// The subsidies for starting a business (see ColonySubsidy.cs). A player applies at the console of the people; the mayor approves. The whole sum is taken out of the budget and held at once, so that the city cannot
// pay what it has not; a part goes to the player at once, and the rest in tranches as the stages of the plan are done: pieces delivered to the orders of the city, rents of the premises paid, deals sealed by the notary.
// If the plan is done, a grant stays with the player and a loan becomes a loan to be paid back. If the time is out first, what is left goes back to the budget and what was paid becomes a loan with a high interest.
public sealed partial class ColonyGovernmentSystem
{
    public const int MaxApplications = 10;
    public const int MaxActiveSubsidies = 5;
    public const int RepaymentInstalments = 3;
    public const int LoanFormInterest = 5;
    public const int FailureInterest = 15;

    private static int SubsidyAmount(ColonyBusinessPrototype business, SubsidyForm form)
    {
        return form == SubsidyForm.Loan ? business.LoanAmount : business.GrantAmount;
    }

    private bool HoldsPlotIn(ColonyGovernmentComponent comp, string name, ZoneKind kind)
    {
        foreach (var plot in comp.Plots.Where(p => p.Holder == name))
        {
            if (comp.Zones.FirstOrDefault(z => z.Id == plot.Zone)?.Kind == kind)
                return true;
        }

        return false;
    }

    #region The applications

    private void OnApplySubsidy(Entity<CitizenConsoleComponent> console, ref ApplySubsidyMessage msg)
    {
        var comp = EnsureGovernment().Comp;
        var actor = msg.Actor;
        if (!_proto.TryIndex<ColonyBusinessPrototype>(msg.Business, out var business) || !Near(console.Owner, actor))
            return;

        var name = MetaData(actor).EntityName;
        string? why = null;
        if (IsDefaulter(comp, name, _timing.CurTime))
            why = Loc.GetString("rumc-auction-defaulter");
        else if (comp.SubsidyApplications.Any(a => a.Person == actor) || comp.Subsidies.Any(s => s.Person == actor))
            why = Loc.GetString("rumc-subsidy-have");
        else if (comp.SubsidyApplications.Count >= MaxApplications)
            why = Loc.GetString("rumc-subsidy-full");
        else if (!HoldsPlotIn(comp, name, business.Zone))
            why = Loc.GetString("rumc-subsidy-need-plot", ("zone", KindName(business.Zone)));
        else if (_bank.FindOwnedCard(actor) == null)
            why = Loc.GetString("rumc-subsidy-no-account");

        if (why != null)
        {
            _popup.PopupEntity(why, console.Owner, actor);
            return;
        }

        comp.SubsidyApplications.Add(new SubsidyApplication(comp.NextApplicationId++, actor, name, business.ID, msg.Form));
        _popup.PopupEntity(Loc.GetString("rumc-subsidy-sent"), console.Owner, actor);
        Refresh();
    }

    private void OnWithdrawApplication(Entity<CitizenConsoleComponent> console, ref WithdrawApplicationMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var actor = msg.Actor;
        var applicationId = msg.Application;
        government.Comp.SubsidyApplications.RemoveAll(a => a.Id == applicationId && a.Person == actor);
        Refresh();
    }

    // Why the mayor cannot approve an application now (null if he can).
    private string? ApplicationBlocked(ColonyGovernmentComponent comp, SubsidyApplication application, ColonyBusinessPrototype business)
    {
        if (!Exists(application.Person))
            return Loc.GetString("rumc-subsidy-gone");

        if (comp.Subsidies.Count >= MaxActiveSubsidies)
            return Loc.GetString("rumc-subsidy-too-many", ("max", MaxActiveSubsidies));

        if (_budget.GetBudget() < SubsidyAmount(business, application.Form))
            return Loc.GetString("rumc-subsidy-no-money");

        if (!HoldsPlotIn(comp, application.Name, business.Zone))
            return Loc.GetString("rumc-subsidy-need-plot", ("zone", KindName(business.Zone)));

        if (_bank.FindOwnedCard(application.Person) == null)
            return Loc.GetString("rumc-subsidy-no-account");

        if (IsDefaulter(comp, application.Name, _timing.CurTime))
            return Loc.GetString("rumc-auction-defaulter");

        return null;
    }

    private void OnApproveSubsidy(Entity<ColonyGovernmentConsoleComponent> console, ref ApproveSubsidyMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var comp = government.Comp;
        var applicationId = msg.Application;
        if (comp.SubsidyApplications.FirstOrDefault(a => a.Id == applicationId) is not { } application
            || !_proto.TryIndex<ColonyBusinessPrototype>(application.Business, out var business) || ApplicationBlocked(comp, application, business) != null)
            return;

        // The whole sum is taken out of the budget and held; a part goes to the player at once.
        var total = SubsidyAmount(business, application.Form);
        _budget.AddToBudget(-total);

        var subsidy = new Subsidy(comp.NextSubsidyId++, application.Person, application.Name, business.ID, application.Form, total,
            _timing.CurTime + TimeSpan.FromSeconds(business.DeadlineSeconds));

        var upfront = total * business.UpfrontPercent / 100;
        if (upfront > 0 && Pay(application.Person, upfront))
            subsidy.Paid = upfront;

        comp.SubsidyApplications.Remove(application);
        comp.Subsidies.Add(subsidy);
        _popup.PopupEntity(Loc.GetString("rumc-subsidy-approved", ("sum", subsidy.Paid)), application.Person, application.Person);

        // A business with no stages is paid in full at once.
        if (business.Stages.Count == 0)
            FinishSubsidy(comp, subsidy, true);

        Refresh();
    }

    private void OnRejectSubsidy(Entity<ColonyGovernmentConsoleComponent> console, ref RejectSubsidyMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var applicationId = msg.Application;
        if (government.Comp.SubsidyApplications.FirstOrDefault(a => a.Id == applicationId) is not { } application)
            return;

        government.Comp.SubsidyApplications.Remove(application);
        if (Exists(application.Person))
            _popup.PopupEntity(Loc.GetString("rumc-subsidy-rejected"), application.Person, application.Person);

        Refresh();
    }

    // The mayor takes a subsidy back: what is left is returned to the budget and nothing is asked of the player.
    private void OnCancelSubsidy(Entity<ColonyGovernmentConsoleComponent> console, ref CancelSubsidyMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var subsidyId = msg.Subsidy;
        if (government.Comp.Subsidies.FirstOrDefault(s => s.Id == subsidyId) is not { } subsidy)
            return;

        government.Comp.Subsidies.Remove(subsidy);
        _budget.AddToBudget(subsidy.Total - subsidy.Paid);
        Refresh();
    }

    #endregion

    #region The plan

    // Something that the player did: if it is what the stage that goes asks for, it is counted, and when the stage is done its share is paid.
    private void AddProgress(ColonyGovernmentComponent comp, EntityUid person, PlanKind kind, int amount, ZoneKind? zone = null)
    {
        foreach (var subsidy in comp.Subsidies.Where(s => s.Person == person).ToList())
        {
            if (!_proto.TryIndex<ColonyBusinessPrototype>(subsidy.Business, out var business) || subsidy.Stage >= business.Stages.Count)
                continue;

            var stage = business.Stages[subsidy.Stage];
            if (stage.Kind != kind || (kind == PlanKind.Rent && zone != null && zone != business.Zone))
                continue;

            subsidy.Progress += amount;
            if (subsidy.Progress < stage.Target)
                continue;

            var pay = Math.Min(subsidy.Total - subsidy.Paid, subsidy.Total * stage.Percent / 100);
            if (pay > 0 && Pay(person, pay))
                subsidy.Paid += pay;

            subsidy.Stage++;
            subsidy.Progress = 0;
            _popup.PopupEntity(Loc.GetString("rumc-subsidy-tranche", ("sum", pay)), person, person);

            if (subsidy.Stage >= business.Stages.Count)
                FinishSubsidy(comp, subsidy, true);
        }
    }

    // The subsidy is over. What is left is returned to the budget. A grant that was earned stays with the player; a loan that was earned, and anything that was not earned, becomes a loan to be paid back (the
    // second with a high interest).
    private void FinishSubsidy(ColonyGovernmentComponent comp, Subsidy subsidy, bool earned)
    {
        comp.Subsidies.Remove(subsidy);
        _budget.AddToBudget(subsidy.Total - subsidy.Paid);

        var repay = !earned || subsidy.Form == SubsidyForm.Loan;
        if (repay && subsidy.Paid > 0 && Exists(subsidy.Person))
        {
            var interest = earned ? LoanFormInterest : FailureInterest;
            var instalment = InstalmentOf(subsidy.Paid, RepaymentInstalments, interest);
            comp.PlayerLoans.Add(new PlayerLoan(comp.NextPlayerLoanId++, subsidy.Person, subsidy.Name, subsidy.Paid, instalment, RepaymentInstalments,
                _timing.CurTime + TimeSpan.FromSeconds(PlayerLoanCycleSeconds)));
        }

        if (Exists(subsidy.Person))
            _popup.PopupEntity(Loc.GetString(earned ? (subsidy.Form == SubsidyForm.Loan ? "rumc-subsidy-done-loan" : "rumc-subsidy-done-grant") : "rumc-subsidy-failed"),
                subsidy.Person, subsidy.Person);
    }

    private void UpdateSubsidies(ColonyGovernmentComponent comp, TimeSpan now)
    {
        comp.SubsidyApplications.RemoveAll(a => !Exists(a.Person));

        foreach (var subsidy in comp.Subsidies.Where(s => s.Deadline <= now).ToList())
            FinishSubsidy(comp, subsidy, false);
    }

    #endregion

    #region The state

    private string PlanText(ColonyBusinessPrototype business)
    {
        var parts = business.Stages.Select(s => Loc.GetString("rumc-subsidy-plan-" + s.Kind.ToString().ToLowerInvariant(), ("target", s.Target), ("percent", s.Percent)));
        return string.Join("; ", parts);
    }

    private SubsidyState BuildSubsidy(ColonyGovernmentComponent comp)
    {
        var now = _timing.CurTime;

        var businesses = _proto.EnumeratePrototypes<ColonyBusinessPrototype>()
            .OrderBy(b => b.GrantAmount)
            .Select(b => new BusinessEntry(b.ID, Loc.GetString(b.Name), Loc.GetString(b.Description), KindName(b.Zone), b.GrantAmount, b.LoanAmount, b.UpfrontPercent, PlanText(b), b.DeadlineSeconds))
            .ToList();

        var applications = new List<ApplicationEntry>();
        foreach (var application in comp.SubsidyApplications)
        {
            if (!_proto.TryIndex<ColonyBusinessPrototype>(application.Business, out var business))
                continue;

            applications.Add(new ApplicationEntry(application.Id, GetNetEntity(application.Person), application.Name, Loc.GetString(business.Name), application.Form,
                SubsidyAmount(business, application.Form), ApplicationBlocked(comp, application, business) ?? string.Empty));
        }

        var active = new List<SubsidyEntry>();
        foreach (var subsidy in comp.Subsidies)
        {
            if (!_proto.TryIndex<ColonyBusinessPrototype>(subsidy.Business, out var business))
                continue;

            var done = subsidy.Stage >= business.Stages.Count;
            var stage = done ? null : business.Stages[subsidy.Stage];
            active.Add(new SubsidyEntry(subsidy.Id, subsidy.Name, Loc.GetString(business.Name), subsidy.Form, subsidy.Total, subsidy.Paid, subsidy.Total - subsidy.Paid,
                stage == null ? Loc.GetString("rumc-subsidy-stage-done")
                    : Loc.GetString("rumc-subsidy-plan-" + stage.Kind.ToString().ToLowerInvariant(), ("target", stage.Target), ("percent", stage.Percent)),
                subsidy.Progress, stage?.Target ?? 0, Math.Max(0, (int) Math.Ceiling((subsidy.Deadline - now).TotalSeconds))));
        }

        return new SubsidyState(businesses, applications, active);
    }

    #endregion
}
