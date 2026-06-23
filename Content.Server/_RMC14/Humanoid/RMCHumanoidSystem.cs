using System.Linq;
using Content.Server.AU14.Roles;
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
    [Dependency] private RoundJobProfileSystem _roundJobProfiles = default!;
    [Dependency] private ISerializationManager _serialization = default!;

    private readonly ISawmill _sawmill = Logger.GetSawmill("au14-humanoidsys");

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCJobSpawnerComponent, ComponentInit>(OnAddJobInit);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnAddJobInit(Entity<RMCJobSpawnerComponent> ent, ref ComponentInit args)
    {
        _sawmill.Debug($"[HumanoidSystem] OnAddJobInit called for entity {ent.Owner}");
        if (!_prototype.TryIndex(ent.Comp.Job, out var job))
            return;

        if (TryComp(ent, out GhostRoleComponent? ghostRole))
        {
            ghostRole.RoleName = job.LocalizedName;
            if (job.LocalizedDescription is { } description)
                ghostRole.RoleDescription = description;
        }

        if (ent.Comp.Loadout && job.StartingGear is { } gear)
        {
            var loadout = new LoadoutComponent();
            loadout.StartingGear ??= [];
            loadout.StartingGear.Add(gear);
            AddComp(ent, loadout);
        }

        var addComponents = job.InheritAddComponentSpecials         // prototype Boolean
            ? CollectAddComponentSpecials(job, includeChild: true)  // merged inheritance chain
            : [.. job.Special.OfType<AddComponentSpecial>()];       // original behavior

        ApplyAddComponentSpecials(ent.Owner, addComponents, job.ID);
        _roundJobProfiles.ApplyJobProfile(ent.Owner, job);
    }

    // Runs after DoJobSpecials() to add the mising ancestor specials -> requires InheritAddComponentSpecials: true
    // If add.RemoveExisting: false (unlikely) this will cause duplicates
    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId is not { } jobId) return;
        if (!ev.Mob.IsValid()) return;
        if (HasComp<RMCJobSpawnerComponent>(ev.Mob)) return;
        if (!_prototype.TryIndex<JobPrototype>(jobId, out var job)) return;

        if (job.InheritAddComponentSpecials)
        {
            _sawmill.Debug($"[HumanoidSystem] Player spawned with job {jobId}, applying merged specials.");

            var specials = CollectAddComponentSpecials(job, includeChild: false);
            ApplyAddComponentSpecials(ev.Mob, specials, jobId);
        }

        _roundJobProfiles.ApplyJobProfile(ev.Mob, job);
    }

    // Because abstract prototypes are not instantiated, we have to BFS walk through the raw yml
    private List<AddComponentSpecial> CollectAddComponentSpecials(JobPrototype job, bool includeChild)
    {
        var results = new List<AddComponentSpecial>();
        var visited = new HashSet<string>();
        var queue = new Queue<string>(job.Parents ?? []);
        _sawmill.Debug($"[HumanoidSystem] {job.ID} Starting ancestor chain walk from [{string.Join(", ", job.Parents ?? [])}]");
        while (queue.TryDequeue(out var id))
        {
            if (!visited.Add(id))
            {
                _sawmill.Debug($"    {id} already visited, skipping");
                continue;
            }
            _sawmill.Debug($"    Visiting ancestor: '{id}'");

            if (!_prototype.TryGetMapping(typeof(JobPrototype), id, out var mapping))
            {
                _sawmill.Debug($"      No raw mapping found, skipping");
                continue;
            }

            if (mapping.TryGetValue("special", out var specialNode))
            {
                _sawmill.Debug($"      Found special node");
                var specials = _serialization.Read<JobSpecial[]?>(specialNode);
                if (specials != null)
                {
                    _sawmill.Debug($"      Deserialized {specials.Length} specials");
                    foreach (var s in specials)
                    {
                        if (s is AddComponentSpecial add)
                        {
                            results.Add(add);
                            _sawmill.Debug($"        Added ancestor's AddComponentSpecial");
                        }
                    }
                }
            }

            if (mapping.TryGetValue("parent", out var parentNode))
            {
                if (parentNode is ValueDataNode single)
                {
                    _sawmill.Debug($"    Parent is '{single.Value}', enqueueing");
                    queue.Enqueue(single.Value);
                }
                else
                    _sawmill.Warning($"Ancestor {id} has multiple parents which isn't supported (use 1 parent per proto)");
            }
        }

        if (includeChild && JobDefinedSpecial(job))
        {
            _sawmill.Debug($"{job.ID} Including child's own specials: {job.Special.Length} entries");
            foreach (var s in job.Special)
            {
                if (s is AddComponentSpecial add)
                {
                    results.Add(add);
                    _sawmill.Debug($"      Added child's AddComponentSpecial");
                }
            }
        }

        _sawmill.Debug($"[HumanoidSystem] '{job.ID}' Total AddComponentSpecials collected: {results.Count}");
        return results;
    }

    private void ApplyAddComponentSpecials(EntityUid target, List<AddComponentSpecial> specials, string jobId)
    {
        foreach (var add in specials)
        {
            if (!add.RemoveExisting)
                _sawmill.Warning($"[HumanoidSystem] Job '{jobId}': AddComponentSpecial with RemoveExisting=false on entity {target}; may cause issues.");
            EntityManager.AddComponents(target, add.Components, add.RemoveExisting);
        }
    }

    private bool JobDefinedSpecial(JobPrototype job)
        => _prototype.TryGetMapping(typeof(JobPrototype), job.ID, out var mappings)
        && mappings.TryGetValue("special", out _);
}
