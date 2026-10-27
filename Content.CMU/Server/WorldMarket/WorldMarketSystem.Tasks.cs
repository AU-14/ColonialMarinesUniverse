// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.WorldMarket;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.WorldMarket;

// What the mother faction asks of an ambassador and what it gives. From time to time it asks for a report (how many cities the faction holds, what the work in them has brought in), and now and
// then sets a mission (hold cities, earn, end troubles, call workers); a report must be sent while it is ready, a mission is done by itself. What is done gives points of influence, money,
// sometimes a crate of rare things, and the trust of the mother faction; what is not done lowers the trust. The trust makes its services dearer or cheaper, and the money that it sends
// the ambassadors now and then to work with bigger or smaller.
public sealed partial class WorldMarketSystem
{
    public const int DefaultTrust = 50;

    // A report is asked for once in half an hour (the first one too). Of the missions each faction always has a few, until it has done them all.
    private static readonly TimeSpan ReportEvery = TimeSpan.FromMinutes(30);
    private const int MissionSlots = 3;
    private static readonly TimeSpan FirstStipend = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StipendEvery = TimeSpan.FromMinutes(8);

    // The money that the mother faction sends: a base, and more for every point of the favour that the faction has in the cities (all the cities together); the trust of the mother faction
    // makes the whole of it smaller or bigger.
    public const int StipendBase = 120;
    public const int StipendPerInfluence = 1;

    // The money that the mother faction pays for a main goal is cut the same way as the other incomes of the ambassadors.
    public const float RewardMoneyFactor = 0.4f;

    private static int RewardMoneyOf(WorldMotherTaskPrototype proto) => (int) Math.Round(proto.RewardMoney * RewardMoneyFactor);

    public int TrustOf(string faction)
    {
        return TryMarket(out var market) ? market.Comp.Trust.GetValueOrDefault(faction, DefaultTrust) : DefaultTrust;
    }

    private void AddTrust(Entity<WorldMarketComponent> market, string faction, int amount)
    {
        market.Comp.Trust[faction] = Math.Clamp(market.Comp.Trust.GetValueOrDefault(faction, DefaultTrust) + amount, 0, 100);
    }

    // How the prices of the services of the mother faction change with its trust: lower from seventy, higher under thirty.
    public static float PriceFactor(int trust) => trust >= 70 ? 0.8f : trust < 30 ? 1.5f : 1f;

    public int OfferCost(string faction, WorldMotherOfferPrototype offer)
    {
        var factor = PriceFactor(TrustOf(faction));

        // A company that is borrowed from an ally costs twice.
        if (PmcAccess(faction, offer) == PmcAccessKind.Ally)
            factor *= 2f;

        // A fighting company costs more than it did, and twice as much again for every time the faction has ordered it already. (The medics, the engineers, the workers and the rest do not.)
        if (offer.Group == MotherOfferGroup.Pmc)
            factor *= CombatCostFactor * MathF.Pow(2f, Math.Min(BoughtCount(faction, offer), MaxDoublings));

        return Math.Max(1, (int) Math.Round(offer.Cost * factor, MidpointRounding.AwayFromZero));
    }

    // How much dearer the fighting companies are than they were, and how many times the price of one may double (so that it does not run away to the sky).
    public const float CombatCostFactor = 2f;
    public const int MaxDoublings = 8;

    // How many times a faction has ordered an offer.
    public int BoughtCount(string faction, WorldMotherOfferPrototype offer)
    {
        return TryMarket(out var market) ? market.Comp.OffersBought.GetValueOrDefault(faction + "|" + offer.ID) : 0;
    }

    private FactionStats StatsOf(Entity<WorldMarketComponent> market, string faction)
    {
        if (!market.Comp.FactionStats.TryGetValue(faction, out var stats))
            market.Comp.FactionStats[faction] = stats = new FactionStats();

        return stats;
    }

    // An order of the mother faction was done: workers are counted for the missions.
    public void NoteMotherOrder(string faction, WorldMotherOfferPrototype offer)
    {
        if (!TryMarket(out var market))
            return;

        if (offer.Group == MotherOfferGroup.Workers)
            StatsOf(market, faction).Workers++;

        // The next order of the same fighting company costs twice as much.
        if (offer.Group == MotherOfferGroup.Pmc)
        {
            var key = faction + "|" + offer.ID;
            market.Comp.OffersBought[key] = market.Comp.OffersBought.GetValueOrDefault(key) + 1;
        }
    }

    private int ControlledCities(Entity<WorldMarketComponent> market, string faction, int level)
    {
        return ActiveCities(market).Count(city => InfluenceOf(market, faction, city.ID) >= level);
    }

    // The money that the mother faction sends the faction the next time.
    public int StipendAmount(string faction)
    {
        if (!TryMarket(out var market))
            return 0;

        var influence = ActiveCities(market).Sum(city => InfluenceOf(market, faction, city.ID));
        return (int) Math.Round((StipendBase + StipendPerInfluence * influence) * (0.6f + TrustOf(faction) / 125f));
    }

    public int StipendSeconds()
    {
        return TryMarket(out var market) ? Math.Max(0, (int) Math.Ceiling((market.Comp.NextStipend - _timing.CurTime).TotalSeconds)) : 0;
    }

    // Crates that are to be put down at the console of the faction (what the missions have given): taken, so they are given once.
    public List<string> ClaimCrates(string faction)
    {
        return TryMarket(out var market) && market.Comp.PendingCrates.Remove(faction, out var crates) ? crates : new List<string>();
    }

    // How much of a task is done, in lines (one for each requirement, marked done or not), and whether all of it is.
    private (bool Done, List<string> Lines) TaskProgress(Entity<WorldMarketComponent> market, MotherTask task, WorldMotherTaskPrototype proto)
    {
        var stats = StatsOf(market, task.Faction);
        var lines = new List<string>();
        var done = true;

        void Require(bool met, string text)
        {
            lines.Add((met ? "✓ " : "· ") + text);
            done &= met;
        }

        if (proto.Cities > 0)
        {
            var have = ControlledCities(market, task.Faction, proto.CityInfluence);
            Require(have >= proto.Cities, Loc.GetString("rumc-mother-req-cities", ("have", have), ("need", proto.Cities), ("level", proto.CityInfluence)));
        }

        if (proto.Earn > 0)
        {
            var have = (int) (stats.Earned - task.BaseEarned);
            Require(have >= proto.Earn, Loc.GetString("rumc-mother-req-earn", ("have", have), ("need", proto.Earn)));
        }

        if (proto.Projects > 0)
        {
            var have = stats.Projects - task.BaseProjects;
            Require(have >= proto.Projects, Loc.GetString("rumc-mother-req-projects", ("have", have), ("need", proto.Projects)));
        }

        if (proto.Crises > 0)
        {
            var have = stats.Crises - task.BaseCrises;
            Require(have >= proto.Crises, Loc.GetString("rumc-mother-req-crises", ("have", have), ("need", proto.Crises)));
        }

        if (proto.Workers > 0)
        {
            var have = stats.Workers - task.BaseWorkers;
            Require(have >= proto.Workers, Loc.GetString("rumc-mother-req-workers", ("have", have), ("need", proto.Workers)));
        }

        return (done, lines);
    }

    private void FinishTask(Entity<WorldMarketComponent> market, MotherTask task, WorldMotherTaskPrototype proto, bool success)
    {
        if (!success)
        {
            AddTrust(market, task.Faction, -proto.TrustLoss);
            return;
        }

        AddTrust(market, task.Faction, proto.TrustGain);

        if (proto.RewardPoints > 0)
            AddPoints(market, task.Faction, proto.RewardPoints);

        if (!proto.Report)
            market.Comp.CompletedMissions.Add(task.Faction + "|" + proto.ID);

        if (proto.RewardMoney > 0)
            PayFaction(market, task.Faction, RewardMoneyOf(proto));

        if (proto.RewardCrate is { } crate)
        {
            if (!market.Comp.PendingCrates.TryGetValue(task.Faction, out var crates))
                market.Comp.PendingCrates[task.Faction] = crates = new List<string>();

            crates.Add(crate);
        }

    }

    // The ambassador sends the report that was asked for, if it is ready. Gives whether it was sent.
    public bool SubmitReport(string faction)
    {
        if (!TryMarket(out var market))
            return false;

        for (var i = 0; i < market.Comp.MotherTasks.Count; i++)
        {
            var task = market.Comp.MotherTasks[i];
            if (task.Faction != faction || !_proto.TryIndex<WorldMotherTaskPrototype>(task.Task, out var proto) || !proto.Report)
                continue;

            if (!TaskProgress(market, task, proto).Done)
                return false;

            FinishTask(market, task, proto, true);
            market.Comp.MotherTasks.RemoveAt(i);
            return true;
        }

        return false;
    }

    // The tasks of a faction as its console shows them.
    public List<MotherTaskEntry> BuildTasks(string faction)
    {
        var entries = new List<MotherTaskEntry>();
        if (!TryMarket(out var market))
            return entries;

        var now = _timing.CurTime;
        foreach (var task in market.Comp.MotherTasks.Where(t => t.Faction == faction))
        {
            if (!_proto.TryIndex<WorldMotherTaskPrototype>(task.Task, out var proto))
                continue;

            var (done, lines) = TaskProgress(market, task, proto);

            var reward = new List<string>();
            if (proto.RewardPoints > 0)
                reward.Add(Loc.GetString("rumc-mother-reward-points", ("points", proto.RewardPoints)));

            if (proto.RewardMoney > 0)
                reward.Add($"${RewardMoneyOf(proto):N0}");

            if (proto.RewardCrate != null)
                reward.Add(Loc.GetString("rumc-mother-reward-crate"));

            reward.Add(proto.Report
                ? Loc.GetString("rumc-mother-reward-trust", ("gain", proto.TrustGain), ("loss", proto.TrustLoss))
                : Loc.GetString("rumc-mother-reward-trust-open", ("gain", proto.TrustGain)));

            // A mission has no time (minus one); the trust that it gives is told without the loss.
            entries.Add(new MotherTaskEntry(proto.Report, Loc.GetString(proto.Name), Loc.GetString(proto.Description),
                proto.Report ? Math.Max(0, (int) Math.Ceiling((task.Deadline - now).TotalSeconds)) : -1, lines, proto.Report && done, string.Join(" · ", reward)));
        }

        // The report first, then the main goals.
        return entries.OrderByDescending(e => e.Report).ToList();
    }

    // Money that is paid to a faction: it comes to the budget of its console with the money of the standing things.
    private void PayFaction(Entity<WorldMarketComponent> market, string faction, float amount)
    {
        market.Comp.FactionIncome[faction] = market.Comp.FactionIncome.GetValueOrDefault(faction) + amount;
    }

    // Once a second: a report that was not sent in time is lost, a main goal that is done is paid, a report is asked for once in half an hour, money is sent.
    private void UpdateMother(Entity<WorldMarketComponent> market, TimeSpan now)
    {
        var comp = market.Comp;

        if (comp.NextMotherReport == TimeSpan.Zero)
        {
            comp.NextMotherReport = now + ReportEvery;
            comp.NextStipend = now + FirstStipend;
        }

        for (var i = comp.MotherTasks.Count - 1; i >= 0; i--)
        {
            var task = comp.MotherTasks[i];
            if (!_proto.TryIndex<WorldMotherTaskPrototype>(task.Task, out var proto))
            {
                comp.MotherTasks.RemoveAt(i);
                continue;
            }

            // A main goal is done as soon as it is done and has no time; a report waits to be sent, and is lost when its time is out.
            if (!proto.Report && TaskProgress(market, task, proto).Done)
            {
                FinishTask(market, task, proto, true);
                comp.MotherTasks.RemoveAt(i);
            }
            else if (proto.Report && now >= task.Deadline)
            {
                FinishTask(market, task, proto, false);
                comp.MotherTasks.RemoveAt(i);
            }
        }

        if (now >= comp.NextMotherReport)
        {
            comp.NextMotherReport = now + ReportEvery;
            IssueReports(market, now);
        }

        IssueMissions(market, now);

        if (now >= comp.NextStipend)
        {
            comp.NextStipend = now + StipendEvery;
            foreach (var faction in comp.KnownFactions)
            {
                var amount = StipendAmount(faction);

                // The mother faction has been told of the faction and inspects it: this payment is cut by half.
                if (comp.Inspected.Remove(faction))
                    amount /= 2;

                PayFaction(market, faction, amount);
            }
        }
    }

    private MotherTask NewTask(Entity<WorldMarketComponent> market, string faction, WorldMotherTaskPrototype proto, TimeSpan deadline)
    {
        var stats = StatsOf(market, faction);
        return new MotherTask
        {
            Faction = faction,
            Task = proto.ID,
            Deadline = deadline,
            BaseEarned = stats.Earned,
            BaseProjects = stats.Projects,
            BaseCrises = stats.Crises,
            BaseWorkers = stats.Workers,
        };
    }

    // Each faction that has no report is asked for one, at random.
    private void IssueReports(Entity<WorldMarketComponent> market, TimeSpan now)
    {
        var pool = _proto.EnumeratePrototypes<WorldMotherTaskPrototype>().Where(t => t.Report).ToList();
        if (pool.Count == 0)
            return;

        foreach (var faction in market.Comp.KnownFactions)
        {
            if (market.Comp.MotherTasks.Any(t => t.Faction == faction && _proto.TryIndex<WorldMotherTaskPrototype>(t.Task, out var other) && other.Report))
                continue;

            var proto = _random.Pick(pool);
            market.Comp.MotherTasks.Add(NewTask(market, faction, proto, now + TimeSpan.FromSeconds(proto.DurationSeconds)));
        }
    }

    // Each faction is always given a few main goals, and they are not the same for all of them: a goal that another faction has now is not set again, nor one that this faction has done (unless
    // there is nothing else).
    private void IssueMissions(Entity<WorldMarketComponent> market, TimeSpan now)
    {
        var comp = market.Comp;
        if (comp.KnownFactions.Count == 0)
            return;

        List<WorldMotherTaskPrototype>? pool = null;

        foreach (var faction in comp.KnownFactions)
        {
            var have = comp.MotherTasks.Count(t => t.Faction == faction && _proto.TryIndex<WorldMotherTaskPrototype>(t.Task, out var other) && !other.Report);
            if (have >= MissionSlots)
                continue;

            pool ??= _proto.EnumeratePrototypes<WorldMotherTaskPrototype>().Where(t => !t.Report).ToList();

            while (have < MissionSlots)
            {
                var free = pool.Where(t => !comp.CompletedMissions.Contains(faction + "|" + t.ID) && !comp.MotherTasks.Any(m => m.Faction == faction && m.Task == t.ID)).ToList();
                var different = free.Where(t => !comp.MotherTasks.Any(m => m.Task == t.ID)).ToList();
                if (different.Count > 0)
                    free = different;

                if (free.Count == 0)
                    break;

                var proto = _random.Pick(free);
                comp.MotherTasks.Add(NewTask(market, faction, proto, TimeSpan.MaxValue));
                have++;
            }
        }
    }
}
