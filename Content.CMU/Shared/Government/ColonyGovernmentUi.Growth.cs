// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Government;

// What the window of the mayor shows of the growth, the studies, the projects with the outside, and the grey business, and what it sends.

[Serializable, NetSerializable]
public sealed class ProjectEntry(string id, ProjectKind kind, string name, string description, int cost, int upkeep, int duration, string effects, string requirement)
{
    public readonly string Id = id;
    public readonly ProjectKind Kind = kind;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly int Cost = cost;
    public readonly int Upkeep = upkeep;
    public readonly int Duration = duration;
    public readonly string Effects = effects;
    public readonly string Requirement = requirement;
}

[Serializable, NetSerializable]
public sealed class ActiveProjectEntry(int id, string name, string target, int leftSeconds, bool permanent)
{
    public readonly int Id = id;
    public readonly string Name = name;
    public readonly string Target = target;
    public readonly int LeftSeconds = leftSeconds;
    public readonly bool Permanent = permanent;
}

[Serializable, NetSerializable]
public sealed class CovertEntry(string id, string name, string description, int cost, int chance, CovertTarget target, string effects, string blocked)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly int Cost = cost;

    // The chance in per cent as it is now, with the heat counted.
    public readonly int Chance = chance;
    public readonly CovertTarget Target = target;
    public readonly string Effects = effects;
    public readonly string Blocked = blocked;
}

[Serializable, NetSerializable]
public sealed class GrowthState(
    List<ProjectEntry> projects, List<ActiveProjectEntry> active,
    List<CovertEntry> covert, int heat, List<string> log, int maxProjects)
{
    public readonly List<ProjectEntry> Projects = projects;
    public readonly List<ActiveProjectEntry> Active = active;
    public readonly List<CovertEntry> Covert = covert;
    public readonly int Heat = heat;
    public readonly List<string> Log = log;
    public readonly int MaxProjects = maxProjects;
}

// ---------------------------------------------------------------- From the window of the mayor

[Serializable, NetSerializable]
public sealed class StartProjectMessage(string project, string target) : BoundUserInterfaceMessage
{
    public readonly string Project = project;
    public readonly string Target = target;
}

[Serializable, NetSerializable]
public sealed class CancelProjectMessage(int project) : BoundUserInterfaceMessage
{
    public readonly int Project = project;
}

[Serializable, NetSerializable]
public sealed class RunCovertMessage(string affair, string target) : BoundUserInterfaceMessage
{
    public readonly string Affair = affair;
    public readonly string Target = target;
}
