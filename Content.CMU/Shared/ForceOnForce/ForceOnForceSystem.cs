using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Roles;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ForceOnForce;

public sealed partial class ForceOnForceSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedRankSystem _ranks = default!;

    public static string? Opponent(string? faction) => faction?.ToLowerInvariant() switch
    {
        "govfor" => "opfor",
        "opfor" => "govfor",
        _ => null,
    };

    public string? GetFaction(EntityUid user)
    {
        return TryComp<MarineComponent>(user, out var marine) && Opponent(marine.Faction) != null
            ? marine.Faction!.ToLowerInvariant()
            : null;
    }

    public bool CanCommand(EntityUid user, bool includeSquadLeaders = false)
    {
        if (GetFaction(user) == null)
            return false;

        if (includeSquadLeaders && HasComp<SquadLeaderComponent>(user))
            return true;

        if (_ranks.GetRank(user)?.Paygrade is { } paygrade &&
            (paygrade.StartsWith("O", StringComparison.OrdinalIgnoreCase) ||
             paygrade.StartsWith("WO", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (!TryComp<OriginalRoleComponent>(user, out var role) ||
            !_prototypes.TryIndex(role.Job, out var job))
            return false;

        return job.MarineAuthorityLevel > 0 || job.RoundRole is
            "PlatoonCommander" or "ExecutiveOfficer" or "AdjutantDress" or "BrigadierGeneral" or
            "Advisor" or "JuniorOfficer" or "EngineeringOfficer" or "IntelOfficer" or "LogisticsOfficer" or "CMO" or "ChiefMP" ||
            includeSquadLeaders && job.RoundRole is "SquadSergeant" or "SectionSergeant";
    }
}
