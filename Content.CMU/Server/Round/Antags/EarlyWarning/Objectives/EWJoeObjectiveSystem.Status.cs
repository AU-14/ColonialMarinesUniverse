using System.Linq;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// Playtest 3: feeds the Joe's own level and objective panel (<see cref="EWJoeStatusComponent"/>).
/// Rebuilt after every evaluation and level change; dirtied only when something the panel shows
/// actually changed, so a steady ladder costs no network traffic.
/// </summary>
public sealed partial class EWJoeObjectiveSystem
{
    public void UpdateStatus(Entity<EWJoeComponent> ent)
    {
        if (TerminatingOrDeleted(ent) || !_proto.TryIndex(ent.Comp.LevelTable, out var table) || table.Levels.Count == 0)
            return;

        var level = Math.Clamp(ent.Comp.Level, 0, table.Levels.Count - 1);
        var def = table.Levels[level];

        var entries = new List<EWJoeObjectiveEntry>();
        if (_mind.TryGetMind(ent, out var mindId, out var mind))
        {
            foreach (var objective in ent.Comp.CurrentObjectives)
            {
                if (!Exists(objective) || _objectives.GetInfo(objective, mindId, mind) is not { } info)
                    continue;

                var completed = ent.Comp.CompletedCredit.ContainsKey(objective);
                entries.Add(new EWJoeObjectiveEntry(info.Title, info.Description,
                    completed ? 1f : info.Progress, completed));
            }
        }

        var required = ent.Comp.CurrentObjectives.Count == 0 ? 0 : RequiredCredit(ent.Comp);
        var earned = ent.Comp.CompletedCredit.Values.Sum();
        var final = def.ObjectiveGroup == null && def.ForcedObjective == null;
        var doubled = IsPreThreatWindow(ent.Comp);
        var thralls = ent.Comp.Thralls.Where(t => Exists(t) && !_mobState.IsDead(t)).Select(t => Name(t)).ToList();

        var status = EnsureComp<EWJoeStatusComponent>(ent);
        if (status.Level == level
            && status.LevelName == def.Name.Id
            && status.FinalLevel == final
            && status.CreditEarned == earned
            && status.CreditRequired == required
            && status.RequireAssimilation == def.RequireAssimilation
            && status.DoubleCredit == doubled
            && status.RespawnReady == ent.Comp.RespawnReady
            && status.Thralls.SequenceEqual(thralls)
            && status.Objectives.SequenceEqual(entries))
        {
            return;
        }

        status.Level = level;
        status.LevelName = def.Name.Id;
        status.FinalLevel = final;
        status.CreditEarned = earned;
        status.CreditRequired = required;
        status.RequireAssimilation = def.RequireAssimilation;
        status.DoubleCredit = doubled;
        status.RespawnReady = ent.Comp.RespawnReady;
        status.Thralls = thralls;
        status.Objectives = entries;
        Dirty(ent, status);
    }
}
