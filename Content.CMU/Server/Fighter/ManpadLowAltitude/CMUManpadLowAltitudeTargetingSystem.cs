using System.Numerics;
using Content.Server.CMU14.Blackfoot;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.Blackfoot;
using Content.Shared.CMU14.Dropship.Integrity;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Fighter;

public sealed partial class CMUManpadLowAltitudeTargetingSystem : EntitySystem
{
    [Dependency] private CMUBlackfootDestructionSystem _destruction = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    private EntityQuery<CMUZLevelMapComponent> _levelQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<DropshipIntegrityComponent> _integrityQuery;
    private EntityQuery<DropshipTacticalHoverComponent> _hoverQuery;
    private EntityQuery<BlackfootFlightComponent> _blackfootQuery;

    public override void Initialize()
    {
        _levelQuery = GetEntityQuery<CMUZLevelMapComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _integrityQuery = GetEntityQuery<DropshipIntegrityComponent>();
        _hoverQuery = GetEntityQuery<DropshipTacticalHoverComponent>();
        _blackfootQuery = GetEntityQuery<BlackfootFlightComponent>();
    }

    public bool TryFindTarget(
        Entity<CMUManpadLowAltitudeComponent> launcher,
        out EntityUid target,
        out CMUManpadLowAltitudeTargetKind kind)
    {
        target = default;
        kind = CMUManpadLowAltitudeTargetKind.None;

        var bestDistanceSquared = float.MaxValue;
        var gunships = EntityQueryEnumerator<DropshipTacticalHoverComponent>();
        while (gunships.MoveNext(out var uid, out _))
        {
            ConsiderTarget(launcher, uid, ref bestDistanceSquared, ref target, ref kind);
        }

        var blackfoots = EntityQueryEnumerator<BlackfootFlightComponent>();
        while (blackfoots.MoveNext(out var uid, out _))
        {
            ConsiderTarget(launcher, uid, ref bestDistanceSquared, ref target, ref kind);
        }

        return kind != CMUManpadLowAltitudeTargetKind.None;
    }

    private void ConsiderTarget(
        Entity<CMUManpadLowAltitudeComponent> launcher,
        EntityUid candidate,
        ref float bestDistanceSquared,
        ref EntityUid target,
        ref CMUManpadLowAltitudeTargetKind kind)
    {
        if (!TryGetKind(candidate, out var candidateKind)
            || !IsInReach(launcher, candidate, out var distanceSquared)
            || distanceSquared >= bestDistanceSquared)
        {
            return;
        }

        bestDistanceSquared = distanceSquared;
        target = candidate;
        kind = candidateKind;
    }

    public bool CanTrack(Entity<CMUManpadLowAltitudeComponent> launcher, EntityUid target)
    {
        return TryGetKind(target, out _) && IsInReach(launcher, target, out _);
    }

    public bool TryGetKind(EntityUid target, out CMUManpadLowAltitudeTargetKind kind)
    {
        kind = CMUManpadLowAltitudeTargetKind.None;

        if (_hoverQuery.HasComp(target))
        {
            if (_integrityQuery.TryComp(target, out var integrity) && !integrity.Crashing && !integrity.Wrecked)
                kind = CMUManpadLowAltitudeTargetKind.Gunship;
        }
        else if (_blackfootQuery.TryComp(target, out var flight) && IsAirborne(flight.State) && !_destruction.IsFalling(target))
        {
            kind = CMUManpadLowAltitudeTargetKind.Blackfoot;
        }

        return kind != CMUManpadLowAltitudeTargetKind.None;
    }

    /// <summary>Gunships are aimed at their grid center, a Blackfoot at its position.</summary>
    public Vector2 GetTargetCenter(EntityUid target)
    {
        var local = _gridQuery.TryComp(target, out var grid) ? grid.LocalAABB.Center : Vector2.Zero;
        return Vector2.Transform(local, _transform.GetWorldMatrix(target));
    }

    /// <summary>A Blackfoot stays on its ground level until takeoff finishes.</summary>
    private static bool IsAirborne(BlackfootFlightState state)
    {
        return state is
            BlackfootFlightState.VTOL or
            BlackfootFlightState.Flight or
            BlackfootFlightState.Landing;
    }

    private bool IsInReach(
        Entity<CMUManpadLowAltitudeComponent> launcher,
        EntityUid target,
        out float distanceSquared)
    {
        distanceSquared = 0f;

        var launcherXform = Transform(launcher);
        var targetXform = Transform(target);
        if (launcherXform.MapUid is not { } launcherMap
            || targetXform.MapUid is not { } targetMap
            || !_levelQuery.TryComp(launcherMap, out var launcherLevel)
            || !_levelQuery.TryComp(targetMap, out var targetLevel)
            || launcherLevel.NetworkUid != targetLevel.NetworkUid
            || targetLevel.Depth <= launcherLevel.Depth)
        {
            return false;
        }

        var origin = _transform.GetWorldPosition(launcherXform);
        distanceSquared = Vector2.DistanceSquared(origin, GetTargetCenter(target));
        if (distanceSquared > launcher.Comp.LockRange * launcher.Comp.LockRange)
            return false;

        return IsOpenToSky(launcherMap, launcherLevel.Depth, targetLevel.Depth, origin);
    }

    private bool IsOpenToSky(EntityUid operatorMap, int operatorDepth, int targetDepth, Vector2 position)
    {
        if (!_zLevels.TryGetZNetwork(operatorMap, out var network))
            return false;

        for (var depth = operatorDepth + 1; depth <= targetDepth; depth++)
        {
            if (!_zLevels.TryGetMapAtDepth(network.Value, depth, out var map)
                || !_gridQuery.TryComp(map, out var grid))
            {
                continue;
            }

            if (!CMUZLevelOpeningCache.IsOpeningTile(map, grid, position, _map, _tileDefinitions))
                return false;
        }

        return true;
    }
}
