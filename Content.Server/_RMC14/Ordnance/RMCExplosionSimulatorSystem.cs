using System.Numerics;
using System.Linq;
using Content.Server._RMC14.Ghost;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Humanoid.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Ordnance;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Audio;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Ordnance;

/// <summary>
///     Runs the large console-based explosion simulator by sampling a finished ordnance item
///     and replaying the predicted blast package inside a disposable chamber map.
/// </summary>
public sealed class RMCExplosionSimulatorSystem : EntitySystem
{
    private static readonly SoundPathSpecifier BeepSound = new("/Audio/Machines/twobeep.ogg");
    private const string SampleSlotId = "beakerSlot";

    private sealed record TargetSpawnProfile(Vector2 Position, string PrototypeId, bool RandomHumanoid = false);

    private static readonly Dictionary<RMCExplosionSimulatorTarget, TargetSpawnProfile[]> TargetProfiles = new()
    {
        [RMCExplosionSimulatorTarget.Marines] =
        [
            new(new Vector2(2, 1), "RMCRifleman", true),
            new(new Vector2(2, -1), "RMCSmartGunOperator", true),
            new(new Vector2(3, 2), "RMCHospitalCorpsman", true),
            new(new Vector2(3, 0), "RMCCombatTech", true),
            new(new Vector2(3, -2), "RMCRifleman", true),
            new(new Vector2(4, 1), "RMCSmartGunOperator", true),
            new(new Vector2(4, -1), "RMCHospitalCorpsman", true),
        ],
        [RMCExplosionSimulatorTarget.SpecialForces] =
        [
            new(new Vector2(2, 0), "RMCMarineRaiderLeader", true),
            new(new Vector2(2, 1), "RMCMarineRaider", true),
            new(new Vector2(2, -1), "RMCMarineRaider", true),
            new(new Vector2(3, 0), "RMCMarineRaider", true),
            new(new Vector2(3, 1), "RMCMarineRaider", true),
        ],
        [RMCExplosionSimulatorTarget.Xenomorphs] =
        [
            new(new Vector2(1, 0), "CMXenoRunner"),
            new(new Vector2(1, 1), "CMXenoSentinel"),
            new(new Vector2(1, -1), "CMXenoWarrior"),
            new(new Vector2(2, 0), "CMXenoSpitter"),
            new(new Vector2(2, 1), "CMXenoDefender"),
            new(new Vector2(2, -1), "CMXenoLurker"),
            new(new Vector2(3, 0), "CMXenoPraetorian"),
            new(new Vector2(3, 1), "CMXenoRavager"),
            new(new Vector2(3, -1), "CMXenoRunner"),
        ],
    };

    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly EyeSystem _eye = default!;
    [Dependency] private readonly SharedRMCFlammableSystem _flammable = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RandomHumanoidSystem _randomHumanoid = default!;
    [Dependency] private readonly RMCOrdnanceSampleResolverSystem _resolver = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly ViewSubscriberSystem _viewSubscriber = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCExplosionSimulatorWatchingComponent, MoveInputEvent>(OnWatchingMoveInput);

        Subs.BuiEvents<RMCExplosionSimulatorComponent>(RMCExplosionSimulatorUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnBuiOpened);
            subs.Event<BoundUIClosedEvent>(OnBuiClosed);
            subs.Event<RMCExplosionSimulatorSimulateMsg>(OnSimulateMsg);
            subs.Event<RMCExplosionSimulatorTargetMsg>(OnTargetMsg);
            subs.Event<RMCExplosionSimulatorReplayMsg>(OnReplayMsg);
        });
    }

    private void OnBuiOpened(Entity<RMCExplosionSimulatorComponent> ent, ref BoundUIOpenedEvent args)
    {
        ToggleViewer(ent.Comp.ChamberCameraEnt, args.Actor, true);
        UpdateUiState(ent);
    }

    private void OnBuiClosed(Entity<RMCExplosionSimulatorComponent> ent, ref BoundUIClosedEvent args)
    {
        ToggleViewer(ent.Comp.ChamberCameraEnt, args.Actor, false);
    }

    private void OnSimulateMsg(Entity<RMCExplosionSimulatorComponent> ent, ref RMCExplosionSimulatorSimulateMsg args)
    {
        if (ent.Comp.IsProcessing)
        {
            UpdateUiState(ent);
            return;
        }

        if (!_itemSlots.TryGetSlot(ent, SampleSlotId, out var itemSlot) ||
            itemSlot.Item is not { } sampleUid ||
            !RunSimulation(ent, sampleUid))
        {
            UpdateUiState(ent);
            return;
        }

        ent.Comp.IsProcessing = true;
        ent.Comp.SimulationReady = false;
        ent.Comp.ProcessingEnd = _timing.CurTime + RMCExplosionSimulatorComponent.ProcessingDuration;
        ent.Comp.ProcessingActor = args.Actor;

        Dirty(ent);
        UpdateUiState(ent);
    }

    private void OnTargetMsg(Entity<RMCExplosionSimulatorComponent> ent, ref RMCExplosionSimulatorTargetMsg args)
    {
        ent.Comp.SelectedTarget = args.Target;
        Dirty(ent);
        UpdateUiState(ent);
    }

    private void OnReplayMsg(Entity<RMCExplosionSimulatorComponent> ent, ref RMCExplosionSimulatorReplayMsg args)
    {
        if (!ent.Comp.SimulationReady)
            return;

        CreateChamberAndReplay(ent, args.Actor);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<RMCExplosionSimulatorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.IsProcessing && _timing.CurTime >= comp.ProcessingEnd)
            {
                comp.IsProcessing = false;
                comp.SimulationReady = true;
                Dirty(uid, comp);

                _audio.PlayPvs(BeepSound, uid);

                if (comp.ProcessingActor != EntityUid.Invalid)
                    _popup.PopupEntity("Simulation complete.", uid, comp.ProcessingActor);

                UpdateUiState((uid, comp));
            }

            if (!comp.PendingExplosion || _timing.CurTime < comp.ExplosionAt)
                continue;

            comp.PendingExplosion = false;
            Dirty(uid, comp);

            if (!EntityManager.EntityExists(comp.ChamberCameraEnt))
                continue;

            var xform = Transform(comp.ChamberCameraEnt);
            var center = new MapCoordinates(xform.WorldPosition, xform.MapID);
            _explosion.QueueExplosion(
                center,
                "RMC",
                comp.PendingTotalIntensity,
                comp.PendingIntensitySlope,
                comp.PendingMaxIntensity,
                null,
                addLog: false);

            if (!comp.PendingFire)
                continue;

            var tile = xform.Coordinates.SnapToGrid(EntityManager, _mapManager);
            _flammable.SpawnFireDiamond(
                comp.PendingFireEntity,
                tile,
                (int) comp.PendingFireRadius,
                (int) comp.PendingFireIntensity,
                (int) comp.PendingFireDuration);
        }
    }

    private bool RunSimulation(Entity<RMCExplosionSimulatorComponent> simEnt, EntityUid sampleUid)
    {
        if (!_resolver.TryResolveSample(sampleUid, out var sample))
            return false;

        ApplyEstimate(simEnt.Comp, sample.Estimate);
        Dirty(simEnt);
        return true;
    }

    private void CreateChamberAndReplay(Entity<RMCExplosionSimulatorComponent> ent, EntityUid actor)
    {
        ToggleViewer(ent.Comp.ChamberCameraEnt, actor, false);

        if (ent.Comp.ChamberMapEnt != EntityUid.Invalid && EntityManager.EntityExists(ent.Comp.ChamberMapEnt))
            QueueDel(ent.Comp.ChamberMapEnt);

        var options = DeserializationOptions.Default with { InitializeMaps = true };
        if (!_mapLoader.TryLoadMap(ent.Comp.ChamberMap, out var map, out var grids, options) ||
            map is not { } chamberMap ||
            grids == null ||
            grids.Count == 0)
        {
            UpdateUiState(ent);
            return;
        }

        var chamberGrid = grids.First();
        ent.Comp.ChamberMapEnt = chamberMap.Owner;
        SpawnTargetEntities(ent.Comp.SelectedTarget, chamberGrid.Owner);

        var camera = Spawn("RMCDemolitionsCamera", new EntityCoordinates(chamberGrid.Owner, Vector2.Zero));
        ent.Comp.ChamberCameraEnt = camera;
        ent.Comp.ChamberCamera = GetNetEntity(camera);

        ToggleViewer(camera, actor, true);

        _eye.SetTarget(actor, camera);
        _eye.SetDrawLight(actor, false);
        EnsureComp<RMCExplosionSimulatorWatchingComponent>(actor).Watching = camera;

        ent.Comp.PendingExplosion = true;
        ent.Comp.ExplosionAt = _timing.CurTime + TimeSpan.FromSeconds(2);

        Dirty(ent);
        UpdateUiState(ent);
    }

    private void SpawnTargetEntities(RMCExplosionSimulatorTarget target, EntityUid gridEnt)
    {
        if (!TargetProfiles.TryGetValue(target, out var profiles))
            return;

        foreach (var profile in profiles)
        {
            var coordinates = new EntityCoordinates(gridEnt, profile.Position);
            var spawned = profile.RandomHumanoid
                ? _randomHumanoid.SpawnRandomHumanoid(profile.PrototypeId, coordinates, profile.PrototypeId)
                : Spawn(profile.PrototypeId, coordinates);

            StripGhostTakeoverComponents(spawned);
        }
    }

    /// <summary>
    ///     Simulator chamber mobs should never become available to ghosts while the replay is open.
    /// </summary>
    private void StripGhostTakeoverComponents(EntityUid uid)
    {
        RemComp<GhostRoleComponent>(uid);
        RemComp<GhostTakeoverAvailableComponent>(uid);
        RemComp<GhostRolePreventCryoSleepComponent>(uid);
        RemComp<GhostRoleApplySpecialComponent>(uid);
    }

    private void UpdateUiState(Entity<RMCExplosionSimulatorComponent> ent)
    {
        var hasSample = _itemSlots.TryGetSlot(ent, SampleSlotId, out var slot) && slot != null && slot.Item.HasValue;
        var sampleName = hasSample && slot != null && slot.Item is { } sampleUid
            ? MetaData(sampleUid).EntityName
            : string.Empty;
        var secondsLeft = ent.Comp.IsProcessing
            ? Math.Max(0, (int) Math.Ceiling((ent.Comp.ProcessingEnd - _timing.CurTime).TotalSeconds))
            : 0;

        var state = new RMCExplosionSimulatorBuiState(
            hasSample,
            sampleName,
            ent.Comp.SelectedTarget,
            ent.Comp.IsProcessing,
            secondsLeft,
            ent.Comp.SimulationReady,
            ent.Comp.LastHasExplosion,
            ent.Comp.LastTotalIntensity,
            ent.Comp.LastIntensitySlope,
            ent.Comp.LastMaxIntensity,
            ent.Comp.LastHasFire,
            ent.Comp.LastFireIntensity,
            ent.Comp.LastFireRadius,
            ent.Comp.LastFireDuration,
            ent.Comp.ChamberCamera);

        _ui.SetUiState(ent.Owner, RMCExplosionSimulatorUiKey.Key, state);
    }

    /// <summary>
    ///     Any movement input exits the detached camera view and returns the actor to their normal eye target.
    /// </summary>
    private void OnWatchingMoveInput(Entity<RMCExplosionSimulatorWatchingComponent> ent, ref MoveInputEvent args)
    {
        if (!args.HasDirectionalMovement || !HasComp<ActorComponent>(ent))
            return;

        _eye.SetTarget(ent.Owner, null);
        _eye.SetDrawLight(ent.Owner, true);

        if (ent.Comp.Watching is { } watching)
            ToggleViewer(watching, ent.Owner, false);

        ent.Comp.Watching = EntityUid.Invalid;
    }

    private static void ApplyEstimate(RMCExplosionSimulatorComponent comp, in RMCOrdnanceYieldEstimate estimate)
    {
        comp.LastHasExplosion = estimate.HasExplosion;
        comp.LastTotalIntensity = estimate.TotalIntensity;
        comp.LastIntensitySlope = estimate.IntensitySlope;
        comp.LastMaxIntensity = estimate.MaxIntensity;
        comp.LastHasFire = estimate.HasFire;
        comp.LastFireIntensity = estimate.FireIntensity;
        comp.LastFireRadius = estimate.FireRadius;
        comp.LastFireDuration = estimate.FireDuration;
        comp.PendingTotalIntensity = estimate.TotalIntensity;
        comp.PendingIntensitySlope = estimate.IntensitySlope;
        comp.PendingMaxIntensity = estimate.MaxIntensity;
        comp.PendingFire = estimate.HasFire;
        comp.PendingFireIntensity = estimate.FireIntensity;
        comp.PendingFireRadius = estimate.FireRadius;
        comp.PendingFireDuration = estimate.FireDuration;
        comp.PendingFireEntity = estimate.FireEntity;
    }

    private void ToggleViewer(EntityUid camera, EntityUid actor, bool subscribed)
    {
        if (camera == EntityUid.Invalid || !_playerManager.TryGetSessionByEntity(actor, out var session))
            return;

        if (subscribed)
            _viewSubscriber.AddViewSubscriber(camera, session);
        else
            _viewSubscriber.RemoveViewSubscriber(camera, session);
    }
}
