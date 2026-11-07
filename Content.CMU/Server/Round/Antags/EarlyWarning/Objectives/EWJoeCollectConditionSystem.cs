using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Hands;
using Content.Shared.Inventory.Events;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// Rolls, titles and scores <see cref="EWJoeCollectConditionComponent"/> objectives. An item counts once
/// the Joe has taken it in hand or equipped it (remembered on the condition) or while it is anywhere in
/// the Joe's container tree; an item that satisfied an earlier, credited collect objective never counts
/// again (<see cref="EWJoeComponent.SpentCollectibles"/>).
/// </summary>
public sealed partial class EWJoeCollectConditionSystem : EntitySystem
{
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    private EntityQuery<ContainerManagerComponent> _containerQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _containerQuery = GetEntityQuery<ContainerManagerComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<EWJoeCollectConditionComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<EWJoeCollectConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<EWJoeCollectConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);

        // acquisition: the Joe took something in hand or put it on
        SubscribeLocalEvent<EWJoeComponent, DidEquipHandEvent>(OnJoeEquippedHand);
        SubscribeLocalEvent<EWJoeComponent, DidEquipEvent>(OnJoeEquipped);
    }

    #region Assignment

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

            // the planet is a z-level network of maps, not one map; items already spent do not exist for us
            var maps = _zLevels.GetAllNetworkMapIds(mapId);
            var spent = CompOrNull<EWJoeComponent>(owner)?.SpentCollectibles;
            var available = CountAvailable(maps, ent.Comp.Whitelist, max, spent);
            if (available < min)
            {
                args.Cancelled = true;
                return;
            }

            max = Math.Min(max, available);
        }

        ent.Comp.Target = _random.Next(min, max + 1);
        ent.Comp.Obtained.Clear();
    }

    private void OnAfterAssign(Entity<EWJoeCollectConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        _metaData.SetEntityName(ent, Loc.GetString(ent.Comp.Title, ("count", ent.Comp.Target)), args.Meta);
        _metaData.SetEntityDescription(ent, Loc.GetString(ent.Comp.Description, ("count", ent.Comp.Target)), args.Meta);
    }

    #endregion

    #region Progress

    private void OnGetProgress(Entity<EWJoeCollectConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (ent.Comp.Target <= 0 || ent.Comp.Credited)
        {
            args.Progress = 1f;
            return;
        }

        if (args.Mind.OwnedEntity is not { } owner)
        {
            args.Progress = 0f;
            return;
        }

        var counted = Counting(owner, ent.Comp).Count;
        args.Progress = Math.Clamp(counted / (float) ent.Comp.Target, 0f, 1f);
    }

    private void OnJoeEquippedHand(Entity<EWJoeComponent> ent, ref DidEquipHandEvent args)
    {
        Acquire(ent, args.Equipped);
    }

    private void OnJoeEquipped(Entity<EWJoeComponent> ent, ref DidEquipEvent args)
    {
        Acquire(ent, args.Equipment);
    }

    /// <summary>Remembers <paramref name="item"/> on every unfinished collect objective it fits.</summary>
    public void Acquire(Entity<EWJoeComponent> joe, EntityUid item)
    {
        if (joe.Comp.SpentCollectibles.Contains(item))
            return;

        if (!_mind.TryGetMind(joe, out _, out var mind))
            return;

        foreach (var objective in mind.Objectives)
        {
            if (!TryComp(objective, out EWJoeCollectConditionComponent? collect) || collect.Credited)
                continue;

            if (_whitelist.IsWhitelistPass(collect.Whitelist, item))
                collect.Obtained.Add(item);
        }
    }

    /// <summary>
    /// The distinct items counting toward the objective right now: remembered acquisitions that still
    /// exist plus whatever is in the owner's container tree, minus items spent on earlier objectives.
    /// </summary>
    public HashSet<EntityUid> Counting(EntityUid owner, EWJoeCollectConditionComponent collect)
    {
        var spent = CompOrNull<EWJoeComponent>(owner)?.SpentCollectibles;

        collect.Obtained.RemoveWhere(uid => !Exists(uid));
        var counted = new HashSet<EntityUid>(collect.Obtained);
        counted.UnionWith(HeldMatching(owner, collect.Whitelist));

        if (spent != null)
            counted.ExceptWith(spent);

        return counted;
    }

    /// <summary>
    /// Marks everything counting toward a just-credited collect objective as spent, so no later
    /// collect objective can be satisfied by the same items.
    /// </summary>
    public void MarkSpent(Entity<EWJoeComponent> joe, EntityUid objective)
    {
        if (!TryComp(objective, out EWJoeCollectConditionComponent? collect))
            return;

        joe.Comp.SpentCollectibles.RemoveWhere(uid => !Exists(uid));
        joe.Comp.SpentCollectibles.UnionWith(Counting(joe, collect));
        // the objective keeps reading complete; only later objectives see these items as spent
        collect.Credited = true;
    }

    /// <summary>Distinct matching entities in the owner's container tree: hands, inventory slots, bags, implants.</summary>
    public HashSet<EntityUid> HeldMatching(EntityUid owner, EntityWhitelist whitelist)
    {
        var found = new HashSet<EntityUid>();
        if (!_containerQuery.TryGetComponent(owner, out var manager))
            return found;

        var stack = new Stack<ContainerManagerComponent>();
        stack.Push(manager);

        while (stack.TryPop(out var current))
        {
            foreach (var container in current.Containers.Values)
            {
                foreach (var contained in container.ContainedEntities)
                {
                    if (_whitelist.IsWhitelistPass(whitelist, contained))
                        found.Add(contained);

                    if (_containerQuery.TryGetComponent(contained, out var inner))
                        stack.Push(inner);
                }
            }
        }

        return found;
    }

    #endregion

    #region Existence

    /// <summary>
    /// Counts whitelisted entities on the given maps that are not in <paramref name="exclude"/> and pass
    /// <paramref name="filter"/>, stopping once <paramref name="enough"/> are found. Walks only the component
    /// and tag queries the whitelist names, never every entity.
    /// </summary>
    public int CountAvailable(HashSet<MapId> maps, EntityWhitelist whitelist, int enough, HashSet<EntityUid>? exclude = null,
        Func<EntityUid, bool>? filter = null)
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
                    Consider(uid, maps, whitelist, seen, exclude, filter, ref count);
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
                Consider(uid, maps, whitelist, seen, exclude, filter, ref count);
                if (count >= enough)
                    return count;
            }
        }

        return count;
    }

    private void Consider(EntityUid uid, HashSet<MapId> maps, EntityWhitelist whitelist, HashSet<EntityUid> seen, HashSet<EntityUid>? exclude,
        Func<EntityUid, bool>? filter, ref int count)
    {
        if (!seen.Add(uid) || exclude != null && exclude.Contains(uid))
            return;

        if (!_xformQuery.TryGetComponent(uid, out var xform) || !maps.Contains(xform.MapID))
            return;

        if (!_whitelist.IsWhitelistPass(whitelist, uid) || filter != null && !filter(uid))
            return;

        count++;
    }

    #endregion
}
