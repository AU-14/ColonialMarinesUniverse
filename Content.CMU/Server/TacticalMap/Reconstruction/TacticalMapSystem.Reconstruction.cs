using System.Linq;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Overwatch;
using Robust.Shared.Audio;

namespace Content.Server._RMC14.TacticalMap;

public sealed partial class TacticalMapSystem
{
    private readonly Dictionary<(EntityUid Scope, string Faction), TimeSpan> _reconstructionAnnouncements = new();
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

    public EntityUid? ReconstructionViewerSquad(EntityUid source) =>
        TryComp<TacticalMapUserComponent>(source, out var user) && user.HasSquad &&
        _squad.TryGetMemberSquad(source, out var squad) ? squad.Owner : null;

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

    public void SetReconstructionCanvas(EntityUid source, EntityUid actor, EntityUid scope, string faction, List<TacticalMapLine> lines,
        Dictionary<Vector2i, string> labels)
    {
        var announce = BeginReconstructionAnnouncement(source, scope, faction, out var sound);
        if (TryComp<SquadTeamComponent>(scope, out var squad))
        {
            squad.TacMapLines = lines; squad.TacMapLabels = labels;
            foreach (var member in squad.Members)
            {
                if (!TryComp<TacticalMapUserComponent>(member, out var user)) continue;
                user.SquadLines = lines; user.SquadLabels = labels; Dirty(member, user);
            }
            if (announce)
                _marineAnnounce.AnnounceOverwatchSquad(actor, "The squad tactical map has been updated.", scope, squad.Color, Name(scope));
            return;
        }
        if (!TryComp<TacticalMapComponent>(scope, out var map))
        {
            if (announce) AnnounceReconstructionUpdate(actor, faction, sound);
            return;
        }
        // Use the classic publication path: faction canvas, last-published contacts, alerts and audit event.
        // Limit it to the selected map so ship/planet drawings cannot overwrite one another.
        UpdateCanvas(lines, labels, faction == MarinesFaction, faction == XenosFaction, faction == OpforFaction,
            faction == GovforFaction, faction == ClfFaction, actor, sound, scope, announce, faction == WeYuFaction);
        var users = EntityQueryEnumerator<ActiveTacticalMapUserComponent, TacticalMapUserComponent>();
        while (users.MoveNext(out var uid, out _, out var user))
            if (user.Map == scope) UpdateUserData((uid, user), map);
    }

    private bool BeginReconstructionAnnouncement(EntityUid source, EntityUid scope, string faction, out SoundSpecifier? sound)
    {
        sound = null;
        TryComp<TacticalMapUserComponent>(source, out var user);
        TryComp<TacticalMapComputerComponent>(source, out var computer);
        if (user != null) sound = user.Sound;
        var time = _timing.CurTime;
        if (time < _reconstructionAnnouncements.GetValueOrDefault((scope, faction)) ||
            user != null && time < user.NextAnnounceAt || computer != null && time < computer.NextAnnounceAt)
            return false;
        _reconstructionAnnouncements[(scope, faction)] = time + _announceCooldown;
        foreach (var key in _reconstructionAnnouncements.Keys.Where(k => TerminatingOrDeleted(k.Scope)).ToArray())
            _reconstructionAnnouncements.Remove(key);
        if (user != null)
        {
            user.LastAnnounceAt = time;
            user.NextAnnounceAt = time + _announceCooldown;
            Dirty(source, user);
        }
        if (computer != null)
        {
            computer.LastAnnounceAt = time;
            computer.NextAnnounceAt = time + _announceCooldown;
            Dirty(source, computer);
        }
        return true;
    }

    private void AnnounceReconstructionUpdate(EntityUid actor, string faction, SoundSpecifier? sound)
    {
        if (faction == XenosFaction)
            _xenoAnnounce.AnnounceSameHive(actor, "There's a shift in the hivemind's tactical picture. The mental map sharpens.", sound);
        else
            AnnounceHumanTacticalMapUpdated(actor, sound, faction);
    }

    /// <summary>Use exactly the computer's normal faction, sensor and infrastructure filtering.</summary>
    public void RefreshReconstructionContacts(Entity<TacticalMapComputerComponent> computer)
    {
        if (TryGetTacticalMap(out var map)) UpdateMapData(computer, map);
    }
}
