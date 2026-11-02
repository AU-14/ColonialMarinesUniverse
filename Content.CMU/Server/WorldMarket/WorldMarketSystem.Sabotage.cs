// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.WorldMarket;
using Robust.Shared.Random;

namespace Content.Server.CMU14.WorldMarket;

// More ways for the ambassadors to hinder one another (all of them need the headquarters of the intelligence, except the complaint to the mother faction). Buying the local leaders over takes a
// part of the target's share of a city for the buyer. An official is bribed to hold a building of the target up for a time. The wells are poisoned where the target is guarded against troubles.
// A harm that the target did in secret is leaked to the whole map. False reports are fed to the scouts of the others, the scouts that are on their way are caught, a spy is put into the
// headquarters of another. A complaint to the mother faction brings an inspection that cuts the next payment to the target. The main goals of the target are spoiled.
public sealed partial class WorldMarketSystem
{
    public const int BuyoutCost = 1100;
    public const int FreezeCost = 600;
    public const int PoisonCost = 1800;
    public const int LeakCost = 500;
    public const int DisinfoCost = 500;
    public const int InterceptCost = 350;
    public const int SpyCost = 400;
    public const int TaskSabotageCost = 1000;
    public const int PayOffCost = 600;

    // The points of influence that a complaint to the mother faction costs.
    public const int ComplainPoints = 2;

    // The favour that a faction must have in a city to buy its leaders over.
    private const int BuyoutLeastInfluence = 10;

    private static readonly TimeSpan FreezeTime = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan DisinfoTime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SpyTime = TimeSpan.FromMinutes(5);

    // How long a harm that was done in secret can still be leaked or seen by a spy.
    private static readonly TimeSpan HarmMemory = TimeSpan.FromMinutes(10);

    #region The record of the harm

    // The harm is written down (the kind is a text), and only the latest are kept.
    private void LogHarm(Entity<WorldMarketComponent> market, string offender, string victim, string cityId, string kind)
    {
        var log = market.Comp.HarmLog;
        log.Add(new HarmRecord { Offender = offender, Victim = victim, City = cityId, Kind = kind, At = _timing.CurTime });

        if (log.Count > 40)
            log.RemoveRange(0, log.Count - 40);
    }

    private HarmRecord? LastHarmOf(Entity<WorldMarketComponent> market, string offender)
    {
        var now = _timing.CurTime;
        return market.Comp.HarmLog.LastOrDefault(r => r.Offender == offender && !r.Leaked && now - r.At <= HarmMemory);
    }

    #endregion

    #region Buying the leaders over

    private string? BuyoutBlocked(Entity<WorldMarketComponent> market, string faction, string cityId, string target, float budget)
    {
        if (HarmBlocked(market, faction, cityId, target, BuyoutCost, budget) is { } blocked)
            return blocked;

        return InfluenceOf(market, faction, cityId) < BuyoutLeastInfluence ? Loc.GetString("rumc-map-need-influence", ("value", BuyoutLeastInfluence)) : null;
    }

    private int? Buyout(Entity<WorldMarketComponent> market, string faction, string cityId, string target, float budget)
    {
        if (!HarmAllowed(market, faction, cityId, target) || BuyoutBlocked(market, faction, cityId, target, budget) != null)
            return null;

        var chance = HarmChance(market, faction, cityId, target, 60);
        BeginHarm(market, faction, target);
        LogHarm(market, faction, target, cityId, "rumc-map-harm-buyout");

        if (_random.Prob(chance / 100f))
        {
            // What the target loses the buyer wins.
            var amount = Math.Min(_random.Next(8, 13), InfluenceOf(market, target, cityId));
            AddCityInfluence(market, target, cityId, -amount);
            AddCityInfluence(market, faction, cityId, amount);

            if (CounterOn(market, target))
                Expose(market, faction, target, false);
        }
        else
        {
            Expose(market, faction, target, true);
        }

        return BuyoutCost;
    }

    #endregion

    #region Bribing an official

    private List<CityProject> FreezeTargets(Entity<WorldMarketComponent> market, string cityId, string target)
    {
        var now = _timing.CurTime;
        return market.Comp.CityProjects
            .Where(p => p.Faction == target && p.City == cityId && p.Rewarded && p.ExpiresAt > now && p.FrozenUntil <= now
                        && _proto.TryIndex<WorldCityActionPrototype>(p.Action, out var action)
                        && action.Feature == WorldBuildingFeature.None && (action.IncomePerMinute > 0f || action.Protects.Count > 0))
            .ToList();
    }

    private string? FreezeBlocked(Entity<WorldMarketComponent> market, string faction, string cityId, string target, float budget)
    {
        if (HarmBlocked(market, faction, cityId, target, FreezeCost, budget) is { } blocked)
            return blocked;

        return FreezeTargets(market, cityId, target).Count == 0 ? Loc.GetString("rumc-map-freeze-nothing") : null;
    }

    private int? Freeze(Entity<WorldMarketComponent> market, string faction, string cityId, string target, float budget)
    {
        if (!HarmAllowed(market, faction, cityId, target) || FreezeBlocked(market, faction, cityId, target, budget) != null)
            return null;

        var chance = HarmChance(market, faction, cityId, target, 75);
        BeginHarm(market, faction, target);
        LogHarm(market, faction, target, cityId, "rumc-map-harm-freeze");

        if (_random.Prob(chance / 100f))
        {
            _random.Pick(FreezeTargets(market, cityId, target)).FrozenUntil = _timing.CurTime + FreezeTime;

            if (CounterOn(market, target))
                Expose(market, faction, target, false);
        }
        else
        {
            Expose(market, faction, target, true);
        }

        return FreezeCost;
    }

    #endregion

    #region Poisoning the wells

    private List<CityProject> PoisonTargets(Entity<WorldMarketComponent> market, string cityId, string target)
    {
        var now = _timing.CurTime;
        return market.Comp.CityProjects
            .Where(p => p.Faction == target && p.City == cityId && p.Rewarded && p.ExpiresAt > now
                        && _proto.TryIndex<WorldCityActionPrototype>(p.Action, out var action) && action.Protects.Count > 0)
            .ToList();
    }

    private string? PoisonBlocked(Entity<WorldMarketComponent> market, string faction, string cityId, string target, float budget)
    {
        if (HarmBlocked(market, faction, cityId, target, PoisonCost, budget) is { } blocked)
            return blocked;

        if (market.Comp.CityCrises.ContainsKey(cityId))
            return Loc.GetString("rumc-map-provoke-already");

        return PoisonTargets(market, cityId, target).Count == 0 ? Loc.GetString("rumc-map-poison-nothing") : null;
    }

    // It always works, goes round the guard of the target, and does not stay secret: the target knows who did it and may answer, and the mother faction thinks the worse of the poisoner.
    private int? Poison(Entity<WorldMarketComponent> market, string faction, string cityId, string target, float budget)
    {
        if (!HarmAllowed(market, faction, cityId, target) || PoisonBlocked(market, faction, cityId, target, budget) != null)
            return null;

        BeginHarm(market, faction, target);
        LogHarm(market, faction, target, cityId, "rumc-map-harm-poison");

        var guard = _random.Pick(PoisonTargets(market, cityId, target));
        var troubles = _proto.TryIndex<WorldCityActionPrototype>(guard.Action, out var action) ? action.Protects : new List<Robust.Shared.Prototypes.ProtoId<WorldCrisisPrototype>>();
        if (troubles.Count > 0 && _proto.TryIndex<WorldCrisisPrototype>(_random.Pick(troubles).Id, out var crisis))
        {
            var now = _timing.CurTime;
            market.Comp.CityCrises[cityId] = new CityCrisis { Id = crisis.ID, StartedAt = now, EndsAt = now + TimeSpan.FromSeconds(crisis.DurationSeconds) };
        }

        Expose(market, faction, target, false);
        AddTrust(market, faction, -5);
        return PoisonCost;
    }

    #endregion

    #region Leaking a secret

    private string? LeakBlocked(Entity<WorldMarketComponent> market, string faction, string offender, float budget)
    {
        if (!HasHq(market, faction))
            return Loc.GetString("rumc-map-hq-needed");

        if (LastHarmOf(market, offender) == null)
            return Loc.GetString("rumc-map-leak-nothing");

        return budget < LeakCost ? Loc.GetString("rumc-map-need-budget") : null;
    }

    // What the faction did in secret last is told to the whole map: it loses the trust of the mother faction, and the one it was done to may answer.
    private int? Leak(Entity<WorldMarketComponent> market, string faction, string offender, float budget)
    {
        if (offender == faction || !market.Comp.KnownFactions.Contains(offender) || LeakBlocked(market, faction, offender, budget) != null)
            return null;

        var record = LastHarmOf(market, offender)!;
        record.Leaked = true;
        AddTrust(market, offender, -12);

        if (record.Victim.Length > 0 && record.Victim != offender)
        {
            market.Comp.Retaliations.RemoveAll(r => r.Victim == record.Victim && r.Offender == offender);
            market.Comp.Retaliations.Add(new Retaliation { Victim = record.Victim, Offender = offender, Until = _timing.CurTime + RetaliationTime });
        }

        return LeakCost;
    }

    #endregion

    #region False reports

    private bool DisinfoOn(Entity<WorldMarketComponent> market, string faction)
    {
        return market.Comp.DisinfoUntil.TryGetValue(faction, out var until) && until > _timing.CurTime;
    }

    private string? DisinfoBlocked(Entity<WorldMarketComponent> market, string faction, float budget)
    {
        if (!HasHq(market, faction))
            return Loc.GetString("rumc-map-hq-needed");

        if (DisinfoOn(market, faction))
            return Loc.GetString("rumc-map-disinfo-on");

        return budget < DisinfoCost ? Loc.GetString("rumc-map-need-budget") : null;
    }

    private int? Disinfo(Entity<WorldMarketComponent> market, string faction, float budget)
    {
        if (DisinfoBlocked(market, faction, budget) != null)
            return null;

        market.Comp.DisinfoUntil[faction] = _timing.CurTime + DisinfoTime;
        return DisinfoCost;
    }

    public int DisinfoSecondsLeft(Entity<WorldMarketComponent> market, string faction)
    {
        return market.Comp.DisinfoUntil.TryGetValue(faction, out var until) ? Math.Max(0, (int) Math.Ceiling((until - _timing.CurTime).TotalSeconds)) : 0;
    }

    // What the scouts of another are told about a faction that feeds them false reports: a made-up favour, and made-up buildings. It changes once a minute, not at every look.
    private (int Influence, string Buildings) FalseReport(string faction, string cityId)
    {
        var minute = (int) (_timing.CurTime.TotalSeconds / 60);
        var hash = Math.Abs(HashCode.Combine(faction, cityId, minute));
        var influence = Math.Clamp(hash % 61, 0, 60);

        var names = _proto.EnumeratePrototypes<WorldCityActionPrototype>().Where(a => !a.Joint).OrderBy(a => a.ID, StringComparer.Ordinal).Select(a => Loc.GetString(a.Name)).ToList();
        var buildings = names.Count == 0 ? string.Empty : string.Join(", ", new[] { names[hash % names.Count], names[(hash / 7 + 3) % names.Count] }.Distinct());
        return (influence, buildings);
    }

    #endregion

    #region Catching the scouts

    private bool ScoutsOnTheWay(Entity<WorldMarketComponent> market, string owner, string cityId)
    {
        return market.Comp.Scouts.TryGetValue(ScoutKey(owner, cityId), out var report) && report.ReadyAt > _timing.CurTime;
    }

    private int InterceptChance(Entity<WorldMarketComponent> market, string target)
    {
        return Math.Clamp(70 - (CounterOn(market, target) ? 25 : 0), 10, 95);
    }

    private string? InterceptBlocked(Entity<WorldMarketComponent> market, string faction, string cityId, string target, float budget)
    {
        if (!HasHq(market, faction))
            return Loc.GetString("rumc-map-hq-needed");

        if (!ScoutsOnTheWay(market, target, cityId))
            return Loc.GetString("rumc-map-intercept-nothing");

        return budget < InterceptCost ? Loc.GetString("rumc-map-need-budget") : null;
    }

    private int? Intercept(Entity<WorldMarketComponent> market, string faction, string cityId, string target, float budget)
    {
        if (target == faction || InterceptBlocked(market, faction, cityId, target, budget) != null)
            return null;

        if (_random.Prob(InterceptChance(market, target) / 100f))
            market.Comp.Scouts.Remove(ScoutKey(target, cityId));

        return InterceptCost;
    }

    #endregion

    #region A spy in a headquarters

    private string? SpyBlocked(Entity<WorldMarketComponent> market, string faction, string target, float budget)
    {
        if (!HasHq(market, faction))
            return Loc.GetString("rumc-map-hq-needed");

        if (!HasHq(market, target))
            return Loc.GetString("rumc-map-spy-no-hq");

        return budget < SpyCost ? Loc.GetString("rumc-map-need-budget") : null;
    }

    private int? Spy(Entity<WorldMarketComponent> market, string faction, string target, float budget)
    {
        if (target == faction || !market.Comp.KnownFactions.Contains(target) || SpyBlocked(market, faction, target, budget) != null)
            return null;

        market.Comp.Spies[faction] = new SpyOrder { Target = target, Until = _timing.CurTime + SpyTime };
        return SpyCost;
    }

    // What the spy has seen: the secret harm that the target has done lately.
    private List<string> SpyLines(Entity<WorldMarketComponent> market, string faction)
    {
        var lines = new List<string>();
        var now = _timing.CurTime;

        if (!market.Comp.Spies.TryGetValue(faction, out var order) || order.Until <= now)
            return lines;

        foreach (var record in market.Comp.HarmLog.Where(r => r.Offender == order.Target && now - r.At <= HarmMemory).OrderByDescending(r => r.At))
        {
            var victim = record.Victim.Length > 0 ? record.Victim : Loc.GetString("rumc-map-spy-everyone");
            var city = record.City.Length > 0 ? CityName(record.City) : "—";
            lines.Add(Loc.GetString("rumc-map-spy-line", ("target", order.Target), ("kind", Loc.GetString(record.Kind)), ("victim", victim), ("city", city), ("seconds", (int) (now - record.At).TotalSeconds)));
        }

        return lines;
    }

    #endregion

    #region A complaint to the mother faction

    private string? ComplainBlocked(Entity<WorldMarketComponent> market, string faction, string target)
    {
        if (market.Comp.Inspected.Contains(target))
            return Loc.GetString("rumc-map-complain-already");

        return PointsOf(faction) < ComplainPoints ? Loc.GetString("rumc-mother-need-points", ("value", ComplainPoints)) : null;
    }

    // The points are paid, and the mother faction inspects the target: its next payment is cut by half.
    private int? Complain(Entity<WorldMarketComponent> market, string faction, string target)
    {
        if (target == faction || !market.Comp.KnownFactions.Contains(target) || ComplainBlocked(market, faction, target) != null || !TrySpendPoints(faction, ComplainPoints))
            return null;

        market.Comp.Inspected.Add(target);
        return 0;
    }

    private string? PayOffBlocked(Entity<WorldMarketComponent> market, string faction, float budget)
    {
        if (!market.Comp.Inspected.Contains(faction))
            return Loc.GetString("rumc-map-payoff-none");

        return budget < PayOffCost ? Loc.GetString("rumc-map-need-budget") : null;
    }

    // The faction pays the inspectors off and the inspection is dropped.
    private int? PayOff(Entity<WorldMarketComponent> market, string faction, float budget)
    {
        if (PayOffBlocked(market, faction, budget) != null)
            return null;

        market.Comp.Inspected.Remove(faction);
        return PayOffCost;
    }

    #endregion

    #region Spoiling the main goals

    private string? TaskSabotageBlocked(Entity<WorldMarketComponent> market, string faction, string target, float budget)
    {
        if (!HasHq(market, faction))
            return Loc.GetString("rumc-map-hq-needed");

        var now = _timing.CurTime;
        if (market.Comp.StrikeReady.TryGetValue(faction, out var ready) && ready > now)
            return Loc.GetString("rumc-map-strike-wait", ("time", (int) Math.Ceiling((ready - now).TotalSeconds)));

        if (!market.Comp.MotherTasks.Any(t => t.Faction == target))
            return Loc.GetString("rumc-map-tasks-nothing");

        return budget < TaskSabotageCost ? Loc.GetString("rumc-map-need-budget") : null;
    }

    private int TaskSabotageChance(Entity<WorldMarketComponent> market, string faction, string target)
    {
        return Math.Clamp(50 - (CounterOn(market, target) ? 25 : 0) + (HasRetaliation(market, faction, target) ? 20 : 0), 10, 95);
    }

    // What the target has done towards its goals is lost (the counters start over), and the time for its reports is cut by half.
    private int? SabotageTasks(Entity<WorldMarketComponent> market, string faction, string target, float budget)
    {
        if (target == faction || !market.Comp.KnownFactions.Contains(target) || TaskSabotageBlocked(market, faction, target, budget) != null)
            return null;

        var chance = TaskSabotageChance(market, faction, target);
        BeginHarm(market, faction, target);
        LogHarm(market, faction, target, string.Empty, "rumc-map-harm-tasks");

        if (_random.Prob(chance / 100f))
        {
            var now = _timing.CurTime;
            var stats = StatsOf(market, target);
            foreach (var task in market.Comp.MotherTasks.Where(t => t.Faction == target))
            {
                task.BaseEarned = stats.Earned;
                task.BaseProjects = stats.Projects;
                task.BaseCrises = stats.Crises;
                task.BaseWorkers = stats.Workers;

                if (task.Deadline != TimeSpan.MaxValue && task.Deadline > now)
                    task.Deadline = now + (task.Deadline - now) / 2;
            }

            if (CounterOn(market, target))
                Expose(market, faction, target, false);
        }
        else
        {
            Expose(market, faction, target, true);
        }

        return TaskSabotageCost;
    }

    #endregion

    #region What the console is told

    // The rest of the secret harm in a place, for a faction that has the headquarters (see FillEspionage).
    private void FillSabotage(Entity<WorldMarketComponent> market, string faction, string cityId, float budget, CityExtras extras)
    {
        foreach (var target in market.Comp.KnownFactions.Where(f => f != faction).OrderBy(f => f, StringComparer.Ordinal))
        {
            var answer = HasRetaliation(market, faction, target);

            if (InfluenceOf(market, target, cityId) > 0)
            {
                extras.Buyouts.Add(new StrikeEntry(target, BuyoutCost, HarmChance(market, faction, cityId, target, 60), answer, BuyoutBlocked(market, faction, cityId, target, budget) ?? string.Empty));
                extras.Freezes.Add(new StrikeEntry(target, FreezeCost, HarmChance(market, faction, cityId, target, 75), answer, FreezeBlocked(market, faction, cityId, target, budget) ?? string.Empty));
                extras.Poisons.Add(new StrikeEntry(target, PoisonCost, 100, answer, PoisonBlocked(market, faction, cityId, target, budget) ?? string.Empty));
            }

            if (ScoutsOnTheWay(market, target, cityId))
                extras.Intercepts.Add(new StrikeEntry(target, InterceptCost, InterceptChance(market, target), false, InterceptBlocked(market, faction, cityId, target, budget) ?? string.Empty));
        }
    }

    // The things of the whole map that the headquarters and the mother faction bring: against each other faction, and the faction's own false reports and inspection.
    private void FillSabotageMap(Entity<WorldMarketComponent> market, string faction, float budget, MapExtras extras)
    {
        foreach (var target in market.Comp.KnownFactions.Where(f => f != faction).OrderBy(f => f, StringComparer.Ordinal))
        {
            // A complaint needs no headquarters.
            extras.Complaints.Add(new StrikeEntry(target, ComplainPoints, 100, false, ComplainBlocked(market, faction, target) ?? string.Empty));

            if (!extras.Hq)
                continue;

            extras.Leaks.Add(new StrikeEntry(target, LeakCost, 100, false, LeakBlocked(market, faction, target, budget) ?? string.Empty));
            extras.Spies.Add(new StrikeEntry(target, SpyCost, 100, false, SpyBlocked(market, faction, target, budget) ?? string.Empty));
            extras.TaskSabotages.Add(new StrikeEntry(target, TaskSabotageCost, TaskSabotageChance(market, faction, target), HasRetaliation(market, faction, target),
                TaskSabotageBlocked(market, faction, target, budget) ?? string.Empty));
        }

        extras.Inspected = market.Comp.Inspected.Contains(faction);
        extras.PayOffCost = PayOffCost;
        extras.PayOffBlocked = PayOffBlocked(market, faction, budget) ?? string.Empty;

        if (!extras.Hq)
            return;

        extras.DisinfoCost = DisinfoCost;
        extras.DisinfoSeconds = DisinfoSecondsLeft(market, faction);
        extras.DisinfoBlocked = DisinfoBlocked(market, faction, budget) ?? string.Empty;
        extras.SpyLines = SpyLines(market, faction);

        if (market.Comp.Spies.TryGetValue(faction, out var order) && order.Until > _timing.CurTime)
            extras.SpySeconds = (int) Math.Ceiling((order.Until - _timing.CurTime).TotalSeconds);
    }

    #endregion

    // Once a second: old records, spies and false reports are dropped.
    private void UpdateSabotage(Entity<WorldMarketComponent> market, TimeSpan now)
    {
        var comp = market.Comp;
        comp.HarmLog.RemoveAll(r => now - r.At > HarmMemory);

        foreach (var faction in comp.Spies.Where(pair => pair.Value.Until <= now).Select(pair => pair.Key).ToList())
            comp.Spies.Remove(faction);

        foreach (var faction in comp.DisinfoUntil.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToList())
            comp.DisinfoUntil.Remove(faction);
    }
}
