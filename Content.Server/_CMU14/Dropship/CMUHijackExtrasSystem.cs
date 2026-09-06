using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Station.Components;
using Content.Server.Stunnable;
using Content.Server._RMC14.Xenonids.JoinXeno;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.AU14;
using Content.Shared.Coordinates;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.StatusEffect;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Dropship;

/// <summary>
///     Hijack song, crash camera shake and ship-wide stun, and the burrowed larva surge for
///     presets that don't run the classic <see cref="CMDistressSignalRuleComponent"/> rule.
///     That rule provides these itself, so this system must stay off while it is active.
/// </summary>
public sealed class CMUHijackExtrasSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private GhostSystem _ghost = default!;
    [Dependency] private LarvaQueueSystem _larvaQueue = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private RMCCameraShakeSystem _rmcCameraShake = default!;
    [Dependency] private RMCPlanetSystem _rmcPlanet = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private StunSystem _stuns = default!;

    private static readonly TimeSpan HijackStunTime = TimeSpan.FromSeconds(5);

    // Mirrors the CMDistressSignalRuleComponent.HijackSong default
    private static readonly SoundSpecifier HijackSong =
        new SoundCollectionSpecifier("RMCHijack", AudioParams.Default.WithVolume(-8));

    private bool _hijackSongPlayed;
    private float _hijackShipWeight;
    private int _hijackMinBurrowed;

    public override void Initialize()
    {
        SubscribeLocalEvent<DropshipHijackStartEvent>(OnDropshipHijackStart);
        SubscribeLocalEvent<DropshipHijackLandedEvent>(OnDropshipHijackLanded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        Subs.CVar(_config, RMCCVars.RMCHijackShipWeight, v => _hijackShipWeight = v, true);
        Subs.CVar(_config, RMCCVars.RMCMinimumHijackBurrowed, v => _hijackMinBurrowed = v, true);
    }

    private void OnDropshipHijackStart(ref DropshipHijackStartEvent ev)
    {
        if (ev.HijackerType is DropshipHijackerType.Human or DropshipHijackerType.Other
            || HasActiveDistressRule())
            return;

        // Classic rule deletes planet-bound xenos here; mirror it so stranded neomorphs can't
        // linger on the planet after the hijack endgame moves to the ship
        var xenoAmount = 0;
        var xenos = EntityQueryEnumerator<XenoComponent, MobStateComponent, TransformComponent>();
        while (xenos.MoveNext(out var xeno, out var comp, out _, out var xform))
        {
            if (_mobState.IsDead(xeno))
                continue;

            if (xform.ParentUid != ev.Dropship && _rmcPlanet.IsOnPlanet(xeno.ToCoordinates()))
            {
                if (TryComp(xeno, out ActorComponent? actor))
                {
                    var session = actor.PlayerSession;
                    Entity<MindComponent> mind;

                    if (_mind.TryGetMind(session, out var mindId, out var mindComp))
                        mind = (mindId, mindComp);
                    else
                        mind = _mind.CreateMind(session.UserId);

                    var ghost = _ghost.SpawnGhost((mind.Owner, mind.Comp), xeno);
                    if (ghost != null)
                        EnsureComp<JoinXenoCooldownIgnoreComponent>(ghost.Value);

                    var origin = _transform.GetMoverCoordinates(xeno);
                    _popup.PopupCoordinates(
                        Loc.GetString("rmc-xeno-hibernation"),
                        origin,
                        Filter.SinglePlayer(session),
                        true,
                        PopupType.MediumXeno);

                    if (comp.CountedInSlots && _hive.GetHive(xeno) is { } hive)
                        _larvaQueue.AddToLarvaQueueFront(hive, session.UserId);
                }

                QueueDel(xeno);
                continue;
            }

            xenoAmount++;
        }

        if (ev.HijackerType == DropshipHijackerType.Pathogen)
            return;

        var shipMapIds = new HashSet<MapId>();
        var almayerQuery = EntityQueryEnumerator<AlmayerComponent, TransformComponent>();
        while (almayerQuery.MoveNext(out _, out var xform))
            AddShipMapAndConnectedZLevelMapIds(shipMapIds, xform.MapUid);

        var shipQuery = EntityQueryEnumerator<ShipFactionComponent, TransformComponent>();
        while (shipQuery.MoveNext(out _, out var xform))
            AddShipMapAndConnectedZLevelMapIds(shipMapIds, xform.MapUid);

        // Surge: hosts on the target ship(s) minus the xenos already aboard, floored at the CVar minimum
        float totalHostWeights = 0;
        var surgeQuery =
            EntityQueryEnumerator<MarineComponent, MobStateComponent, InfectableComponent, TransformComponent>();
        while (surgeQuery.MoveNext(out var marine, out _, out _, out _, out var xform))
        {
            if (_mobState.IsDead(marine) || !shipMapIds.Contains(xform.MapID))
                continue;

            if (!TryComp<MindContainerComponent>(marine, out var mindContainer) ||
                !TryComp<MindComponent>(mindContainer.Mind, out var mind))
                continue;

            foreach (var roleId in mind.MindRoles)
            {
                if (!TryComp<MindRoleComponent>(roleId, out var mindRole) ||
                    mindRole.JobPrototype == null ||
                    !_prototypes.TryIndex(mindRole.JobPrototype, out var proto))
                {
                    continue;
                }

                totalHostWeights += proto.RoleWeight;
            }
        }

        var surgeAmount =
            Math.Max((int) Math.Ceiling(totalHostWeights * _hijackShipWeight) - xenoAmount, _hijackMinBurrowed);

        var hiveQuery = EntityQueryEnumerator<HiveComponent>();
        while (hiveQuery.MoveNext(out var hive, out var hiveComp))
        {
            _hive.ResetHiveCoreCooldown((hive, hiveComp));
            var surge = EnsureComp<HijackBurrowedSurgeComponent>(hive);
            surge.PooledLarva = surgeAmount;
            Dirty(hive, surge);
        }
    }

    private void OnDropshipHijackLanded(ref DropshipHijackLandedEvent ev)
    {
        if (HasActiveDistressRule())
            return;

        if (!_hijackSongPlayed)
        {
            _hijackSongPlayed = true;
            var song = _audio.PlayGlobal(HijackSong, Filter.Broadcast(), true);
            if (song?.Entity is { } songEnt)
                EnsureComp<RMCHijackSongComponent>(songEnt);
        }

        // Only shake/stun for crash landings (xeno hijack), not normal landings (human hijack)
        if (ev.IsHumanHijack)
            return;

        var didCameraShake = false;

        var targetMaps = new HashSet<MapId>();
        AddShipMapAndConnectedZLevelMapIds(targetMaps, ev.Map);

        var gridQuery = EntityQueryEnumerator<BecomesStationComponent, MapGridComponent, TransformComponent>();
        while (gridQuery.MoveNext(out var uid, out _, out _, out var xform))
        {
            var map = _transform.GetMapId(uid);
            if (!targetMaps.Contains(map))
                continue;

            if (!didCameraShake)
            {
                _rmcCameraShake.ShakeCamera(Filter.BroadcastMap(map), 10, 2);
                didCameraShake = true;
            }

            StunAllOnShip(xform);
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _hijackSongPlayed = false;
    }

    private bool HasActiveDistressRule()
    {
        var query = EntityQueryEnumerator<ActiveGameRuleComponent, CMDistressSignalRuleComponent>();
        return query.MoveNext(out _, out _);
    }

    private void AddShipMapAndConnectedZLevelMapIds(ICollection<MapId> shipMaps, EntityUid? mapUid)
    {
        if (mapUid is not { } map)
            return;

        foreach (var connectedMap in _zLevels.GetAllNetworkMaps(map))
            AddMapId(shipMaps, connectedMap);
    }

    private void AddMapId(ICollection<MapId> shipMaps, EntityUid map)
    {
        var mapId = _transform.GetMapId(map);
        if (!shipMaps.Contains(mapId))
            shipMaps.Add(mapId);
    }

    /// <summary>
    ///     Stuns all non-xeno occupants on a ship grid.
    /// </summary>
    private void StunAllOnShip(TransformComponent xform)
    {
        // Get enumeration exceptions from people dropping things if we just paralyze as we go
        var toKnock = new ValueList<EntityUid>();
        GetOccupantsOnShip(xform, ref toKnock);

        foreach (var child in toKnock)
        {
            if (!TryComp<StatusEffectsComponent>(child, out var status))
                continue;

            _stuns.TryParalyze(child, HijackStunTime, true, status);
        }
    }

    /// <summary>
    ///     Gets all non-xeno entities on a ship grid (for crash stun).
    /// </summary>
    private void GetOccupantsOnShip(TransformComponent xform, ref ValueList<EntityUid> reference)
    {
        // Not recursive because probably not necessary? If we need it to be that's why this method is separate.
        var childEnumerator = xform.ChildEnumerator;
        while (childEnumerator.MoveNext(out var child))
        {
            if (HasComp<XenoComponent>(child))
                continue;

            reference.Add(child);
        }
    }
}
