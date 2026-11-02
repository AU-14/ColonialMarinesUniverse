// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.Government;

namespace Content.Server.CMU14.Government;

// The quota of the metropolis (see ColonyQuota.cs). A plan in money is set for a time; the mayor pays it into the plan out of the budget; when the sum is paid the metropolis gives a grant that is greater than
// it, and when the time is out unpaid, what was paid goes back, the budget is fined, and every faction thinks the worse of the city. The next plan comes after a pause. Nothing is announced: the plan, what is
// paid, the time, and what came of the last plans are in the window of the mayor.
public sealed partial class ColonyGovernmentSystem
{
    public const int MaxQuotaLog = 8;

    private ColonyQuotaPrototype? QuotaProto()
    {
        return _proto.EnumeratePrototypes<ColonyQuotaPrototype>().OrderBy(p => p.ID).FirstOrDefault();
    }

    // The sum of the plan that is set now: the first sum, a little greater after each plan that was kept (to a limit), to the nearest fifty.
    private static int QuotaAmountFor(ColonyQuotaPrototype proto, int level)
    {
        var steps = Math.Min(level, proto.MaxGrowths);
        var amount = proto.Amount * Math.Pow(1 + proto.GrowthPercent / 100.0, steps);
        return Math.Max(50, (int) Math.Round(amount / 50.0) * 50);
    }

    private void UpdateQuota(ColonyGovernmentComponent comp, TimeSpan now)
    {
        if (QuotaProto() is not { } proto)
            return;

        // The first plan is set after a pause, so that the town has time to fill the budget.
        if (!comp.QuotaStarted)
        {
            comp.QuotaStarted = true;
            comp.QuotaNext = now + TimeSpan.FromSeconds(proto.PauseSeconds);
        }

        if (!comp.QuotaActive)
        {
            if (now < comp.QuotaNext)
                return;

            comp.QuotaActive = true;
            comp.QuotaAmount = QuotaAmountFor(proto, comp.QuotaLevel);
            comp.QuotaPaid = 0;
            comp.QuotaDeadline = now + TimeSpan.FromSeconds(proto.DurationSeconds);
            return;
        }

        if (now < comp.QuotaDeadline)
            return;

        // The time is out and the plan is not kept: what was paid goes back, the budget is fined, the factions think the worse of the city.
        var paid = comp.QuotaPaid;
        var fine = Math.Min((int) _budget.GetBudget(), comp.QuotaAmount * proto.FinePercent / 100);
        _budget.AddToBudget(paid);
        if (fine > 0)
            _budget.AddToBudget(-fine);

        foreach (var faction in _market.KnownFactions())
            ShiftAttitude(comp, faction, proto.FailureAttitude);

        AddQuotaLog(comp, Loc.GetString("rumc-quota-log-failure", ("amount", comp.QuotaAmount), ("paid", paid), ("fine", fine)));
        comp.QuotaLevel = 0;
        comp.QuotaActive = false;
        comp.QuotaPaid = 0;
        comp.QuotaNext = now + TimeSpan.FromSeconds(proto.PauseSeconds);
    }

    private static void AddQuotaLog(ColonyGovernmentComponent comp, string line)
    {
        comp.QuotaLog.Insert(0, line);
        if (comp.QuotaLog.Count > MaxQuotaLog)
            comp.QuotaLog.RemoveRange(MaxQuotaLog, comp.QuotaLog.Count - MaxQuotaLog);
    }

    // The mayor pays a part into the plan out of the budget. When the sum is whole the metropolis gives the grant.
    private void OnPayQuota(Entity<ColonyGovernmentConsoleComponent> console, ref PayQuotaMessage msg)
    {
        var comp = EnsureGovernment().Comp;
        if (QuotaProto() is not { } proto || !comp.QuotaActive)
            return;

        var pay = Math.Min(msg.Amount, Math.Min(comp.QuotaAmount - comp.QuotaPaid, (int) _budget.GetBudget()));
        if (pay <= 0)
            return;

        _budget.AddToBudget(-pay);
        comp.QuotaPaid += pay;

        if (comp.QuotaPaid >= comp.QuotaAmount)
        {
            // The plan is kept: the sum goes to the metropolis, and the grant is greater.
            var grant = comp.QuotaAmount * proto.GrantPercent / 100;
            _budget.AddToBudget(grant);

            foreach (var faction in _market.KnownFactions())
                ShiftAttitude(comp, faction, proto.SuccessAttitude);

            AddQuotaLog(comp, Loc.GetString("rumc-quota-log-success", ("amount", comp.QuotaAmount), ("grant", grant)));
            comp.QuotaLevel++;
            comp.QuotaActive = false;
            comp.QuotaPaid = 0;
            comp.QuotaNext = _timing.CurTime + TimeSpan.FromSeconds(proto.PauseSeconds);
        }

        Refresh();
    }

    private QuotaState BuildQuota(ColonyGovernmentComponent comp)
    {
        if (QuotaProto() is not { } proto)
            return new QuotaState(false, false, string.Empty, string.Empty, 0, 0, 0, 0, 0, 0, 0, new List<string>());

        var now = _timing.CurTime;
        var amount = comp.QuotaActive ? comp.QuotaAmount : QuotaAmountFor(proto, comp.QuotaLevel);
        return new QuotaState(true, comp.QuotaActive, Loc.GetString(proto.Name), Loc.GetString(proto.Description), amount, comp.QuotaPaid,
            comp.QuotaActive ? Math.Max(0, (int) Math.Ceiling((comp.QuotaDeadline - now).TotalSeconds)) : 0,
            comp.QuotaActive ? 0 : Math.Max(0, (int) Math.Ceiling((comp.QuotaNext - now).TotalSeconds)),
            amount * proto.GrantPercent / 100, amount * proto.FinePercent / 100, comp.QuotaLevel, comp.QuotaLog.ToList());
    }
}
