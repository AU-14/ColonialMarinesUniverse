// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.Government;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Government;

// The projects with the outside and the grey business. A project is an embassy with a faction (permanent, kept for a sum every minute, it earns the ambassador points of influence), or a mission or a caravan way
// with a city of the map (for a time, they bring money). The price is paid once. The grey affairs (the spies, the bribes, the pressure) cost much and may fail: the chance falls as the heat of the city rises,
// every affair raises it, and when it is too high it breaks out into a scandal that all the factions hear of. What comes of an affair is told to the mayor alone.
public sealed partial class ColonyGovernmentSystem
{
    public const int MaxProjects = 3;
    public const int ScandalHeat = 70;
    public const int CovertCooldownSeconds = 180;
    public const int MaxCovertLog = 12;

    #region Projects

    private IEnumerable<ColonyProjectPrototype> ActiveProjects(ColonyGovernmentComponent comp)
    {
        foreach (var project in comp.Projects)
        {
            if (_proto.TryIndex<ColonyProjectPrototype>(project.Proto, out var proto))
                yield return proto;
        }
    }

    // A target is a faction for an embassy and a city for the others; its name for the people.
    private string TargetName(ColonyProjectPrototype proto, string target)
    {
        return proto.Kind == ProjectKind.Embassy ? target : _market.Cities().FirstOrDefault(c => c.Id == target)?.Name ?? target;
    }

    private string? ProjectBlocked(ColonyGovernmentComponent comp, ColonyProjectPrototype proto, string target)
    {
        if (comp.Projects.Count >= MaxProjects)
            return Loc.GetString("rumc-project-blocked-many", ("max", MaxProjects));

        if (comp.Projects.Any(p => p.Proto == proto.ID && p.Target == target))
            return Loc.GetString("rumc-project-blocked-active");

        if (_budget.GetBudget() < proto.Cost)
            return Loc.GetString("rumc-project-blocked-money");

        if (proto.Kind == ProjectKind.Embassy)
        {
            if (!_market.KnownFactions().Contains(target))
                return Loc.GetString("rumc-project-blocked-target");

            if (AttitudeOf(comp, target) < proto.MinRelation)
                return Loc.GetString("rumc-project-blocked-relation", ("value", proto.MinRelation));

            return null;
        }

        var city = _market.Cities().FirstOrDefault(c => c.Id == target);
        if (city == null)
            return Loc.GetString("rumc-project-blocked-target");

        if (RelationOf(comp, target) < proto.MinRelation)
            return Loc.GetString("rumc-project-blocked-relation", ("value", proto.MinRelation));

        if (proto.Requires is { } needed && !city.Capabilities.Contains(needed))
            return Loc.GetString("rumc-government-accord-city-lacks");

        return null;
    }

    private void OnStartProject(Entity<ColonyGovernmentConsoleComponent> console, ref StartProjectMessage msg)
    {
        var comp = EnsureGovernment().Comp;
        var target = msg.Target;
        if (!_proto.TryIndex<ColonyProjectPrototype>(msg.Project, out var proto) || ProjectBlocked(comp, proto, target) != null)
            return;

        _budget.AddToBudget(-proto.Cost);
        TimeSpan? until = proto.DurationSeconds > 0 ? _timing.CurTime + TimeSpan.FromSeconds(proto.DurationSeconds) : null;
        comp.Projects.Add(new ActiveProject(comp.NextProjectId++, proto.ID, target, until));

        if (proto.Kind == ProjectKind.Embassy)
            ShiftAttitude(comp, target, proto.RelationShift);
        else
            ShiftRelation(comp, target, proto.RelationShift);

        Refresh();
    }

    // A project is closed early: nothing is given back, and the other side takes it ill.
    private void OnCancelProject(Entity<ColonyGovernmentConsoleComponent> console, ref CancelProjectMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var comp = government.Comp;
        var projectId = msg.Project;
        if (comp.Projects.FirstOrDefault(p => p.Id == projectId) is not { } project || !_proto.TryIndex<ColonyProjectPrototype>(project.Proto, out var proto))
            return;

        comp.Projects.Remove(project);
        if (proto.Kind == ProjectKind.Embassy)
            ShiftAttitude(comp, project.Target, -5);
        else
            ShiftRelation(comp, project.Target, -3);

        Refresh();
    }

    private void UpdateProjects(ColonyGovernmentComponent comp, TimeSpan now)
    {
        foreach (var project in comp.Projects.Where(p => p.Until != null && p.Until <= now).ToList())
        {
            comp.Projects.Remove(project);
        }
    }

    // The keeping of what stands, for a minute: an embassy that cannot be paid for is closed, and the ambassador of the faction earns his points.
    private void ProjectMinute(ColonyGovernmentComponent comp)
    {
        foreach (var project in comp.Projects.ToList())
        {
            if (!_proto.TryIndex<ColonyProjectPrototype>(project.Proto, out var proto))
                continue;

            if (proto.UpkeepPerMinute > 0)
            {
                if (_budget.GetBudget() < proto.UpkeepPerMinute)
                {
                    comp.Projects.Remove(project);
                    continue;
                }

                _budget.AddToBudget(-proto.UpkeepPerMinute);
            }

            if (proto.Kind == ProjectKind.Embassy && proto.PointsPerMinute > 0f)
                comp.PointsOwed[project.Target] = comp.PointsOwed.GetValueOrDefault(project.Target) + proto.PointsPerMinute;
        }
    }

    private string ProjectEffects(ColonyProjectPrototype proto)
    {
        var parts = new List<string>();
        if (proto.IncomePerMinute != 0f)
            parts.Add(Loc.GetString("rumc-government-fx-income", ("value", (int) MathF.Round(proto.IncomePerMinute))));

        if (proto.DutyCut != 0)
            parts.Add(Loc.GetString("rumc-government-fx-duty", ("value", proto.DutyCut)));

        if (proto.CallDiscountPercent != 0)
            parts.Add(Loc.GetString("rumc-government-fx-call", ("value", proto.CallDiscountPercent)));

        if (proto.PointsPerMinute > 0f)
            parts.Add(Loc.GetString("rumc-government-fx-points", ("value", proto.PointsPerMinute.ToString("0.#"))));

        return string.Join(", ", parts);
    }

    private List<ProjectEntry> ProjectEntries()
    {
        return _proto.EnumeratePrototypes<ColonyProjectPrototype>()
            .OrderBy(p => p.Kind).ThenBy(p => p.Cost)
            .Select(p => new ProjectEntry(p.ID, p.Kind, Loc.GetString(p.Name), Loc.GetString(p.Description), p.Cost, p.UpkeepPerMinute, p.DurationSeconds, ProjectEffects(p),
                p.Requires is { } needed ? Loc.GetString("rumc-government-cap-" + needed.ToString().ToLowerInvariant()) : string.Empty))
            .ToList();
    }

    private List<ActiveProjectEntry> ActiveProjectEntries(ColonyGovernmentComponent comp, TimeSpan now)
    {
        var entries = new List<ActiveProjectEntry>();
        foreach (var project in comp.Projects)
        {
            if (!_proto.TryIndex<ColonyProjectPrototype>(project.Proto, out var proto))
                continue;

            entries.Add(new ActiveProjectEntry(project.Id, Loc.GetString(proto.Name), TargetName(proto, project.Target),
                project.Until is { } until ? Math.Max(0, (int) Math.Ceiling((until - now).TotalSeconds)) : 0, project.Until == null));
        }

        return entries;
    }

    #endregion

    #region Grey business

    private float CovertChance(ColonyGovernmentComponent comp, ColonyCovertPrototype proto)
    {
        return Math.Clamp(proto.SuccessChance - comp.Heat / 200f, 0.1f, 0.95f);
    }

    private string? CovertBlocked(ColonyGovernmentComponent comp, ColonyCovertPrototype proto, string target, TimeSpan now)
    {
        if (comp.CovertReady.TryGetValue(proto.ID, out var ready) && ready > now)
            return Loc.GetString("rumc-covert-blocked-cooldown", ("seconds", (int) Math.Ceiling((ready - now).TotalSeconds)));

        if (_budget.GetBudget() < proto.Cost)
            return Loc.GetString("rumc-covert-blocked-money");

        if (proto.Target == CovertTarget.Faction && !_market.KnownFactions().Contains(target))
            return Loc.GetString("rumc-project-blocked-target");

        if (proto.Target == CovertTarget.City && _market.Cities().All(c => c.Id != target))
            return Loc.GetString("rumc-project-blocked-target");

        return null;
    }

    private void OnRunCovert(Entity<ColonyGovernmentConsoleComponent> console, ref RunCovertMessage msg)
    {
        var comp = EnsureGovernment().Comp;
        var now = _timing.CurTime;
        var target = msg.Target;
        if (!_proto.TryIndex<ColonyCovertPrototype>(msg.Affair, out var proto) || CovertBlocked(comp, proto, target, now) != null)
            return;

        _budget.AddToBudget(-proto.Cost);
        comp.CovertReady[proto.ID] = now + TimeSpan.FromSeconds(CovertCooldownSeconds);

        var targetName = proto.Target == CovertTarget.City ? _market.Cities().FirstOrDefault(c => c.Id == target)?.Name ?? target : target;
        var success = _random.Prob(CovertChance(comp, proto));
        var outcome = new List<string>();

        if (success)
        {
            if (proto.SuccessReward > 0)
            {
                _budget.AddToBudget(proto.SuccessReward);
                outcome.Add(Loc.GetString("rumc-covert-out-reward", ("sum", proto.SuccessReward)));
            }

            if (proto.Target == CovertTarget.Faction)
            {
                if (proto.SuccessAttitude != 0)
                {
                    ShiftAttitude(comp, target, proto.SuccessAttitude);
                    outcome.Add(Loc.GetString("rumc-covert-out-attitude", ("value", Signed(proto.SuccessAttitude))));
                }

                if (proto.SuccessPoints > 0)
                {
                    _market.GrantPoints(target, proto.SuccessPoints);
                    outcome.Add(Loc.GetString("rumc-covert-out-points", ("value", proto.SuccessPoints)));
                }
            }
            else if (proto.Target == CovertTarget.City && proto.SuccessRelation != 0)
            {
                ShiftRelation(comp, target, proto.SuccessRelation);
                outcome.Add(Loc.GetString("rumc-covert-out-relation", ("value", Signed(proto.SuccessRelation))));
            }
        }
        else
        {
            if (proto.FailureFine > 0)
            {
                _budget.AddToBudget(-Math.Min(proto.FailureFine, (int) _budget.GetBudget()));
                outcome.Add(Loc.GetString("rumc-covert-out-fine", ("sum", proto.FailureFine)));
            }

            if (proto.Target == CovertTarget.Faction && proto.FailureAttitude != 0)
            {
                ShiftAttitude(comp, target, proto.FailureAttitude);
                outcome.Add(Loc.GetString("rumc-covert-out-attitude", ("value", Signed(proto.FailureAttitude))));
            }
            else if (proto.Target == CovertTarget.City && proto.FailureRelation != 0)
            {
                ShiftRelation(comp, target, proto.FailureRelation);
                outcome.Add(Loc.GetString("rumc-covert-out-relation", ("value", Signed(proto.FailureRelation))));
            }
        }

        comp.Heat = Math.Min(100f, comp.Heat + proto.Heat);
        comp.CovertLog.Insert(0, Loc.GetString(success ? "rumc-covert-log-success" : "rumc-covert-log-failure",
            ("name", Loc.GetString(proto.Name)), ("target", proto.Target == CovertTarget.None ? "—" : targetName), ("outcome", outcome.Count == 0 ? "—" : string.Join(", ", outcome))));
        if (comp.CovertLog.Count > MaxCovertLog)
            comp.CovertLog.RemoveRange(MaxCovertLog, comp.CovertLog.Count - MaxCovertLog);

        // When the city is too well known for this, it breaks out: every faction hears, and thinks the worse of the city.
        if (comp.Heat >= ScandalHeat)
        {
            comp.Heat = 40f;
            foreach (var faction in _market.KnownFactions())
                ShiftAttitude(comp, faction, -5);
        }

        Refresh();
    }

    private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();

    private void CovertMinute(ColonyGovernmentComponent comp)
    {
        comp.Heat = Math.Max(0f, comp.Heat - 0.5f);
    }

    private string CovertEffects(ColonyCovertPrototype proto)
    {
        var parts = new List<string>();
        if (proto.SuccessReward > 0)
            parts.Add(Loc.GetString("rumc-covert-out-reward", ("sum", proto.SuccessReward)));

        if (proto.SuccessAttitude != 0)
            parts.Add(Loc.GetString("rumc-covert-out-attitude", ("value", Signed(proto.SuccessAttitude))));

        if (proto.SuccessRelation != 0)
            parts.Add(Loc.GetString("rumc-covert-out-relation", ("value", Signed(proto.SuccessRelation))));

        if (proto.SuccessPoints > 0)
            parts.Add(Loc.GetString("rumc-covert-out-points", ("value", proto.SuccessPoints)));

        var failure = new List<string>();
        if (proto.FailureFine > 0)
            failure.Add(Loc.GetString("rumc-covert-out-fine", ("sum", proto.FailureFine)));

        if (proto.FailureAttitude != 0)
            failure.Add(Loc.GetString("rumc-covert-out-attitude", ("value", Signed(proto.FailureAttitude))));

        if (proto.FailureRelation != 0)
            failure.Add(Loc.GetString("rumc-covert-out-relation", ("value", Signed(proto.FailureRelation))));

        return Loc.GetString("rumc-covert-effects", ("success", parts.Count == 0 ? "—" : string.Join(", ", parts)), ("failure", failure.Count == 0 ? "—" : string.Join(", ", failure)),
            ("heat", proto.Heat));
    }

    private List<CovertEntry> CovertEntries(ColonyGovernmentComponent comp, TimeSpan now)
    {
        return _proto.EnumeratePrototypes<ColonyCovertPrototype>()
            .OrderBy(p => p.Cost)
            .Select(p => new CovertEntry(p.ID, Loc.GetString(p.Name), Loc.GetString(p.Description), p.Cost, (int) MathF.Round(CovertChance(comp, p) * 100f), p.Target, CovertEffects(p),
                comp.CovertReady.TryGetValue(p.ID, out var ready) && ready > now ? Loc.GetString("rumc-covert-blocked-cooldown", ("seconds", (int) Math.Ceiling((ready - now).TotalSeconds))) : string.Empty))
            .ToList();
    }

    #endregion

    #region The state

    private GrowthState BuildGrowth(ColonyGovernmentComponent comp)
    {
        var now = _timing.CurTime;
        return new GrowthState(ProjectEntries(), ActiveProjectEntries(comp, now), CovertEntries(comp, now),
            (int) MathF.Round(comp.Heat), comp.CovertLog.ToList(), MaxProjects);
    }

    #endregion
}
