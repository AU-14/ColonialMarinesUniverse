using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Construction;
using Content.Shared.CMU14.Fob;
using Content.Shared.Audio;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Fob;

/// <summary>Local, destructible utilities powered only by the selected FOB's registered reserve.</summary>
public sealed partial class CMUFobEquipmentSystem : EntitySystem
{
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private RMCConstructionSystem _construction = default!;
    [Dependency] private CMUFobInteractionSystem _controls = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private CMUFobEffectsSystem _effects = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private CMUFobMapSystem _sites = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private CMUFobReserveSystem _reserve = default!;
    [Dependency] private SharedRoofSystem _roof = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedToolSystem _tools = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    private TimeSpan _nextUpdate;
    private readonly HashSet<Entity<PhysicsComponent>> _obstacles = new();
    private const string Wrench = "Anchoring";
    private const string Cutters = "Cutting";

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUFobEquipmentComponent, InteractUsingEvent>(OnTool);
        SubscribeLocalEvent<CMUFobEquipmentComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<CMUFobEquipmentComponent, CMUFobEquipmentDoAfterEvent>(OnFinished);
        SubscribeLocalEvent<CMUFobEquipmentComponent, DoAfterAttemptEvent<CMUFobEquipmentDoAfterEvent>>(OnAttempt);
        SubscribeLocalEvent<CMUFobEquipmentComponent, GettingPickedUpAttemptEvent>(OnPickup);
        SubscribeLocalEvent<CMUFobEquipmentComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUFobEquipmentComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<CMUFobEquipmentComponent, DamageChangedEvent>(OnDamage);
        SubscribeLocalEvent<CMUFobEquipmentComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUFobEquipmentComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CMUFobEquipmentComponent> ent, ref MapInitEvent args) => UpdateVisuals(ent);

    private bool AtSite(Entity<CMUFobEquipmentComponent> ent, out Entity<CMUFobNetworkComponent> bank)
    {
        bank = default;
        return _reserve.Enabled && !TerminatingOrDeleted(ent) && !MetaData(ent).EntityPaused
            && !_containers.IsEntityInContainer(ent) && _reserve.TryGetNetwork("govfor", out bank)
            && bank.Comp.State != CMUFobReserveState.Closed && !MetaData(bank).EntityPaused
            && Transform(ent).MapUid == bank.Comp.Map && Transform(ent).GridUid is { } grid
            && TryComp<MapGridComponent>(grid, out var mapGrid)
            && !_map.GetTileRef(grid, mapGrid, Transform(ent).Coordinates).Tile.IsEmpty
            && Vector2.DistanceSquared(_transform.GetWorldPosition(ent), _transform.GetWorldPosition(bank)) <= bank.Comp.DeploymentRange * bank.Comp.DeploymentRange
            && _sites.CanPlaceUtility(bank, _transform.GetWorldPosition(ent));
    }

    public bool IsPowered(Entity<CMUFobEquipmentComponent> ent)
        => ent.Comp.Deployed && Transform(ent).Anchored && AtSite(ent, out var bank)
           && _reserve.IsActive(bank) && _controls.HasOperationalControl(bank);

    public bool CanSense(EntityUid network, EntityUid target)
        => TryGetSpottingSensor(network, target, out _);

    public bool IsUnderRoof(EntityUid target)
    {
        var transform = Transform(target);
        return transform.GridUid is { } grid && TryComp<MapGridComponent>(grid, out var mapGrid)
            && TryComp<RoofComponent>(grid, out var roof)
            && _roof.IsRooved((grid, mapGrid, roof), _map.GetTileRef(grid, mapGrid, transform.Coordinates).GridIndices);
    }

    public bool TryGetSpottingSensor(EntityUid network, EntityUid target, out EntityUid sensor)
    {
        sensor = default;
        if (IsUnderRoof(target)) return false;
        var sensors = EntityQueryEnumerator<CMUFobEquipmentComponent>();
        while (sensors.MoveNext(out var uid, out var comp))
        {
            if (comp.Kind == CMUFobEquipmentKind.Sensor && comp.Network == network && IsPowered((uid, comp))
                && Transform(uid).MapUid == Transform(target).MapUid
                && _interaction.InRangeUnobstructed(uid, target, comp.SensorRange,
                    CollisionGroup.Impassable | CollisionGroup.BulletImpassable))
            {
                sensor = uid;
                return true;
            }
        }
        return false;
    }

    public void GetSensorCandidates(EntityUid network, HashSet<Entity<MobStateComponent>> candidates)
    {
        var sensors = EntityQueryEnumerator<CMUFobEquipmentComponent>();
        while (sensors.MoveNext(out var uid, out var comp))
        {
            if (comp.Kind == CMUFobEquipmentKind.Sensor && comp.Network == network && IsPowered((uid, comp)))
                _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(uid), comp.SensorRange, candidates);
        }
    }

    /// <summary>Once launched, guidance uses distance and roofing, not sight or handoff to another sensor.</summary>
    public bool CanTrack(EntityUid network, EntityUid sensor, EntityUid target, float range)
        => !TerminatingOrDeleted(sensor) && TryComp<CMUFobEquipmentComponent>(sensor, out var comp)
           && comp.Kind == CMUFobEquipmentKind.Sensor && comp.Network == network && IsPowered((sensor, comp))
           && Transform(sensor).MapUid == Transform(target).MapUid && !IsUnderRoof(target)
           && Vector2.DistanceSquared(_transform.GetWorldPosition(sensor), _transform.GetWorldPosition(target)) <= range * range;

    private bool CanWork(Entity<CMUFobEquipmentComponent> ent, EntityUid user, EntityUid? tool, CMUFobEquipmentDoAfterEvent ev)
    {
        if (!AtSite(ent, out var bank) || !_controls.CanOperate(ent, user, bank.Comp.Faction)) return false;
        if (ev.Generator)
            return ent.Comp.Kind == CMUFobEquipmentKind.Generator && ent.Comp.Deployed && !ent.Comp.Spent
                && !bank.Comp.BackupUsed && _reserve.GetRemaining(bank) < bank.Comp.Capacity;
        if (ev.Deploy == ent.Comp.Deployed || !ev.Deploy && bank.Comp.State is CMUFobReserveState.Starting or CMUFobReserveState.Active) return false;
        if (ev.Deploy)
        {
            // Anchoring snaps to the tile center. Testing the dropped item's unsnapped position
            // incorrectly rejects the next fence section beside a deployed light or fence.
            var grid = Transform(ent).GridUid!.Value;
            var mapGrid = Comp<MapGridComponent>(grid);
            var tile = _map.GetTileRef(grid, mapGrid, Transform(ent).Coordinates);
            var center = _map.GridTileToWorld(grid, mapGrid, tile.GridIndices);
            if (Vector2.DistanceSquared(center.Position, _transform.GetWorldPosition(bank)) > bank.Comp.DeploymentRange * bank.Comp.DeploymentRange) return false;
            if (!_sites.CanPlaceUtility(bank, center.Position)
                || !_construction.CanBuildAt(Transform(ent).Coordinates, Name(ent), out _, anchoring: true)) return false;
            _obstacles.Clear();
            _lookup.GetEntitiesInRange(center, 0.35f, _obstacles);
            foreach (var obstacle in _obstacles)
            {
                if (obstacle.Owner != ent.Owner && obstacle.Comp.CanCollide
                    && TryComp<FixturesComponent>(obstacle, out var fixtures)
                    && fixtures.Fixtures.Values.Any(fixture => fixture.Hard)) return false;
            }
        }
        return tool is { } held && _hands.TryGetActiveItem(user, out var active) && active == held
            && _tools.HasQuality(held, ent.Comp.Kind == CMUFobEquipmentKind.Fence && !ev.Deploy ? Cutters : Wrench);
    }

    public bool TryStartWork(Entity<CMUFobEquipmentComponent> ent, EntityUid user, EntityUid? tool, bool generator = false)
    {
        var ev = new CMUFobEquipmentDoAfterEvent { Deploy = !ent.Comp.Deployed, Generator = generator };
        if (_doAfter.IsRunning(ent.Comp.Work) || !CanWork(ent, user, tool, ev)) return false;
        ent.Comp.Charging = generator;
        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(generator ? 10 : 5), ev, ent, ent, tool)
        {
            BreakOnMove = true, BreakOnDamage = true, BreakOnHandChange = true, NeedHand = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
        }, out ent.Comp.Work);
        if (!started) ent.Comp.Charging = false;
        UpdateVisuals(ent);
        return started;
    }

    private void OnTool(Entity<CMUFobEquipmentComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_reserve.Enabled) return;
        if (!_tools.HasQuality(args.Used, Wrench) && !_tools.HasQuality(args.Used, Cutters)) return;
        args.Handled = true;
        if (!TryStartWork(ent, args.User, args.Used))
            _popup.PopupEntity(Loc.GetString("cmu-fob-equipment-work-denied"), ent, args.User);
    }

    private void OnActivate(Entity<CMUFobEquipmentComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || ent.Comp.Kind != CMUFobEquipmentKind.Generator || !_reserve.Enabled) return;
        args.Handled = true;
        if (!TryStartWork(ent, args.User, null, true))
            _popup.PopupEntity(Loc.GetString("cmu-fob-generator-denied"), ent, args.User);
    }

    private void OnAttempt(Entity<CMUFobEquipmentComponent> ent, ref DoAfterAttemptEvent<CMUFobEquipmentDoAfterEvent> args)
    {
        if (!CanWork(ent, args.DoAfter.Args.User, args.DoAfter.Args.Used, args.Event)) args.Cancel();
    }

    private void OnFinished(Entity<CMUFobEquipmentComponent> ent, ref CMUFobEquipmentDoAfterEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;
        ent.Comp.Work = null;
        ent.Comp.Charging = false;
        if (args.Cancelled || !CanWork(ent, args.User, args.Used, args) || !AtSite(ent, out var bank))
        {
            UpdateVisuals(ent);
            return;
        }
        if (args.Generator)
        {
            if (!_reserve.TryUseBackup(bank)) return;
            ent.Comp.Spent = true;
            _effects.Play(ent, CMUFobPresentationCue.Charge);
            _popup.PopupEntity(Loc.GetString("cmu-fob-generator-used"), ent, PopupType.MediumCaution);
        }
        else
        {
            if (args.Deploy && TryComp<StackComponent>(ent, out var stack))
            {
                // A bundle remains portable. Consume one section only after successful placement;
                // the deployed entity retains any damage rather than laundering it through a stack.
                if (_stack.GetCount((ent, stack)) < 1) return;
                var section = SpawnAtPosition("CMUFobFence", Transform(ent).Coordinates);
                if (!_transform.AnchorEntity(section) || !_stack.TryUse((ent, stack), 1))
                {
                    QueueDel(section);
                    return;
                }
                if (TryComp<DamageableComponent>(ent, out var damage))
                    _damage.TryChangeDamage(section, _damage.GetAllDamage((ent, damage)), ignoreResistances: true);
                var equipment = Comp<CMUFobEquipmentComponent>(section);
                equipment.Deployed = true;
                equipment.Network = bank;
                if (TryComp<FixturesComponent>(section, out var sectionFixtures))
                    foreach (var fixture in sectionFixtures.Fixtures.Values)
                        _physics.SetHard(section, fixture, true);
                EnsureComp<CMUFobInstructionsComponent>(section);
                UpdateVisuals((section, equipment));
                _effects.Play(section, CMUFobPresentationCue.Deploy);
                return;
            }
            if (args.Deploy && !_transform.AnchorEntity(ent)) return;
            if (!args.Deploy) _transform.Unanchor(ent);
            ent.Comp.Deployed = args.Deploy;
            ent.Comp.Network = args.Deploy ? bank.Owner : null;
            if (TryComp<FixturesComponent>(ent, out var fixtures))
                foreach (var fixture in fixtures.Fixtures.Values)
                    _physics.SetHard(ent, fixture, ent.Comp.Kind != CMUFobEquipmentKind.Sensor && args.Deploy);
            EnsureComp<CMUFobInstructionsComponent>(ent);
            _effects.Play(ent, args.Deploy ? CMUFobPresentationCue.Deploy : CMUFobPresentationCue.Pack);
        }
        UpdateVisuals(ent);
    }

    private void OnPickup(Entity<CMUFobEquipmentComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (ent.Comp.Deployed) args.Cancel();
    }

    private void OnDamage(Entity<CMUFobEquipmentComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased) return;
        _doAfter.Cancel(ent.Comp.Work);
        if (args.Origin is { } attacker && _interaction.InRangeUnobstructed(ent.Owner, attacker, 1.5f)) Shock(ent, attacker);
    }

    private void OnCollide(Entity<CMUFobEquipmentComponent> ent, ref StartCollideEvent args) => Shock(ent, args.OtherEntity);

    private void Shock(Entity<CMUFobEquipmentComponent> ent, EntityUid target)
    {
        if (ent.Comp.Kind != CMUFobEquipmentKind.Fence || !IsPowered(ent) || _timing.CurTime < ent.Comp.NextShock
            || !HasComp<XenoComponent>(target) || !TryComp<MobStateComponent>(target, out var mob) || mob.CurrentState == MobState.Dead) return;
        ent.Comp.NextShock = _timing.CurTime + TimeSpan.FromSeconds(2);
        _damage.TryChangeDamage(target, ent.Comp.ShockDamage, origin: ent);
        _effects.Play(ent, CMUFobPresentationCue.Shock);
    }

    private void OnShutdown(Entity<CMUFobEquipmentComponent> ent, ref ComponentShutdown args) => _doAfter.Cancel(ent.Comp.Work);

    private void OnExamined(Entity<CMUFobEquipmentComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(!_reserve.Enabled ? "cmu-fob-disabled" : ent.Comp.Spent ? "cmu-fob-generator-spent"
            : ent.Comp.Deployed ? "cmu-fob-equipment-deployed" : "cmu-fob-equipment-stowed"));
    }

    private void UpdateVisuals(Entity<CMUFobEquipmentComponent> ent)
    {
        ent.Comp.Powered = IsPowered(ent);
        if (ent.Comp.Kind == CMUFobEquipmentKind.Generator)
            _ambient.SetAmbience(ent, _reserve.Enabled && ent.Comp.Deployed && ent.Comp.Charging && _doAfter.IsRunning(ent.Comp.Work));
        var state = ent.Comp.Spent ? "backup_generator_depleted" : !ent.Comp.Deployed ? ent.Comp.StowedState
            : ent.Comp.Powered ? ent.Comp.OnState : ent.Comp.OffState;
        if (ent.Comp.Kind == CMUFobEquipmentKind.Generator && !ent.Comp.Spent)
            state = ent.Comp.Deployed && ent.Comp.Charging && _doAfter.IsRunning(ent.Comp.Work) ? ent.Comp.OnState : ent.Comp.OffState;
        if (ent.Comp.Kind == CMUFobEquipmentKind.Fence && ent.Comp.Deployed)
        {
            var connections = 0;
            var fences = EntityQueryEnumerator<CMUFobEquipmentComponent, TransformComponent>();
            while (fences.MoveNext(out var other, out var fence, out var xf))
            {
                if (other == ent.Owner || !fence.Deployed || fence.Kind != CMUFobEquipmentKind.Fence || xf.GridUid != Transform(ent).GridUid) continue;
                var delta = _transform.GetWorldPosition(other) - _transform.GetWorldPosition(ent);
                if (Vector2.DistanceSquared(delta, Vector2.UnitY) < 0.1f) connections |= 1;
                if (Vector2.DistanceSquared(delta, -Vector2.UnitY) < 0.1f) connections |= 2;
                if (Vector2.DistanceSquared(delta, Vector2.UnitX) < 0.1f) connections |= 4;
                if (Vector2.DistanceSquared(delta, -Vector2.UnitX) < 0.1f) connections |= 8;
            }
            state = $"fence{connections}{(ent.Comp.Powered ? "" : "_off")}";
        }
        _appearance.SetData(ent, CMUFobEquipmentVisuals.State, state);
        _effects.SetStatus(ent, ent.Comp.Spent ? CMUFobPresentationStatus.Spent
            : _doAfter.IsRunning(ent.Comp.Work) ? CMUFobPresentationStatus.Working
            : !ent.Comp.Deployed ? CMUFobPresentationStatus.Off
            : ent.Comp.Powered ? CMUFobPresentationStatus.Active : CMUFobPresentationStatus.Off);
        if (ent.Comp.Kind == CMUFobEquipmentKind.Illuminator) _lights.SetEnabled(ent, ent.Comp.Powered);
    }

    public override void Update(float frameTime)
    {
        if (!_reserve.Enabled || _timing.CurTime < _nextUpdate) return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(0.25);
        var query = EntityQueryEnumerator<CMUFobEquipmentComponent>();
        while (query.MoveNext(out var uid, out var comp)) UpdateVisuals((uid, comp));
    }
}
