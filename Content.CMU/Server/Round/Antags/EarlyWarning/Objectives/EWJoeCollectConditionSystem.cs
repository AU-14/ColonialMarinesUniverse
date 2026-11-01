using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Objectives.Components;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// Rolls, titles and scores <see cref="EWJoeCollectConditionComponent"/> objectives.
/// </summary>
public sealed partial class EWJoeCollectConditionSystem : EntitySystem
{
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    private EntityQuery<ContainerManagerComponent> _containerQuery;
    private EntityQuery<StackComponent> _stackQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _containerQuery = GetEntityQuery<ContainerManagerComponent>();
        _stackQuery = GetEntityQuery<StackComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<EWJoeCollectConditionComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<EWJoeCollectConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<EWJoeCollectConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnAssigned(Entity<EWJoeCollectConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        var min = Math.Max(1, ent.Comp.Min);
        var max = Math.Max(min, ent.Comp.Max);

        if (ent.Comp.VerifyExistence)
        {
            if (args.Mind.OwnedEntity is not { } owner)
            {
                args.Cancelled = true;
                return;
            }

            var mapId = Transform(owner).MapID;
            if (mapId == MapId.Nullspace)
            {
                args.Cancelled = true;
                return;
            }

            // the planet is a z-level network of maps, not one map
            var maps = _zLevels.GetAllNetworkMapIds(mapId);
            var available = CountAvailable(maps, ent.Comp.Whitelist, max);
            if (available < min)
            {
                args.Cancelled = true;
                return;
            }

            max = Math.Min(max, available);
        }

        ent.Comp.Target = _random.Next(min, max + 1);
    }

    private void OnAfterAssign(Entity<EWJoeCollectConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        _metaData.SetEntityName(ent, Loc.GetString(ent.Comp.Title, ("count", ent.Comp.Target)), args.Meta);
        _metaData.SetEntityDescription(ent, Loc.GetString(ent.Comp.Description, ("count", ent.Comp.Target)), args.Meta);
    }

    private void OnGetProgress(Entity<EWJoeCollectConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (ent.Comp.Target <= 0)
        {
            args.Progress = 1f;
            return;
        }

        var held = args.Mind.OwnedEntity is { } owner ? CountHeld(owner, ent.Comp.Whitelist) : 0;
        args.Progress = Math.Clamp(held / (float) ent.Comp.Target, 0f, 1f);
    }

    /// <summary>Counts matching entities in the owner's container tree: hands, inventory slots, bags, implants.</summary>
    public int CountHeld(EntityUid owner, EntityWhitelist whitelist)
    {
        if (!_containerQuery.TryGetComponent(owner, out var manager))
            return 0;

        var count = 0;
        var stack = new Stack<ContainerManagerComponent>();
        stack.Push(manager);

        while (stack.TryPop(out var current))
        {
            foreach (var container in current.Containers.Values)
            {
                foreach (var contained in container.ContainedEntities)
                {
                    if (_whitelist.IsWhitelistPass(whitelist, contained))
                        count += _stackQuery.TryGetComponent(contained, out var stackComp) ? stackComp.Count : 1;

                    if (_containerQuery.TryGetComponent(contained, out var inner))
                        stack.Push(inner);
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Counts whitelisted entities on the given maps, stopping once <paramref name="enough"/> are found.
    /// Walks only the component and tag queries the whitelist names, never every entity.
    /// </summary>
    public int CountAvailable(HashSet<MapId> maps, EntityWhitelist whitelist, int enough)
    {
        var count = 0;
        var seen = new HashSet<EntityUid>();

        if (whitelist.Components != null)
        {
            foreach (var name in whitelist.Components)
            {
                if (!_factory.TryGetRegistration(name, out var registration))
                {
                    Log.Error($"Collect objective whitelist names unknown component {name}");
                    continue;
                }

                foreach (var (uid, _) in EntityManager.GetAllComponents(registration.Type))
                {
                    Consider(uid, maps, whitelist, seen, ref count);
                    if (count >= enough)
                        return count;
                }
            }
        }

        if (whitelist.Tags != null)
        {
            var query = EntityQueryEnumerator<TagComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                Consider(uid, maps, whitelist, seen, ref count);
                if (count >= enough)
                    return count;
            }
        }

        return count;
    }

    /// <summary>Adds one candidate to the count if it is on the maps and passes the whole whitelist.</summary>
    private void Consider(EntityUid uid, HashSet<MapId> maps, EntityWhitelist whitelist, HashSet<EntityUid> seen, ref int count)
    {
        if (!seen.Add(uid))
            return;

        if (!_xformQuery.TryGetComponent(uid, out var xform) || !maps.Contains(xform.MapID))
            return;

        if (!_whitelist.IsWhitelistPass(whitelist, uid))
            return;

        count += _stackQuery.TryGetComponent(uid, out var stackComp) ? stackComp.Count : 1;
    }
}
