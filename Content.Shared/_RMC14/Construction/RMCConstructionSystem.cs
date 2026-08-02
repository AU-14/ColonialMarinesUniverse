using Content.Shared._AU14.ZLevelBuilding;
using Content.Shared._RMC14.Construction.Prototypes;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Emplacements;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Ladder;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Construction;
using Content.Shared.Construction.Components;
using Content.Shared.Coordinates;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Construction;

public sealed partial class RMCConstructionSystem : EntitySystem
{
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private FixtureSystem _fixture = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedWeaponMountSystem _weaponMount = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ExamineSystemShared _examine = default!;

    private static readonly EntProtoId Blocker = "RMCDropshipDoorBlocker";

    private readonly List<EntityCoordinates> _toCreate = new();

    private EntityQuery<DoorComponent> _doorQuery;

    public override void Initialize()
    {
        _doorQuery = GetEntityQuery<DoorComponent>();

        SubscribeLocalEvent<DropshipHijackLandedEvent>(OnDropshipHijackLanded);

        SubscribeLocalEvent<RMCConstructionPreventCollideComponent, PreventCollideEvent>(OnConstructionPreventCollide);

        SubscribeLocalEvent<RMCConstructionItemComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<RMCConstructionItemComponent, RMCConstructionBuildDoAfterEvent>(OnBuildDoAfter);

        SubscribeLocalEvent<RMCConstructionAttemptEvent>(OnConstructionAttempt);

        SubscribeLocalEvent<DropshipComponent, DropshipMapInitEvent>(OnDropshipMapInit);

        SubscribeLocalEvent<RMCDropshipBlockedComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RMCDropshipBlockedComponent, AnchorAttemptEvent>(OnAnchorAttempt);
        SubscribeLocalEvent<RMCDropshipBlockedComponent, UserAnchoredEvent>(OnUserAnchored);

        Subs.BuiEvents<RMCConstructionItemComponent>(RMCConstructionUiKey.Key,
            subs =>
            {
                subs.Event<RMCConstructionBuiMsg>(OnConstructionBuiMsg);
            });
    }

    private void OnDropshipHijackLanded(ref DropshipHijackLandedEvent ev)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<RMCReplaceOnHijackLandComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!TryComp(uid, out TransformComponent? xform) || xform.MapUid != ev.Map)
                continue;

            if (comp.Id is not { } id)
            {
                Del(uid);
                continue;
            }

            var coordinates = _transform.GetMoverCoordinates(uid, xform);
            var rotation = xform.LocalRotation;
            Del(uid);
            var spawned = Spawn(id, coordinates);
            _transform.SetLocalRotation(spawned, rotation);
        }
    }

    public void OnUseInHand(Entity<RMCConstructionItemComponent> ent, ref UseInHandEvent args)
    {
        var user = args.User;

        args.Handled = true;

        _ui.OpenUi(ent.Owner, RMCConstructionUiKey.Key, user);
    }

    private void OnConstructionBuiMsg(Entity<RMCConstructionItemComponent> ent, ref RMCConstructionBuiMsg args)
    {
        Build(ent, args.Actor, args.Build, args.Amount);
    }

    public bool Build(Entity<RMCConstructionItemComponent> ent, EntityUid user, ProtoId<RMCConstructionPrototype> protoID, int amount)
    {
        if (_net.IsClient)
            return false;

        if (!_prototype.TryIndex<RMCConstructionPrototype>(protoID, out var proto))
            return false;

        if (!TryComp(user, out TransformComponent? transform))
            return false;

        if (proto.Skill is { } skill && !_skills.HasSkill(user, skill, proto.RequiredSkillLevel))
        {
            var message = Loc.GetString("rmc-construction-untrained-build");
            _popup.PopupEntity(message, ent, user, PopupType.SmallCaution);
            return false;
        }

        var direction = transform.LocalRotation.GetCardinalDir();
        var coordinates = transform.Coordinates;

        if (!proto.IgnoreBuildRestrictions && !CanBuildAt(coordinates, proto.Prototype, out var popup, direction: direction, collision: proto.RestrictedCollisionGroup, user: user))
        {
            _popup.PopupEntity(popup, ent, user, PopupType.SmallCaution);
            return false;
        }

        if (proto.RestrictedTags is { } tags && _rmcMap.TileHasAnyTag(coordinates, tags))
        {
            var message = Loc.GetString("rmc-construction-not-proper-surface", ("construction", proto.Name));
            _popup.PopupEntity(message, ent, user, PopupType.SmallCaution);
            return false;
        }

        if (proto.MaterialCost is { } materialCost && TryComp<StackComponent>(ent.Owner, out var stack))
        {
            var totalAmount = amount / proto.Amount;
            var cost = (amount == proto.Amount) ? materialCost : totalAmount * materialCost;
            var costEv = new RMCConstructionCostEvent(user, stack.StackTypeId, cost, cost);
            RaiseLocalEvent(user, ref costEv, true);
            cost = Math.Max(1, costEv.Cost);

            if (stack.Count < cost)
            {
                var message = Loc.GetString("rmc-construction-more-material", ("material", ent), ("object", proto.Name));
                _popup.PopupEntity(message, user, user, PopupType.SmallCaution);
                return false;
            }
        }

        // TODO add the ability to combine materials

        var ev = new RMCConstructionBuildDoAfterEvent(
            proto.ID,
            amount,
            GetNetCoordinates(coordinates),
            direction
        );

        var skillMultiplier = _skills.HasSkill(user, proto.DelaySkill, 2) ? 1 : 2;
        var delay = proto.DoAfterTime * skillMultiplier;

        var delayEv = new RMCConstructionDelayEvent(user, delay, delay);
        RaiseLocalEvent(user, ref delayEv, true);
        delay = delayEv.Delay;

        var doAfterTime = Math.Max(delay.TotalSeconds, proto.DoAfterTimeMin.TotalSeconds);

        var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(doAfterTime), ev, ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = false,
            MovementThreshold = 0.5f,
            DuplicateCondition = DuplicateConditions.SameEvent,
            CancelDuplicate = true
        };

        _doAfter.TryStartDoAfter(doAfter);
        UpdateStackAmountUI(ent);
        return true;
    }

    private void OnConstructionPreventCollide(Entity<RMCConstructionPreventCollideComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.Target is not { } target || Deleted(target))
        {
            RemCompDeferred<RMCConstructionPreventCollideComponent>(ent.Owner);
            return;
        }

        if (args.OtherEntity != target)
            return;

        if (!_examine.InRangeUnOccluded(ent.Owner, target, ent.Comp.Range))
        {
            RemCompDeferred<RMCConstructionPreventCollideComponent>(ent.Owner);
            return;
        }

        args.Cancelled = true;
    }

    public void MakeConstructionImmuneToCollision(EntityUid construction, EntityUid user)
    {
        var constructionComp = EnsureComp<RMCConstructionPreventCollideComponent>(construction);
        constructionComp.Target = user;
        Dirty(construction, constructionComp);
    }

    private void OnBuildDoAfter(Entity<RMCConstructionItemComponent> ent, ref RMCConstructionBuildDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (_net.IsClient)
            return;

        if (!_prototype.TryIndex(args.Prototype, out var entry))
            return;

        var coordinates = GetCoordinates(args.Coordinates);
        args.Handled = true;

        // If the stack amount is equal to the default amount, use the default material cost.
        // Otherwise, use the material cost times the stack amount.
        var totalAmount = args.Amount / entry.Amount; // So a stack of 20 with an amount of 4 and a cost of 1 is correctly 5 cost
        var cost = (args.Amount == entry.Amount) ? entry.MaterialCost : totalAmount * entry.MaterialCost;

        var materialShortfall = 0;
        var stackType = string.Empty;
        if (TryComp<StackComponent>(ent.Owner, out var stack))
        {
            var baseCost = cost ?? 1;
            var costEv = new RMCConstructionCostEvent(args.User, stack.StackTypeId, baseCost, baseCost);
            RaiseLocalEvent(args.User, ref costEv, true);
            var paidCost = Math.Max(1, costEv.Cost);
            if (!_stack.Use(ent.Owner, paidCost, stack))
            {
                var message = Loc.GetString("rmc-construction-more-material", ("material", ent.Owner), ("object", entry.Name));
                _popup.PopupEntity(message, args.User, args.User, PopupType.SmallCaution);
                return;
            }

            materialShortfall = Math.Max(0, baseCost - paidCost);
            stackType = stack.StackTypeId;
        }
        else if (_net.IsServer)
        {
            QueueDel(ent.Owner);
        }

        if (!Deleted(ent))
            UpdateStackAmountUI(ent);

        var builtEntities = new List<EntityUid>();
        if (args.Amount > 1)
        {
            builtEntities.AddRange(SpawnMultiple(entry.Prototype, args.Amount, coordinates));
        }
        else
        {
            var built = SpawnAtPosition(entry.Prototype, coordinates);
            builtEntities.Add(built);

            if (!entry.NoRotate)
                _transform.SetLocalRotation(built, args.Direction.ToAngle());

            // This is so you won't be stuck inside of a construction you build
            // Removes collision with the construction until you leave
            if (!HasComp<BarricadeComponent>(built))
                MakeConstructionImmuneToCollision(built, args.User);

        }

        for (var i = 0; i < builtEntities.Count; i++)
        {
            var built = builtEntities[i];
            var perEntityShortfall = builtEntities.Count == 0
                ? 0
                : materialShortfall / builtEntities.Count + (i < materialShortfall % builtEntities.Count ? 1 : 0);
            var transactionEv = new RMCConstructionTransactionCompletedEvent(
                built,
                args.User,
                stackType,
                perEntityShortfall);
            RaiseLocalEvent(built, ref transactionEv, broadcast: true);

            var completedEv = new ConstructionCompletedEvent(built, args.User);
            RaiseLocalEvent(built, ref completedEv, broadcast: true);
        }
    }

    /// <summary>
    ///     Say you want to spawn 97 units of something that has a max stack count of 30.
    ///     This would spawn 3 stacks of 30 and 1 stack of 7.
    /// </summary>
    public List<EntityUid> SpawnMultiple(string entityPrototype, int amount, EntityCoordinates spawnPosition)
    {
        if (_net.IsClient)
            return new();

        if (amount <= 0)
        {
            Log.Error(
                $"Attempted to spawn an invalid stack: {entityPrototype}, {amount}. Trace: {Environment.StackTrace}");
            return new();
        }

        var spawns = CalculateSpawns(entityPrototype, amount);

        var spawnedEnts = new List<EntityUid>();
        foreach (var count in spawns)
        {
            var entity = SpawnAtPosition(entityPrototype, spawnPosition);
            spawnedEnts.Add(entity);
            _stack.SetCount(entity, count);
        }

        return spawnedEnts;
    }

    /// <summary>
    /// Calculates how many stacks to spawn that total up to <paramref name="amount"/>.
    /// </summary>
    /// <param name="entityPrototype">The stack to spawn.</param>
    /// <param name="amount">The amount of pieces across all stacks.</param>
    /// <returns>The list of stack counts per entity.</returns>
    public List<int> CalculateSpawns(string entityPrototype, int amount)
    {
        var proto = _prototype.Index<EntityPrototype>(entityPrototype);
        proto.TryComp<StackComponent>(out var stack, EntityManager.ComponentFactory);
        var maxCountPerStack = _stack.GetMaxCount(stack);
        var amounts = new List<int>();
        while (amount > 0)
        {
            var countAmount = Math.Min(maxCountPerStack, amount);
            amount -= countAmount;
            amounts.Add(countAmount);
        }

        return amounts;
    }

    private void UpdateStackAmountUI(Entity<RMCConstructionItemComponent> ent)
    {
        var state = new RMCConstructionBuiState(string.Empty);
        _ui.SetUiState(ent.Owner, RMCConstructionUiKey.Key, state);
    }

    private void OnConstructionAttempt(ref RMCConstructionAttemptEvent ev)
    {
        if (ev.Cancelled)
            return;

        var graph = _prototype.Index(ev.Prototype.Graph);
        var node = graph.Nodes[ev.Prototype.TargetNode];
        var entProtoId = node.Entity.GetId(null, null, new(EntityManager));

        if (entProtoId == null)
            return;

        var entProto = _prototype.Index<EntityPrototype>(entProtoId);

        if (!CanBuildAt(ev.Location, entProto, out var popup, user: ev.User))
        {
            ev.Popup = popup;
            ev.Cancelled = true;
        }
    }

    private void OnDropshipMapInit(Entity<DropshipComponent> ent, ref DropshipMapInitEvent args)
    {
        _toCreate.Clear();

        var enumerator = Transform(ent).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (!_doorQuery.HasComp(child))
                continue;

            _toCreate.Add(child.ToCoordinates());
        }

        foreach (var toCreate in _toCreate)
        {
            SpawnAtPosition(Blocker, toCreate);
        }
    }

    private void OnMapInit(Entity<RMCDropshipBlockedComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp(ent, out PhysicsComponent? physics))
            return;

        var shape = new PhysShapeCircle(0.49f);
        _fixture.TryCreateFixture(
            ent,
            shape,
            ent.Comp.FixtureId,
            collisionMask: (int) CollisionGroup.DropshipImpassable,
            body: physics
        );
    }

    private void OnAnchorAttempt(Entity<RMCDropshipBlockedComponent> ent, ref AnchorAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!CanBuildAt(ent.Owner.ToCoordinates(), Name(ent), out var popup, true))
        {
            _popup.PopupClient(popup, ent, args.User, PopupType.SmallCaution);
            args.Cancel();
        }
    }

    private void OnUserAnchored(Entity<RMCDropshipBlockedComponent> ent, ref UserAnchoredEvent args)
    {
        if (!CanBuildAt(ent.Owner.ToCoordinates(), Name(ent), out _, true))
        {
            var xform = Transform(ent);
            _transform.Unanchor(ent.Owner, xform);
        }
    }

    public bool CanConstruct(EntityUid? user)
    {
        return !HasComp<DisableConstructionComponent>(user);
    }

    public bool CanBuildAt(EntityCoordinates coordinates, EntProtoId prototype, out string? popup, bool anchoring = false, Direction direction = Direction.Invalid, CollisionGroup? collision = null, EntityUid? user = null)
    {
        popup = default;
        if (!_prototype.TryIndex<EntityPrototype>(prototype, out var proto))
            return false;

        // AU14: laying a floor is precisely how you make unbuildable terrain buildable, so a tile applier is
        // not subject to the terrain's own blockConstruction/blockAnchoring flags - otherwise beach, coast and
        // water-shore tiles (whose abstract parents carry those flags, so every variant inherits them) refuse
        // the tile ghost outright and the player just sees nothing appear. Structures, barricades and every
        // other recipe stay blocked exactly as before; they can be built once a floor is down.
        var isTileApplier = proto.TryComp<TileApplierComponent>(out _, _componentFactory);

        var canBuild = CanBuildAt(coordinates, proto.Name, out popup, anchoring, direction, collision, isTileApplier);
        if (!canBuild)
            return false;

        if (_transform.GetGrid(coordinates) is not { } gridId)
            return true;

        if (!TryComp(gridId, out MapGridComponent? grid))
            return true;

        if (proto.TryComp(out BarricadeComponent? barricade, _componentFactory))
        {
            return !_weaponMount.HasWeaponMountNearbyPopup((gridId, grid), coordinates, proto, user: user);
        }

        return canBuild;
    }

    /// <param name="ignoreBlockedTerrain">Skips the tile's own blockConstruction/blockAnchoring veto. Set for
    /// tile appliers, which exist to replace that terrain.</param>
    public bool CanBuildAt(EntityCoordinates coordinates, string? prototypeName, out string? popup, bool anchoring = false, Direction direction = Direction.Invalid, CollisionGroup? collision = null, bool ignoreBlockedTerrain = false)
    {
        popup = default;
        if (_transform.GetGrid(coordinates) is not { } gridId)
            return true;

        if (!_turf.TryGetTileRef(coordinates, out var turf))
            return false;

        prototypeName ??= Loc.GetString("rmc-construction-name");
        if (HasComp<DropshipComponent>(gridId))
        {
            popup = Loc.GetString("rmc-construction-not-proper-surface", ("construction", prototypeName));
            return false;
        }

        if (!TryComp(gridId, out MapGridComponent? grid))
            return true;

        var indices = _map.TileIndicesFor(gridId, grid, coordinates);
        if (!_map.TryGetTileDef(grid, indices, out var def))
            return true;

        var invalid = !ignoreBlockedTerrain && def is ContentTileDefinition { BlockConstruction: true };
        if (anchoring)
            invalid = !ignoreBlockedTerrain && def is ContentTileDefinition { BlockAnchoring: true };

        if (invalid || _rmcMap.HasAnchoredEntityEnumerator<LadderComponent>(coordinates))
        {
            popup = Loc.GetString("rmc-construction-not-proper-surface", ("construction", prototypeName));
            return false;
        }

        if (direction != Direction.Invalid && _rmcMap.HasAnchoredEntityEnumerator<BarricadeComponent>(coordinates, facing: direction.AsFlag()))
        {
            popup = Loc.GetString("rmc-construction-not-barricade-clear");
            return false;
        }

        if (collision is { } collisionGroup && _turf.IsTileBlocked(turf.Value, collisionGroup))
        {
            popup = Loc.GetString("rmc-construction-not-proper-surface", ("construction", prototypeName));
            return false;
        }

        return true;
    }
}

/// <summary>Extension point for server-authoritative material pricing without coupling RMC construction to a fork.</summary>
[ByRefEvent]
public record struct RMCConstructionCostEvent(EntityUid User, string StackType, int BaseCost, int Cost);

/// <summary>Extension point for construction delay modifiers.</summary>
[ByRefEvent]
public record struct RMCConstructionDelayEvent(EntityUid User, TimeSpan BaseDelay, TimeSpan Delay);

/// <summary>Reports the exact material delta associated with one newly-built entity.</summary>
[ByRefEvent]
public record struct RMCConstructionTransactionCompletedEvent(
    EntityUid Built,
    EntityUid User,
    string StackType,
    int MaterialShortfall);
