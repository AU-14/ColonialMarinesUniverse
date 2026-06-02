using System.Linq;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Jobs;
using Content.Shared.Clothing.Components;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server._RMC14.Humanoid;

// NOTE: Nuke the Debug comments when ghostroles/playerspawns/latejoins all work
// Yes it hurts to read, unknown edge cases may come up and I don't want to write it again
public sealed partial class RMCHumanoidSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    private ISawmill Sawmill => Logger.GetSawmill("au14-humanoidsys");

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCJobSpawnerComponent, ComponentInit>(OnAddJobInit);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnAddJobInit(Entity<RMCJobSpawnerComponent> ent, ref ComponentInit args)
    {
        Sawmill.Debug($"[HumanoidSystem] OnAddJobInit called for entity {ent.Owner}");
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

        var addComponents = job.InheritAddComponentSpecials         // prototype Boolean
            ? GetAllAddComponentSpecials(job, includeChild: true)   // merged inheritance chain
            : [.. job.Special.OfType<AddComponentSpecial>()];       // original behavior

        foreach (var add in addComponents)
            EntityManager.AddComponents(ent, add.Components, add.RemoveExisting);
    }

    // Runs after DoJobSpecials() to add the mising ancestor specials -> requires InheritAddComponentSpecials: true
    // If add.RemoveExisting: false (unlikely) this will cause duplicates
    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null) return;
        if (!_prototype.TryIndex<JobPrototype>(ev.JobId, out var job)) return;
        if (!job.InheritAddComponentSpecials) return;
        Sawmill.Debug($"[HumanoidSystem] Player spawned with job {ev.JobId}, applying merged specials.");
        var addComponents = GetAllAddComponentSpecials(job);
        foreach (var add in addComponents)
        {
            if (!add.RemoveExisting)
                Sawmill.Warning($"[HumanoidSystem] AddComponentSpecial with RemoveExisting=false on job {ev.JobId}, which may cause issues.");
            EntityManager.AddComponents(ev.Mob, add.Components, add.RemoveExisting);
        }
    }

    // Because abstract prototypes are not instantiated, we have to BFS walk through the raw yml
    private List<AddComponentSpecial> GetAllAddComponentSpecials(JobPrototype job, bool includeChild = false)
    {
        var results = new List<AddComponentSpecial>();
        var visited = new HashSet<string>();
        var queue = new Queue<string>(job.Parents ?? []);
        Sawmill.Debug($"[HumanoidSystem] {job.ID} Starting ancestor chain walk from [{string.Join(", ", job.Parents ?? [])}]");
        while (queue.TryDequeue(out var id))
        {
            if (!visited.Add(id))
            {
                Sawmill.Debug($"    {id} already visited, skipping");
                continue;
            }
            Sawmill.Debug($"    Visiting ancestor: '{id}'");

            if (!_prototype.TryGetMapping(typeof(JobPrototype), id, out var mapping))
            {
                Sawmill.Debug($"      No raw mapping found, skipping");
                continue;
            }

            if (mapping.TryGetValue("special", out var specialNode))
            {
                Sawmill.Debug($"      Found special node");
                var specials = _serialization.Read<JobSpecial[]?>(specialNode);
                if (specials != null)
                {
                    Sawmill.Debug($"      Deserialized {specials.Length} specials");
                    foreach (var s in specials)
                    {
                        if (s is AddComponentSpecial add)
                        {
                            results.Add(add);
                            Sawmill.Debug($"        Added ancestor's AddComponentSpecial");
                        }
                    }
                }
            }

            if (mapping.TryGetValue("parent", out var parentNode))
            {
                if (parentNode is ValueDataNode single)
                {
                    Sawmill.Debug($"    Parent is '{single.Value}', enqueueing");
                    queue.Enqueue(single.Value);
                }
                else
                    Sawmill.Warning($"Ancestor {id} has multiple parents which isn't supported (use 1 parent per proto)");
            }
        }

        if (includeChild && JobDefinedSpecial(job))
        {
            Sawmill.Debug($"{job.ID} Including child's own specials: {job.Special.Length} entries");
            foreach (var s in job.Special)
            {
                if (s is AddComponentSpecial add)
                {
                    results.Add(add);
                    Sawmill.Debug($"      Added child's AddComponentSpecial");
                }
            }
        }

        Sawmill.Debug($"[HumanoidSystem] '{job.ID}' Total AddComponentSpecials collected: {results.Count}");
        return results;
    }

    private bool JobDefinedSpecial(JobPrototype job)
        => _prototype.TryGetMapping(typeof(JobPrototype), job.ID, out var mappings)
        && mappings.TryGetValue("special", out _);
}
