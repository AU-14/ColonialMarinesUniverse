using System.Linq;
using Content.Server.CMU14.Round;
using Content.Shared.CMU14.Round.Roles;
using Content.Shared.CMU14.Lobby;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Lobby;

/// <summary>Resolves event command selectors against actual job metadata and active platoons.</summary>
public sealed partial class CMUEventJobTargetsSystem : EntitySystem
{
    [Dependency] private PlatoonSpawnRuleSystem _platoons = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public bool TryResolveSide(string selector, out RoundJobSide side)
    {
        if (Enum.TryParse(selector, true, out side) && Enum.IsDefined(side) && side != RoundJobSide.None)
            return true;

        var govfor = string.Equals(_platoons.SelectedGovforPlatoon?.ID, selector, StringComparison.OrdinalIgnoreCase);
        var opfor = string.Equals(_platoons.SelectedOpforPlatoon?.ID, selector, StringComparison.OrdinalIgnoreCase);
        side = govfor && !opfor ? RoundJobSide.Govfor : opfor && !govfor ? RoundJobSide.Opfor : RoundJobSide.None;
        return side != RoundJobSide.None;
    }

    public List<JobPrototype> GetJobs(RoundJobSide side, string role)
    {
        var department = _prototypes.EnumeratePrototypes<DepartmentPrototype>()
            .FirstOrDefault(p => p.ID.Equals(role, StringComparison.OrdinalIgnoreCase));
        return _prototypes.EnumeratePrototypes<JobPrototype>()
            .Where(job => job.RoundSide == side &&
                (role.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                 job.ID.Equals(role, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(job.RoundRole, role, StringComparison.OrdinalIgnoreCase) ||
                 department?.Roles.Contains(job.ID) == true))
            .OrderBy(job => job.ID)
            .ToList();
    }

    public IEnumerable<string> SideOptions() => Enum.GetValues<RoundJobSide>()
        .Where(side => side != RoundJobSide.None).Select(side => side.ToString().ToLowerInvariant())
        .Concat(new[] { _platoons.SelectedGovforPlatoon?.ID, _platoons.SelectedOpforPlatoon?.ID }
            .OfType<string>()).Distinct();

    public List<CMUEventSideInfo> GetSides() => Enum.GetValues<RoundJobSide>()
        .Where(side => side != RoundJobSide.None)
        .Select(side =>
        {
            var id = side.ToString().ToLowerInvariant();
            var name = Loc.GetString($"cmu-event-side-{id}");
            var platoon = side == RoundJobSide.Govfor ? _platoons.SelectedGovforPlatoon :
                side == RoundJobSide.Opfor ? _platoons.SelectedOpforPlatoon : null;
            return new CMUEventSideInfo(id, platoon == null ? name : $"{name} — {Loc.GetString(platoon.Name)}");
        }).ToList();

    public IEnumerable<string> RoleOptions(string side)
    {
        if (!TryResolveSide(side, out var resolved))
            return [];

        var jobs = GetJobs(resolved, "all");
        var ids = jobs.Select(job => new ProtoId<JobPrototype>(job.ID)).ToHashSet();
        var departments = _prototypes.EnumeratePrototypes<DepartmentPrototype>()
            .Where(department => department.Roles.Any(ids.Contains)).Select(department => department.ID);
        return jobs.SelectMany(job => new[] { job.RoundRole, job.ID })
            .OfType<string>().Concat(departments).Distinct().OrderBy(role => role);
    }
}
