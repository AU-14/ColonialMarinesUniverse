using Content.Server.GameTicking;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.ZLevels.Core;

/// <summary>
/// Owns construction and initialization ordering for complete Z-networks.
/// </summary>
public sealed partial class CMUZNetworkLifecycleSystem : EntitySystem
{
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    private readonly Dictionary<EntityUid, NetworkConstructionTransaction> _pendingRoundNetworks = new();
    private int _topologyAttachmentDepth;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PostGameMapLoad>(OnGameMapLoad);
        SubscribeLocalEvent<CMUZLevelMapComponent, MapInitEvent>(OnMapInit);
    }

    private void OnGameMapLoad(PostGameMapLoad ev)
    {
        if (ev.GameMap.MapsAbove.Count == 0 &&
            ev.GameMap.MapsBelow.Count == 0)
        {
            return;
        }

        var baseLevel = _map.GetMap(ev.Map);
        if (TryCreateRoundNetwork(ev.GameMap, baseLevel, out var error))
            return;

        throw new InvalidOperationException(error);
    }

    private void OnMapInit(Entity<CMUZLevelMapComponent> ent, ref MapInitEvent args)
    {
        if (_topologyAttachmentDepth > 0 ||
            ent.Comp.Depth != 0 ||
            !_zLevels.TryGetZNetwork(ent.Owner, out var network))
        {
            return;
        }

        if (_pendingRoundNetworks.TryGetValue(network.Value.Owner, out var transaction))
        {
            if (TryInitializeRoundNetwork(network.Value, transaction, out var error))
                return;

            throw new InvalidOperationException(error);
        }

        InitializeAuxiliaryLevels(network.Value);
    }

    /// <summary>
    /// Builds a complete Z-network around a base level owned by round orchestration.
    /// </summary>
    public bool TryCreateRoundNetwork(
        GameMapPrototype gameMap,
        EntityUid baseLevel,
        out string error)
    {
        if (!TryLoadLevels(gameMap, baseLevel, null, out var levels, out error))
            return false;

        if (!TryCommitNetwork(
                levels,
                gameMap.ZLevelsComponentOverrides,
                $"Station z-Network: {gameMap.MapName}",
                gameMap.MapName,
                mappingNames: false,
                out var network,
                out var transaction,
                out error))
        {
            return false;
        }

        if (!_pendingRoundNetworks.TryAdd(network.Owner, transaction))
        {
            error = RollbackAfterFailure(
                transaction,
                "Failed to register pending Z-network initialization",
                new InvalidOperationException($"Z-network {network.Owner} already has pending initialization."));
            return false;
        }

        if (_map.IsInitialized(levels[0].Map.Comp.MapId) &&
            !TryInitializeRoundNetwork(network, transaction, out error))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Loads and owns every level in a Z-network intended for mapping.
    /// </summary>
    public bool TryCreateMappingNetwork(
        GameMapPrototype gameMap,
        out CMUZNetworkLoadResult result,
        out string error)
    {
        result = default;
        var options = new DeserializationOptions { StoreYamlUids = true };
        if (!TryLoadLevels(gameMap, null, options, out var levels, out error))
            return false;

        if (!TryCommitNetwork(
                levels,
                gameMap.ZLevelsComponentOverrides,
                $"Mapping zNetwork: {gameMap.MapName}",
                gameMap.MapName,
                mappingNames: true,
                out var network,
                out _,
                out error))
        {
            return false;
        }

        PublishNetworkPostCommit(network);

        var mapIds = new MapId[levels.Count];
        for (var i = 0; i < levels.Count; i++)
        {
            mapIds[i] = levels[i].Map.Comp.MapId;
        }

        result = new CMUZNetworkLoadResult(network, levels[0].Map, mapIds);
        return true;
    }

    /// <summary>
    /// Connects existing maps into a complete Z-network ordered from lowest to highest.
    /// </summary>
    public bool TryCombineLevels(
        IReadOnlyList<EntityUid> levelMaps,
        out Entity<CMUZLevelsNetworkComponent>? network,
        out string error)
    {
        network = null;
        error = string.Empty;

        if (levelMaps.Count < 2)
        {
            error = "At least two maps are required to form a Z-network.";
            return false;
        }

        var levels = new List<LoadedZLevel>(levelMaps.Count);
        var uniqueMaps = new HashSet<EntityUid>();
        for (var depth = 0; depth < levelMaps.Count; depth++)
        {
            var mapUid = levelMaps[depth];
            if (!TryComp<MapComponent>(mapUid, out var map) ||
                !_map.MapExists(map.MapId))
            {
                error = $"Z-level map {mapUid} at depth {depth} does not exist.";
                return false;
            }

            if (!uniqueMaps.Add(mapUid))
            {
                error = $"Z-level map {mapUid} appears more than once.";
                return false;
            }

            if (_zLevels.TryGetZNetwork(mapUid, out var existingNetwork))
            {
                error = $"Z-level map {mapUid} is already in network {existingNetwork.Value.Owner}.";
                return false;
            }

            levels.Add(new LoadedZLevel((mapUid, map), depth, false));
        }

        if (!TryCommitNetwork(
                levels,
                componentOverrides: null,
                networkName: null,
                levelName: null,
                mappingNames: false,
                out var createdNetwork,
                out _,
                out error))
        {
            return false;
        }

        PublishNetworkPostCommit(createdNetwork);

        network = createdNetwork;
        return true;
    }

    private bool TryLoadLevels(
        GameMapPrototype gameMap,
        EntityUid? suppliedBaseLevel,
        DeserializationOptions? options,
        out List<LoadedZLevel> levels,
        out string error)
    {
        levels = new List<LoadedZLevel>(
            1 + gameMap.MapsBelow.Count + gameMap.MapsAbove.Count);
        error = string.Empty;

        if (suppliedBaseLevel is { } baseLevel)
        {
            if (!TryComp<MapComponent>(baseLevel, out var baseMap))
            {
                error = $"Base Z-level {baseLevel} has no map component.";
                return false;
            }

            levels.Add(new LoadedZLevel((baseLevel, baseMap), 0, false));
        }
        else if (!TryLoadLevel(gameMap.MapPath, 0, options, levels, out error))
        {
            return false;
        }

        var depth = -gameMap.MapsBelow.Count;
        foreach (var path in gameMap.MapsBelow)
        {
            if (!TryLoadLevel(path, depth, options, levels, out error))
            {
                RollbackOwnedLevels(levels);
                return false;
            }

            depth++;
        }

        depth = 1;
        foreach (var path in gameMap.MapsAbove)
        {
            if (!TryLoadLevel(path, depth, options, levels, out error))
            {
                RollbackOwnedLevels(levels);
                return false;
            }

            depth++;
        }

        return true;
    }

    private bool TryLoadLevel(
        ResPath path,
        int depth,
        DeserializationOptions? options,
        List<LoadedZLevel> levels,
        out string error)
    {
        Entity<MapComponent>? map;
        var loaded = options is { } loadOptions
            ? _mapLoader.TryLoadMap(path, out map, out _, loadOptions)
            : _mapLoader.TryLoadMap(path, out map, out _);

        if (!loaded ||
            map is not { } loadedMap)
        {
            error = $"Failed to load Z-level at depth {depth}: {path}.";
            return false;
        }

        levels.Add(new LoadedZLevel(loadedMap, depth, true));
        error = string.Empty;
        return true;
    }

    private bool TryCommitNetwork(
        List<LoadedZLevel> levels,
        ComponentRegistry? componentOverrides,
        string? networkName,
        string? levelName,
        bool mappingNames,
        out Entity<CMUZLevelsNetworkComponent> network,
        out NetworkConstructionTransaction transaction,
        out string error)
    {
        network = default;
        transaction = new NetworkConstructionTransaction(levels);
        var maps = new Dictionary<EntityUid, int>(levels.Count);
        foreach (var level in levels)
        {
            if (!_map.MapExists(level.Map.Comp.MapId))
            {
                var validationError =
                    $"Z-level map {level.Map.Comp.MapId} at depth {level.Depth} disappeared before commit.";
                error = RollbackAfterFailure(
                    transaction,
                    "Failed to validate Z-network topology",
                    new InvalidOperationException(validationError));
                return false;
            }

            if (_zLevels.TryGetZNetwork(level.Map.Owner, out var existingNetwork))
            {
                var validationError =
                    $"Z-level map {level.Map.Comp.MapId} at depth {level.Depth} is already in network {existingNetwork.Value.Owner}.";
                error = RollbackAfterFailure(
                    transaction,
                    "Failed to validate Z-network topology",
                    new InvalidOperationException(validationError));
                return false;
            }

            if (!maps.TryAdd(level.Map.Owner, level.Depth))
            {
                var validationError = $"Z-level map {level.Map.Comp.MapId} appears more than once.";
                error = RollbackAfterFailure(
                    transaction,
                    "Failed to validate Z-network topology",
                    new InvalidOperationException(validationError));
                return false;
            }
        }

        try
        {
            ValidateComponentOverrides(componentOverrides);
            CaptureCallerOwnedState(transaction, componentOverrides);

            network = _zLevels.CreateZNetwork();
            transaction.Network = network.Owner;
            _meta.SetEntityName(
                network,
                networkName ?? $"Combined zNetwork: {network.Owner.Id}");

            foreach (var level in levels)
            {
                if (componentOverrides is { Count: > 0 })
                    EntityManager.AddComponents(level.Map.Owner, componentOverrides);

                if (levelName == null ||
                    level.Depth == 0 && !mappingNames)
                {
                    continue;
                }

                var prefix = mappingNames ? "Mapping " : string.Empty;
                var suffix = level.Depth == 0 ? string.Empty : $" [{level.Depth}]";
                _meta.SetEntityName(level.Map.Owner, $"{prefix}{levelName}{suffix}");
            }

            _topologyAttachmentDepth++;
            try
            {
                _zLevels.AttachMapsToZNetwork(network, maps);
            }
            finally
            {
                _topologyAttachmentDepth--;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = RollbackAfterFailure(
                transaction,
                "Failed to commit Z-network topology",
                exception);
            network = default;
            return false;
        }
    }

    private void ValidateComponentOverrides(ComponentRegistry? componentOverrides)
    {
        if (componentOverrides == null)
            return;

        foreach (var (name, entry) in componentOverrides)
        {
            var registration = _componentFactory.GetRegistration(name);
            if (!registration.Type.IsInstanceOfType(entry.Component))
            {
                throw new InvalidOperationException(
                    $"Z-level component override {name} contains {entry.Component.GetType().Name}.");
            }

            if (registration.Type == typeof(MetaDataComponent) ||
                registration.Type == typeof(TransformComponent) ||
                registration.Type == typeof(MapComponent) ||
                registration.Type == typeof(MapGridComponent) ||
                registration.Type == typeof(CMUZLevelMapComponent) ||
                registration.Type == typeof(CMUZLevelsNetworkComponent))
            {
                throw new InvalidOperationException(
                    $"Z-level component overrides cannot replace lifecycle-owned {registration.Type.Name}.");
            }
        }
    }

    private void CaptureCallerOwnedState(
        NetworkConstructionTransaction transaction,
        ComponentRegistry? componentOverrides)
    {
        foreach (var level in transaction.Levels)
        {
            if (level.Owned)
                continue;

            CaptureComponentState(transaction, level.Map.Owner, typeof(CMUZLevelMapComponent));

            if (componentOverrides == null)
                continue;

            foreach (var name in componentOverrides.Keys)
            {
                var registration = _componentFactory.GetRegistration(name);
                CaptureComponentState(transaction, level.Map.Owner, registration.Type);
            }
        }
    }

    private void CaptureComponentState(
        NetworkConstructionTransaction transaction,
        EntityUid level,
        Type componentType)
    {
        var key = new ComponentSnapshotKey(level, componentType);
        if (transaction.CallerOwnedComponents.ContainsKey(key))
            return;

        IComponent? snapshot = null;
        if (TryComp(level, componentType, out var component))
        {
            snapshot = _serialization.CreateCopy(
                component,
                notNullableOverride: true) as IComponent;
            if (snapshot == null)
                throw new InvalidOperationException($"Could not snapshot {componentType.Name} on Z-level {level}.");
        }

        transaction.CallerOwnedComponents.Add(key, snapshot);
    }

    private bool TryInitializeRoundNetwork(
        Entity<CMUZLevelsNetworkComponent> network,
        NetworkConstructionTransaction transaction,
        out string error)
    {
        try
        {
            InitializeAuxiliaryLevels(network);
            _pendingRoundNetworks.Remove(network.Owner);
        }
        catch (Exception exception)
        {
            error = RollbackAfterFailure(
                transaction,
                "Failed to initialize auxiliary Z-levels",
                exception);
            return false;
        }

        PublishNetworkPostCommit(network);
        error = string.Empty;
        return true;
    }

    private void PublishNetworkPostCommit(Entity<CMUZLevelsNetworkComponent> network)
    {
        try
        {
            _zLevels.PublishZNetworkUpdated(network);
        }
        catch (Exception exception)
        {
            Log.Error(
                $"Post-commit Z-network notification failed for {ToPrettyString(network)}. " +
                $"The committed topology was retained because subscriber side effects cannot be rolled back safely.\n{exception}");
        }
    }

    private void InitializeAuxiliaryLevels(Entity<CMUZLevelsNetworkComponent> network)
    {
        foreach (var (depth, mapUid) in network.Comp.ZLevels)
        {
            if (depth == 0 ||
                mapUid is not { } level ||
                !TryComp<MapComponent>(level, out var map) ||
                _map.IsInitialized(map.MapId))
            {
                continue;
            }

            _map.InitializeMap(map.MapId);
        }
    }

    private void RollbackOwnedLevels(List<LoadedZLevel> levels)
    {
        for (var i = levels.Count - 1; i >= 0; i--)
        {
            var level = levels[i];
            if (level.Owned && _map.MapExists(level.Map.Comp.MapId))
                _map.DeleteMap(level.Map.Comp.MapId);
        }
    }

    private string RollbackAfterFailure(
        NetworkConstructionTransaction transaction,
        string operation,
        Exception failure)
    {
        var rollbackFailures = new List<Exception>();
        if (transaction.Network is { } network)
        {
            _pendingRoundNetworks.Remove(network);

            try
            {
                if (Exists(network))
                    Del(network);
            }
            catch (Exception exception)
            {
                rollbackFailures.Add(exception);
            }
        }

        foreach (var (key, snapshot) in transaction.CallerOwnedComponents)
        {
            if (!Exists(key.Level))
                continue;

            try
            {
                if (snapshot == null)
                    RemComp(key.Level, key.ComponentType);
                else
                    AddComp(key.Level, snapshot, overwrite: true);
            }
            catch (Exception exception)
            {
                rollbackFailures.Add(exception);
            }
        }

        for (var i = transaction.Levels.Count - 1; i >= 0; i--)
        {
            var level = transaction.Levels[i];
            if (!level.Owned ||
                !_map.MapExists(level.Map.Comp.MapId))
            {
                continue;
            }

            try
            {
                _map.DeleteMap(level.Map.Comp.MapId);
            }
            catch (Exception exception)
            {
                rollbackFailures.Add(exception);
            }
        }

        var error = $"{operation}: {failure}";
        if (rollbackFailures.Count > 0)
            error += $" Rollback also failed {rollbackFailures.Count} time(s): {rollbackFailures[0].Message}";

        return error;
    }

    private readonly record struct LoadedZLevel(
        Entity<MapComponent> Map,
        int Depth,
        bool Owned);

    private sealed class NetworkConstructionTransaction(List<LoadedZLevel> levels)
    {
        public readonly Dictionary<ComponentSnapshotKey, IComponent?> CallerOwnedComponents = new();
        public readonly List<LoadedZLevel> Levels = levels;
        public EntityUid? Network;
    }

    private readonly record struct ComponentSnapshotKey(
        EntityUid Level,
        Type ComponentType);
}

public readonly record struct CMUZNetworkLoadResult(
    Entity<CMUZLevelsNetworkComponent> Network,
    Entity<MapComponent> BaseLevel,
    IReadOnlyList<MapId> CreatedMaps);
