using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RMC14.ARES;
using Content.Shared._RMC14.ARES.Logs;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Components;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.PowerLoader;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Coordinates;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Dropship.Fabricator;

public sealed partial class DropshipFabricatorSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private RMCComponentsSystem _components = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private ARESCoreSystem _core = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerLoaderSystem _powerLoader = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    // CMU14: faction gameplay fixes.
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    private int _startingPoints;
    private TimeSpan _gainEvery;

    public ImmutableArray<EntProtoId<DropshipFabricatorPrintableComponent>> Printables { get; private set; }

    private static readonly EntProtoId<ARESLogTypeComponent> LogCat = "ARESTabDropshipLogs";

    public override void Initialize()
    {
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        SubscribeLocalEvent<DropshipFabricatorComponent, MapInitEvent>(OnFabricatorMapInit);
        SubscribeLocalEvent<DropshipFabricatorComponent, DropshipFabricatoreRecycleDoafterEvent>(OnDropshipPartRecycled);

        Subs.BuiEvents<DropshipFabricatorComponent>(DropshipFabricatorUi.Key,
            subs =>
            {
                subs.Event<DropshipFabricatorPrintMsg>(OnPrintMsg);
                subs.Event<DropshipFabricatorCancelQueueMsg>(OnCancelQueueMsg);
            });

        Subs.CVar(_config, RMCCVars.RMCDropshipFabricatorStartingPoints, v => _startingPoints = v, true);
        Subs.CVar(_config, RMCCVars.RMCDropshipFabricatorGainEverySeconds, v => _gainEvery = TimeSpan.FromSeconds(v), true);

        ReloadPrototypes();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs ev)
    {
        if (ev.WasModified<EntityPrototype>())
            ReloadPrototypes();
    }

    private void OnFabricatorMapInit(Entity<DropshipFabricatorComponent> ent, ref MapInitEvent args)
    {
        // CMU14: faction gameplay fixes.
        EnsureAccount(ent);
    }

    private void OnDropshipPartRecycled(Entity<DropshipFabricatorComponent> ent, ref DropshipFabricatoreRecycleDoafterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        // CMU14: faction gameplay fixes.
        EnsureAccount(ent);
        if (!TryComp(args.Used, out DropshipFabricatorPrintableComponent? printable) ||
            !TryComp(ent.Comp.Account, out DropshipFabricatorPointsComponent? points))
        {
            return;
        }

        args.Handled = true;

        var refund = printable.Cost;
        if (TryComp(args.Used, out DropshipAmmoComponent? ammo))
            refund = (int) (refund * (float) ammo.Rounds / ammo.MaxRounds);

        points.Points += (int) (refund * printable.RecycleMultiplier);
        Dirty(ent.Comp.Account.Value, points);
        // CMU14: faction gameplay fixes.
        SendUIStateAll((ent.Comp.Account.Value, points));
        Del(args.Used);

        _audio.PlayPvs(ent.Comp.RecycleSound, ent);
        _powerLoader.TrySyncHands(args.User);
    }

    private void OnPrintMsg(Entity<DropshipFabricatorComponent> ent, ref DropshipFabricatorPrintMsg args)
    {
        if (args.Id == default || !_prototypes.TryIndex(args.Id, out var proto))
            return;

        if (!TryGetPrintable(proto, out var printable))
            return;

        var actor = args.Actor;
        // CMU14: faction gameplay fixes.
        EnsureAccount(ent);
        if (!TryComp(ent.Comp.Account, out DropshipFabricatorPointsComponent? points))
            return;

        if (ent.Comp.Queue.Count >= ent.Comp.MaxQueue)
        {
            _popup.PopupClient(Loc.GetString("rmc-dropship-fabricator-queue-full"), actor, actor, PopupType.SmallCaution);
            return;
        }

        if (printable.Cost > points.Points)
        {
            _popup.PopupClient(Loc.GetString("rmc-dropship-fabricator-insufficient-points"), actor, actor, PopupType.SmallCaution);
            return;
        }

        points.Points -= printable.Cost;
        Dirty(ent.Comp.Account.Value, points);
        // CMU14: faction gameplay fixes.
        SendUIStateAll((ent.Comp.Account.Value, points));

        ent.Comp.Queue.Add(new DropshipFabricatorQueueEntry(proto.ID, printable.Cost));
        Dirty(ent);
        TryStartNextPrint(ent);

        _core.CreateARESLog(ent, LogCat, (string) $"{Name(args.Actor)} printed {proto.Name} for {printable.Cost} points at the dropship lathe");
    }

    private void OnCancelQueueMsg(Entity<DropshipFabricatorComponent> ent, ref DropshipFabricatorCancelQueueMsg args)
    {
        // CMU14: faction gameplay fixes.
        EnsureAccount(ent);
        if (args.Index < 0 || args.Index >= ent.Comp.Queue.Count)
            return;

        var entry = ent.Comp.Queue[args.Index];
        ent.Comp.Queue.RemoveAt(args.Index);

        if (TryComp(ent.Comp.Account, out DropshipFabricatorPointsComponent? points))
        {
            points.Points += entry.Cost;
            Dirty(ent.Comp.Account.Value, points);
            // CMU14: faction gameplay fixes.
            SendUIStateAll((ent.Comp.Account.Value, points));
        }

        Dirty(ent);
    }

    private bool TryStartNextPrint(Entity<DropshipFabricatorComponent> ent)
    {
        if (ent.Comp.Printing != null)
            return false;

        var changed = false;
        while (ent.Comp.Queue.Count > 0)
        {
            changed = true;
            var entry = ent.Comp.Queue[0];
            ent.Comp.Queue.RemoveAt(0);

            if (!_prototypes.TryIndex(entry.Id, out var proto) ||
                !TryGetPrintable(proto, out var printable))
            {
                RefundQueuedCost(ent, entry.Cost);
                continue;
            }

            ent.Comp.Printing = entry.Id;
            ent.Comp.PrintAt = _timing.CurTime + printable.Delay;
            Dirty(ent);

            _appearance.SetData(ent, DropshipFabricatorVisuals.State, DropshipFabricatorState.Fabricating);
            return true;
        }

        if (changed)
            Dirty(ent);

        return false;
    }

    private void RefundQueuedCost(Entity<DropshipFabricatorComponent> ent, int cost)
    {
        if (!TryComp(ent.Comp.Account, out DropshipFabricatorPointsComponent? points))
            return;

        points.Points += cost;
        Dirty(ent.Comp.Account.Value, points);
        // CMU14: faction gameplay fixes.
        SendUIStateAll((ent.Comp.Account.Value, points));
    }

    // CMU14: faction gameplay fixes.
    private void EnsureAccount(Entity<DropshipFabricatorComponent> ent)
    {
        if (_net.IsClient)
            return;

        // Carrier maps initialize before their ShipFaction is assigned. Bind once ownership is available.
        // Keep a funded account stable afterwards, including while orders are queued.
        if (TryComp(ent.Comp.Account, out DropshipFabricatorPointsComponent? current) && current.Faction != null)
            return;

        var faction = GetFaction(ent.Owner);
        if (current != null && faction == null)
            return;

        var account = EnsurePoints(faction);
        ent.Comp.Account = account.Owner;
        ent.Comp.Points = account.Comp.Points;
        Dirty(ent);
    }

    private string? GetFaction(EntityUid fabricator)
    {
        var parent = fabricator;
        while (TryComp(parent, out TransformComponent? transform))
        {
            if (TryComp<ShipFactionComponent>(parent, out var ship) && !string.IsNullOrWhiteSpace(ship.Faction))
                return ship.Faction;

            if (TryComp<DropshipDeckComponent>(parent, out var deck) &&
                TryComp<ShipFactionComponent>(deck.Ship, out var carrier) && !string.IsNullOrWhiteSpace(carrier.Faction))
            {
                return carrier.Faction;
            }

            parent = transform.ParentUid;
        }

        // Other decks may be separate maps whose faction is held by the carrier's primary deck.
        if (Transform(fabricator).MapUid is { } map && _zLevels.TryGetZNetwork(map, out var network))
        {
            IReadOnlyDictionary<int, EntityUid?> levels = network.Value.Comp.ZLevels;
            foreach (var level in levels.Values)
            {
                if (TryComp<ShipFactionComponent>(level, out var ship) && !string.IsNullOrWhiteSpace(ship.Faction))
                    return ship.Faction;
            }
        }

        return null;
    }

    private Entity<DropshipFabricatorPointsComponent> EnsurePoints(string? faction)
    {
        var query = EntityQueryEnumerator<DropshipFabricatorPointsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // CMU14: faction gameplay fixes.
            if (string.Equals(comp.Faction, faction, StringComparison.OrdinalIgnoreCase))
                return (uid, comp);
        }

        var points = Spawn(null, MapCoordinates.Nullspace);
        var pointsComp = EnsureComp<DropshipFabricatorPointsComponent>(points);
        // CMU14: faction gameplay fixes.
        pointsComp.Faction = faction;
        pointsComp.Points = _startingPoints;
        return (points, pointsComp);
    }

    private void ReloadPrototypes()
    {
        var printables = new List<EntityPrototype>();
        var prototypes = _prototypes.EnumeratePrototypes<EntityPrototype>();
        foreach (var prototype in prototypes)
        {
            if (TryGetPrintable(prototype, out _))
                printables.Add(prototype);
        }

        printables.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        Printables = printables.Select(e => new EntProtoId<DropshipFabricatorPrintableComponent>(e.ID)).ToImmutableArray();
    }

    private bool TryGetPrintable(EntityPrototype prototype,
        [NotNullWhen(true)] out DropshipFabricatorPrintableComponent? printable)
    {
        printable = null;

        // RemoveComponents runs on spawned entities, but the catalog and print requests use prototypes.
        if (_components.RemovesComponent<DropshipFabricatorPrintableComponent>(prototype))
            return false;

        return prototype.TryComp(out printable, _compFactory);
    }

    // CMU14: faction gameplay fixes.
    public void ChangeBudget(int amount, string? faction = null)
    {
        var accountQuery = EntityQueryEnumerator<DropshipFabricatorPointsComponent>();
        while (accountQuery.MoveNext(out var uid, out var comp))
        {
            // CMU14: faction gameplay fixes.
            if (faction != null && !string.Equals(comp.Faction, faction, StringComparison.OrdinalIgnoreCase))
                continue;

            comp.Points += amount;
            Dirty(uid, comp);
            // CMU14: faction gameplay fixes.
            SendUIStateAll((uid, comp));
        }
    }

    // CMU14: faction gameplay fixes.
    private void SendUIStateAll(Entity<DropshipFabricatorPointsComponent> account)
    {
        var fabricatorQuery = EntityQueryEnumerator<DropshipFabricatorComponent>();
        while (fabricatorQuery.MoveNext(out var fabricatorId, out var fabricator))
        {
            // CMU14: faction gameplay fixes.
            if (fabricator.Account != account.Owner)
                continue;

            fabricator.Points = account.Comp.Points;
            Dirty(fabricatorId, fabricator);
        }
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var allFabricatorQuery = EntityQueryEnumerator<DropshipFabricatorComponent, TransformComponent>();
        while (allFabricatorQuery.MoveNext(out var uid, out var comp, out var xform))
        {
            // CMU14: faction gameplay fixes.
            EnsureAccount((uid, comp));
            if (comp.Printing == null)
            {
                TryStartNextPrint((uid, comp));
                continue;
            }

            if (time < comp.PrintAt)
                continue;

            var rotation = _transform.GetWorldRotation(xform);
            var coordinates = uid.ToCoordinates().Offset(comp.PrintOffset.Rotate(rotation));
            SpawnAtPosition(comp.Printing.Value, coordinates);

            comp.Printing = null;
            Dirty(uid, comp);

            if (!TryStartNextPrint((uid, comp)))
                _appearance.SetData(uid, DropshipFabricatorVisuals.State, DropshipFabricatorState.Idle);
        }

        var pointsQuery = EntityQueryEnumerator<DropshipFabricatorPointsComponent>();
        while (pointsQuery.MoveNext(out var pointsId, out var points))
        {
            if (time < points.NextPointsAt)
                continue;

            points.NextPointsAt = time + _gainEvery;
            points.Points++;
            Dirty(pointsId, points);

            // CMU14: faction gameplay fixes.
            SendUIStateAll((pointsId, points));
        }
    }
}

[Serializable, NetSerializable]
public sealed partial class DropshipFabricatoreRecycleDoafterEvent : SimpleDoAfterEvent
{
}
