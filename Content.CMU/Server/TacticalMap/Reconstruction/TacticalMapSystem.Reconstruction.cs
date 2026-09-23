using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Overwatch;

namespace Content.Server._RMC14.TacticalMap;

public sealed partial class TacticalMapSystem
{
    public void ResolveReconstructionFaction(Entity<TacticalMapComputerComponent> computer, EntityUid actor)
    {
        if (NormalizeMapFaction(computer.Comp.Faction) == null && _skills.HasSkill(actor, computer.Comp.Skill, computer.Comp.SkillLevel))
            ResolveComputerWriteFaction(computer, actor);
    }

    private static bool ValidReconstructionCanvas(TacticalMapUpdateCanvasMsg message)
    {
        if (message.Lines == null || message.Labels == null || message.Labels.Count > 256) return false;
        var pointCount = 0;
        foreach (var line in message.Lines)
        {
            if (!float.IsFinite(line.Thickness) || line.Thickness < 1 || line.Thickness > 8) return false;
            if (line.WorldPoints is not { } points) continue;
            pointCount += points.Length;
            if (points.Length is < 1 or > 512 || pointCount > 16384 || line.Depth is < -128 or > 127) return false;
            foreach (var point in points)
                if (!float.IsFinite(point.X) || !float.IsFinite(point.Y)) return false;
        }
        foreach (var text in message.Labels.Values)
            if (text == null || text.Length > 120) return false;
        return true;
    }

    public EntityUid ReconstructionCanvasScope(EntityUid source, EntityUid map) =>
        HasComp<TacticalMapComponent>(map) && TryComp<OverwatchConsoleComponent>(source, out var console) && console.Squad is { } squad &&
        HasComp<SquadTeamComponent>(GetEntity(squad)) ? GetEntity(squad) : map;

    public bool TryReconstructionCanvas(EntityUid scope, string faction,
        out List<TacticalMapLine> lines, out Dictionary<Vector2i, string> labels)
    {
        if (TryComp<SquadTeamComponent>(scope, out var squad))
        {
            lines = squad.TacMapLines; labels = squad.TacMapLabels;
            return true;
        }
        if (TryComp<TacticalMapComponent>(scope, out var map))
        {
            (lines, labels) = faction switch
            {
                XenosFaction => (map.XenoLines, map.XenoLabels),
                GovforFaction => (map.GovforLines, map.GovforLabels),
                OpforFaction => (map.OpforLines, map.OpforLabels),
                ClfFaction => (map.ClfLines, map.ClfLabels),
                WeYuFaction => (map.WeYuLines, map.WeYuLabels),
                _ => (map.MarineLines, map.MarineLabels),
            };
            return true;
        }
        lines = default!; labels = default!;
        return false;
    }

    public void SetReconstructionCanvas(EntityUid scope, string faction, List<TacticalMapLine> lines,
        Dictionary<Vector2i, string> labels)
    {
        if (TryComp<SquadTeamComponent>(scope, out var squad))
        {
            squad.TacMapLines = lines; squad.TacMapLabels = labels;
            foreach (var member in squad.Members)
            {
                if (!TryComp<TacticalMapUserComponent>(member, out var user)) continue;
                user.SquadLines = lines; user.SquadLabels = labels; Dirty(member, user);
            }
            return;
        }
        if (!TryComp<TacticalMapComponent>(scope, out var map)) return;
        switch (faction)
        {
            case XenosFaction: map.XenoLines = lines; map.XenoLabels = labels; break;
            case GovforFaction: map.GovforLines = lines; map.GovforLabels = labels; break;
            case OpforFaction: map.OpforLines = lines; map.OpforLabels = labels; break;
            case ClfFaction: map.ClfLines = lines; map.ClfLabels = labels; break;
            case WeYuFaction: map.WeYuLines = lines; map.WeYuLabels = labels; break;
            default: map.MarineLines = lines; map.MarineLabels = labels; break;
        }
        map.MapDirty = true;
        // Reuse the normal recipients and filtering. A Send makes both views current immediately.
        var computers = EntityQueryEnumerator<TacticalMapComputerComponent>();
        while (computers.MoveNext(out var uid, out var computer))
            if (computer.Map == scope && _ui.IsUiOpen(uid, TacticalMapComputerUi.Key)) UpdateMapData((uid, computer), map);
        var users = EntityQueryEnumerator<ActiveTacticalMapUserComponent, TacticalMapUserComponent>();
        while (users.MoveNext(out var uid, out _, out var user))
            if (user.Map == scope) UpdateUserData((uid, user), map);
    }

    /// <summary>Use exactly the computer's normal faction, sensor and infrastructure filtering.</summary>
    public void RefreshReconstructionContacts(Entity<TacticalMapComputerComponent> computer)
    {
        if (TryGetTacticalMap(out var map)) UpdateMapData(computer, map);
    }
}
