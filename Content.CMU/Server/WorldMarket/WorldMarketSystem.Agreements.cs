// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.Trade;
using Content.Shared.CMU14.WorldMarket;

namespace Content.Server.CMU14.WorldMarket;

public sealed partial class WorldMarketSystem
{
    #region The agreements

    // The colony signs an agreement with a company: the treasury pays for it, and for its time the duty is lower, the company pays more, and an embargo does not reach it. A company has one
    // agreement at a time.
    public bool SignAgreement(Entity<WorldMarketComponent> market, string id)
    {
        if (!_proto.TryIndex<TradeAgreementPrototype>(id, out var proto))
            return false;

        if (AgreementOf(market, proto.Company.Id) != null || _budget.GetBudget() < proto.Cost)
            return false;

        _budget.AddToBudget(-proto.Cost);
        market.Comp.Agreements.Add(new SignedAgreement(proto.ID, _timing.CurTime + TimeSpan.FromSeconds(proto.DurationSeconds)));

        // A company that was closed to the colony opens at once when the agreement is signed.
        market.Comp.Embargo.Remove(proto.Company.Id);

        Tell(market, Loc.GetString("rumc-world-agreement-signed", ("name", Loc.GetString(proto.Name)), ("company", Loc.GetString(proto.Company))));
        return true;
    }

    // The colony breaks an agreement: what it cost is not given back.
    public bool BreakAgreement(Entity<WorldMarketComponent> market, string id)
    {
        var signed = market.Comp.Agreements.FirstOrDefault(a => a.Id == id && a.Until > _timing.CurTime);
        if (signed == null || !_proto.TryIndex<TradeAgreementPrototype>(id, out var proto))
            return false;

        market.Comp.Agreements.Remove(signed);
        Tell(market, Loc.GetString("rumc-world-agreement-broken", ("name", Loc.GetString(proto.Name))));
        return true;
    }

    #endregion

    #region The voyages

    // When the next voyage leaves: the schedule, put off by the events. Null when there is no table of the world, and the platforms call a drone at once as they used to.
    public TimeSpan? NextVoyageAt()
    {
        if (!TryMarket(out var market) || !market.Comp.VoyagesScheduled)
            return null;

        return market.Comp.NextVoyage > market.Comp.DelayUntil ? market.Comp.NextVoyage : market.Comp.DelayUntil;
    }

    // The colony pays for an extra voyage: it leaves in half a minute, and the schedule goes on from there.
    public bool CallUrgentVoyage(Entity<WorldMarketComponent> market)
    {
        var now = _timing.CurTime;
        var comp = market.Comp;

        if (now < comp.UrgentReadyAt || _budget.GetBudget() < UrgentCost)
            return false;

        // A voyage that leaves sooner than that anyway is not worth a call.
        var next = Math.Max(comp.NextVoyage.TotalSeconds, comp.DelayUntil.TotalSeconds);
        if (next - now.TotalSeconds <= UrgentLead.TotalSeconds + 15)
            return false;

        _budget.AddToBudget(-UrgentCost);
        comp.NextVoyage = now + UrgentLead;
        comp.DelayUntil = TimeSpan.Zero;
        comp.UrgentReadyAt = now + UrgentCooldown;

        Tell(market, Loc.GetString("rumc-world-urgent-voyage"), announce: false);
        return true;
    }

    private void UpdateVoyage(Entity<WorldMarketComponent> market)
    {
        var now = _timing.CurTime;
        var comp = market.Comp;

        if (now < comp.NextVoyage || now < comp.DelayUntil)
            return;

        comp.NextVoyage = now + VoyageEvery;
        DeliverWares(market);
    }

    #endregion

    #region The quotas

    // A quota of the mother company is issued now: one at a time up to two. The tests call it.
    public bool IssueQuota(Entity<WorldMarketComponent> market, MetropolisQuotaPrototype? only = null)
    {
        var now = _timing.CurTime;
        var comp = market.Comp;

        var candidates = _proto.EnumeratePrototypes<MetropolisQuotaPrototype>()
            .Where(q => comp.Quotas.All(active => active.Id != q.ID))
            .ToList();

        if (only != null)
            candidates = new List<MetropolisQuotaPrototype> { only };

        if (candidates.Count == 0)
            return false;

        var total = candidates.Sum(q => q.Weight);
        var roll = _random.NextFloat() * total;
        var pick = candidates[^1];
        foreach (var candidate in candidates)
        {
            roll -= candidate.Weight;
            if (roll <= 0f)
            {
                pick = candidate;
                break;
            }
        }

        comp.Quotas.Add(new ActiveQuota(pick.ID, now + TimeSpan.FromSeconds(pick.DurationSeconds)));
        Tell(market, Loc.GetString("rumc-world-quota-issued", ("text", Loc.GetString(pick.Text, ("amount", pick.Amount))), ("reward", pick.Reward), ("fine", pick.Fine)));
        return true;
    }

    private void UpdateQuotas(Entity<WorldMarketComponent> market)
    {
        var now = _timing.CurTime;
        var comp = market.Comp;

        if (now >= comp.NextQuota && comp.Quotas.Count < 2)
        {
            comp.NextQuota = now + TimeSpan.FromSeconds(_random.Next(300, 480));
            IssueQuota(market);
        }

        foreach (var quota in comp.Quotas.ToList())
        {
            if (!_proto.TryIndex<MetropolisQuotaPrototype>(quota.Id, out var proto))
            {
                comp.Quotas.Remove(quota);
                continue;
            }

            if (quota.Progress >= proto.Amount)
                FinishQuota(market, quota, proto, done: true);
            else if (now >= quota.Deadline)
                FinishQuota(market, quota, proto, done: false);
        }
    }

    // A quota is over: done, it is paid and the standing goes up; missed, it costs a fine, the standing goes down and an inspection comes.
    public void FinishQuota(Entity<WorldMarketComponent> market, ActiveQuota quota, MetropolisQuotaPrototype proto, bool done)
    {
        var comp = market.Comp;
        comp.Quotas.Remove(quota);
        comp.QuotaLog.Insert(0, new QuotaRecord(proto.ID, done, _timing.CurTime));
        if (comp.QuotaLog.Count > 6)
            comp.QuotaLog.RemoveRange(6, comp.QuotaLog.Count - 6);

        var text = Loc.GetString(proto.Text, ("amount", proto.Amount));
        if (done)
        {
            _budget.AddToBudget(proto.Reward);
            comp.Standing = Math.Min(comp.Standing + 1, 6);
            Tell(market, Loc.GetString("rumc-world-quota-done", ("text", text), ("reward", proto.Reward)));
        }
        else
        {
            _budget.AddToBudget(-proto.Fine);
            comp.Standing = Math.Max(comp.Standing - 1, -4);
            comp.InspectionUntil = _timing.CurTime + TimeSpan.FromMinutes(5);
            Tell(market, Loc.GetString("rumc-world-quota-missed", ("text", text), ("fine", proto.Fine)));
        }
    }

    // What a sale of parcels came to counts for the quotas.
    public void OnSold(TradeLedger ledger)
    {
        if (!TryMarket(out var market))
            return;

        foreach (var quota in market.Comp.Quotas)
        {
            if (_proto.TryIndex<MetropolisQuotaPrototype>(quota.Id, out var proto) && ledger.Legal.TryGetValue(proto.Stack, out var count))
                quota.Progress += count;
        }

        OnSoldIllegal(market, ledger);
    }

    #endregion
}
