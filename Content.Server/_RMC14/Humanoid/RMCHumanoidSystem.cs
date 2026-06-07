using System.Linq;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Jobs;
using Content.Shared.Clothing.Components;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Humanoid;

public sealed partial class RMCHumanoidSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCJobSpawnerComponent, ComponentInit>(OnAddJobInit);
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

        var addComponents = job.InheritAddComponentSpecials     // prototype Boolean
            ? GetAllAddComponentSpecials(job)                   // merged inheritance chain
            : [.. job.Special.OfType<AddComponentSpecial>()];   // original behavior

        foreach (var add in addComponents)
            EntityManager.AddComponents(ent, add.Components, add.RemoveExisting);
    }

    private List<AddComponentSpecial> GetAllAddComponentSpecials(JobPrototype job)
    {
        var results = new List<AddComponentSpecial>();

        if (job.Parents is { Length: > 0 })
        {
            foreach (var parentId in job.Parents)
            {
                if (_prototype.TryIndex<JobPrototype>(parentId, out var parentJob))
                    results.AddRange(GetAllAddComponentSpecials(parentJob));
            }
        }

        foreach (var special in job.Special)
        {
            if (special is AddComponentSpecial addComp)
                results.Add(addComp);
        }

        return results;
    }
}
