using Content.Server.CMU14.Squads.Foxtrot;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.CMU14.Squads.Foxtrot;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Station.Systems;

// CMU14: the opt-in Foxtrot squad of GOVFOR/OPFOR.
public sealed partial class StationSpawningSystem
{
    [Dependency] private CMUFoxtrotSquadSystem _cmuFoxtrotSquad = default!;

    /// <summary>
    /// The player whose character is being spawned by <see cref="SpawnPlayerCharacterOnStation"/>, if any.
    /// </summary>
    private ICommonSession? _cmuSpawningPlayer;

    private bool CMUTryPickFoxtrotSquad(
        EntityUid? station,
        string? stationJobId,
        HumanoidCharacterProfile? profile,
        out string squad)
    {
        squad = string.Empty;
        if (string.IsNullOrEmpty(stationJobId) ||
            !_cmuFoxtrotSquad.TryPickFoxtrotSquad(station,
                new ProtoId<JobPrototype>(stationJobId),
                _cmuSpawningPlayer,
                profile,
                out var foxtrot))
        {
            return false;
        }

        squad = foxtrot;
        return true;
    }

    private void CMUApplyFoxtrotSquadPerks(EntityUid member, Entity<SquadTeamComponent> squad, JobPrototype? job)
    {
        if (HasComp<CMUFoxtrotSquadComponent>(squad))
            _cmuFoxtrotSquad.ApplyPerks(member, squad, job);
    }
}
