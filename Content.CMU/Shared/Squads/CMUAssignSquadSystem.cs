using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.GameTicking;

namespace Content.Shared.CMU14.Squads;

public sealed class CMUAssignSquadSystem : EntitySystem
{
    [Dependency] private SquadSystem _squad = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUAssignSquadComponent, PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(Entity<CMUAssignSquadComponent> ent, ref PlayerSpawnCompleteEvent args)
    {
        if (!_squad.TryEnsureSquad(ent.Comp.Squad, out var squad))
        {
            Log.Error($"{ToPrettyString(ent)} could not join squad {ent.Comp.Squad}, it is not a squad prototype");
            return;
        }

        _squad.AssignSquad(ent, (squad.Owner, squad.Comp), args.JobId);
    }
}
