using System.Numerics;
using Content.Client.IconSmoothing;
using Content.Shared.CMU14.ThreeD;
using Content.Shared.Doors.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DLiveSceneSystem
{
    // Cache geometry only. Recompute source-cell membership on every query, so
    // moving/removing/unanchoring a shutter cannot leave a stale recessed wall.
    private readonly Dictionary<(string Model, int Mask, int Sides), CMU3DModelPart[]> _prisonWindowParts = [];
    private readonly Dictionary<(string Model, int Faces), CMU3DModelPart[]> _prisonWallReliefParts = [];

    private bool TryPrisonWallReliefParts(EntityUid uid, CMU3DModelPrototype model, float yaw,
        IReadOnlyList<CMU3DModelPart> original, out IReadOnlyList<CMU3DModelPart> parts)
    {
        parts = original;
        if (model.ID != "CMU3DReinforcedPrisonWall" ||
            !TryComp(uid, out MetaDataComponent? meta) || meta.EntityPrototype?.ID != "RMCWallPrisonReinforced" ||
            !TryComp(uid, out IconSmoothComponent? smooth) || !smooth.Enabled ||
            smooth.Mode != IconSmoothingMode.Corners || !smooth.AdditionalKeys.Contains("windows") ||
            !TryComp(uid, out TransformComponent? xform) || !xform.Anchored ||
            !TryComp(xform.GridUid, out MapGridComponent? grid))
            return false;
        var gridUid = xform.GridUid.Value;
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var position = _transform.GetWorldPosition(xform) + model.GroundOffset;
        var faces = 0;
        foreach (var (_, delta, _) in CMU3DSceneLayout.Cardinals)
        foreach (var other in _map.GetAnchoredEntities(gridUid, grid, tile + delta))
        {
            if (TerminatingOrDeleted(other) || !TryComp(other, out MetaDataComponent? windowMeta) ||
                windowMeta.EntityPrototype?.ID != "RMCWindowPrisonCell" ||
                _catalog?.Resolve("RMCWindowPrisonCell") is not { Exact: true } match || !match.Model.ConnectToNeighbours ||
                !TryComp(other, out SpriteComponent? sprite) || !sprite.Visible || sprite.Color.A <= 0 ||
                !TryConnections(other, out var mask, out var gridYaw))
                continue;
            if (!_connectedParts.TryGetValue((match.Model.ID, mask), out var connected))
            {
                connected = CMU3DSceneLayout.ConnectedParts(match.Model.Parts, mask, match.Model.SupportSurface, match.Model.ConnectionEndInset);
                _connectedParts[(match.Model.ID, mask)] = connected;
            }
            if (!TryPrisonWindowParts(other, match.Model, mask, gridYaw, connected, out _, out _))
                continue;
            var windowDelta = _transform.GetWorldPosition(other) + match.Model.GroundOffset - position;
            faces |= CMU3DSceneLayout.PrisonWallJoinFace(windowDelta, yaw, gridYaw, mask);
        }
        if (faces == 0)
            return false;
        var key = (model.ID, faces);
        if (!_prisonWallReliefParts.TryGetValue(key, out var joined))
        {
            if (!CMU3DSceneLayout.TryPrisonJoinedReliefParts(original, faces, out joined))
                return false;
            _prisonWallReliefParts[key] = joined;
        }
        parts = joined;
        return true;
    }

    private bool TryPrisonWindowParts(EntityUid uid, CMU3DModelPrototype model, int mask, float gridYaw,
        IReadOnlyList<CMU3DModelPart> connected, out IReadOnlyList<CMU3DModelPart> parts, out int sides)
    {
        parts = connected;
        sides = 0;
        if (model.ID != "CMU3DPrisonCellObservationWindow" ||
            !TryComp(uid, out MetaDataComponent? meta) || meta.EntityPrototype?.ID != "RMCWindowPrisonCell" ||
            CMU3DSceneLayout.ShutterExteriorAxis("RMCWindowPrisonCell", mask) is not { } axis ||
            !TryComp(uid, out TransformComponent? xform) || !xform.Anchored ||
            !TryComp(xform.GridUid, out MapGridComponent? grid))
            return false;
        var gridUid = xform.GridUid.Value;
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var position = _transform.GetWorldPosition(xform) + model.GroundOffset;
        foreach (var other in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (other == uid || TerminatingOrDeleted(other) ||
                !TryComp(other, out MetaDataComponent? shutterMeta) || shutterMeta.EntityPrototype is not { } prototype ||
                prototype.ID is not ("RMCShutterHybrisaWindow" or "RMCShutterHybrisaWindowOpen") ||
                _catalog?.Resolve(prototype.ID) is not { Exact: true } match ||
                !TryComp(other, out SpriteComponent? sprite) || !sprite.Visible || sprite.Color.A <= 0 ||
                sprite.ContainerOccluded || !sprite.AddToTree ||
                !TryComp(other, out TransformComponent? shutterXform) || !shutterXform.Anchored ||
                !TryComp(other, out DoorComponent? door))
                continue;
            var posed = _catalog.WithDoorState(match, door.State);
            if (posed is not { Exact: true } shutter ||
                Array.IndexOf(shutter.Model.WindowMountTargets, "RMCWindowPrisonCell") < 0 ||
                !TryDoorParts(other, sprite, shutter.Model, out _, out _))
                continue;
            var yaw = CMU3DSceneLayout.RenderYaw(shutter.Model, (float) _transform.GetWorldRotation(other).Theta,
                sprite.NoRotation, sprite.SnapCardinals) + (float) sprite.Rotation.Theta;
            var delta = _transform.GetWorldPosition(shutterXform) + shutter.Model.GroundOffset - position;
            sides |= CMU3DSceneLayout.PrisonShutterSide(prototype.ID, shutter.Model.ID, axis, yaw,
                gridYaw, delta, shutter.Model.WindowMountInside);
        }
        if (sides == 0)
            return false;
        var key = (model.ID, mask, sides);
        if (!_prisonWindowParts.TryGetValue(key, out var recessed))
        {
            if (!CMU3DSceneLayout.TryPrisonRecessParts(connected, axis, sides, out recessed))
            {
                sides = 0;
                return false;
            }
            _prisonWindowParts[key] = recessed;
        }
        parts = recessed;
        return true;
    }
}
