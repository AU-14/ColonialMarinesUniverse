using Content.Server.Ghost.Roles.Components;
using Content.Server.AU14.Roles;
using Content.Server.Jobs;
using Content.Shared.Clothing.Components;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Humanoid;

public sealed partial class RMCHumanoidSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private RoundJobProfileSystem _roundJobProfiles = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCJobSpawnerComponent, ComponentInit>(OnAddJobInit);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnAddJobInit(Entity<RMCJobSpawnerComponent> ent, ref ComponentInit args)
    {
        if (!_prototype.TryIndex(ent.Comp.Job, out var job))
            return;

        if (TryComp(ent, out GhostRoleComponent? ghostRole))
        {
            ghostRole.RoleName = job.LocalizedName;

            if (job.LocalizedDescription is { } description)
                ghostRole.RoleDescription = description;
        }

        if (ent.Comp.Loadout &&
            job.StartingGear is { } gear)
        {
            var loadout = new LoadoutComponent();
            loadout.StartingGear ??= [];
            loadout.StartingGear.Add(gear);
            AddComp(ent, loadout);
        }

        foreach (var special in job.Special)
        {
            if (special is AddComponentSpecial add)
                EntityManager.AddComponents(ent, add.Components, add.RemoveExisting);
        }

        _roundJobProfiles.ApplyJobProfile(ent.Owner, job);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId is not { } jobId ||
            !ev.Mob.IsValid() ||
            !_prototype.TryIndex<JobPrototype>(jobId, out var job))
        {
            return;
        }

        _roundJobProfiles.ApplyJobProfile(ev.Mob, job);
    }
}
