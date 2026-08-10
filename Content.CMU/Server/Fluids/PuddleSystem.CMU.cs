// ReSharper disable CheckNamespace

using System.Numerics;
using System.Linq;
using Content.Server.Decals;
using Content.Server.Fluids.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Decals;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Fluids.EntitySystems;

public sealed partial class PuddleSystem
{
    private static readonly EntProtoId PuddlePrototype = "Puddle";
    private static readonly EntProtoId BloodDecalPuddlePrototype = "BloodDecalPuddle";
    private static readonly ProtoId<ReagentPrototype> BloodReagent = "Blood";

    [Dependency] private DecalSystem _cmuDecals = default!;

    private void InitializeCmuPuddles()
    {
        SubscribeLocalEvent<PuddleDecalVisualsComponent, ComponentShutdown>(OnPuddleDecalShutdown);
    }

    private void OnPuddleDecalShutdown(Entity<PuddleDecalVisualsComponent> entity, ref ComponentShutdown args)
    {
        if (entity.Comp.DecalId is { } decalId &&
            entity.Comp.GridUid is { } gridUid)
        {
            _cmuDecals.RemoveDecal(gridUid, decalId);
        }
    }

    private EntProtoId GetPuddlePrototype(Solution solution)
    {
        foreach (var (reagent, _) in solution.Contents)
        {
            if (reagent.Prototype == BloodReagent)
                return BloodDecalPuddlePrototype;
        }

        return PuddlePrototype;
    }

    private void TrySpawnPuddleDecal(EntityUid puddleUid, EntityCoordinates? coordinates = null)
    {
        if (TryComp<PuddleDecalVisualsComponent>(puddleUid, out var decalVisuals))
            TrySpawnPuddleDecal((puddleUid, decalVisuals), coordinates ?? Transform(puddleUid).Coordinates);
    }

    private void TrySpawnPuddleDecal(Entity<PuddleDecalVisualsComponent> ent, EntityCoordinates coordinates)
    {
        if (ent.Comp.DecalId != null || ent.Comp.Decals.Count == 0)
            return;

        if (_transform.GetGrid(coordinates) is not { } gridUid)
            return;

        var rotation = ent.Comp.RandomRotation ? _random.NextAngle() : Angle.Zero;
        if (_cmuDecals.TryAddDecal(
                _random.Pick(ent.Comp.Decals),
                coordinates.Offset(ent.Comp.Offset),
                out var decalId,
                rotation: rotation,
                zIndex: ent.Comp.ZIndex,
                cleanable: ent.Comp.Cleanable))
        {
            ent.Comp.DecalId = decalId;
            ent.Comp.GridUid = gridUid;
        }
    }

    public override bool CleanDecalsAt(TileRef tileRef)
    {
        if (!TryGetDecalsAt(tileRef, out var grid, out var decals))
            return false;

        var removedAny = false;
        ClearMissingPuddleDecalReferences(tileRef, grid, decals);

        foreach (var (index, decal) in decals)
        {
            if (!decal.Cleanable || !_cmuDecals.RemoveDecal(tileRef.GridUid, index))
                continue;

            ClearPuddleDecalReference(tileRef, grid, index);
            removedAny = true;
        }

        return removedAny;
    }

    public override bool HasCleanableDecalsAt(TileRef tileRef)
    {
        if (!TryGetDecalsAt(tileRef, out _, out var decals))
            return false;

        foreach (var (_, decal) in decals)
        {
            if (decal.Cleanable)
                return true;
        }

        return false;
    }

    private bool TryGetDecalsAt(
        TileRef tileRef,
        out MapGridComponent grid,
        out HashSet<(DecalIndex Index, Decal Decal)> decals)
    {
        grid = default!;
        decals = default!;

        if (!TryComp(tileRef.GridUid, out MapGridComponent? gridComp))
            return false;

        grid = gridComp;

        var bounds = _lookup.GetLocalBounds(tileRef, grid.TileSize)
            .Enlarged(0.5f)
            .Translated(new Vector2(-0.5f, -0.5f));
        decals = _cmuDecals.GetDecalsIntersecting(tileRef.GridUid, bounds);
        return true;
    }

    private void ClearMissingPuddleDecalReferences(
        TileRef tileRef,
        MapGridComponent grid,
        HashSet<(DecalIndex Index, Decal Decal)> decals)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(tileRef.GridUid, grid, tileRef.GridIndices);

        while (anchored.MoveNext(out var ent))
        {
            if (!TryComp<PuddleDecalVisualsComponent>(ent.Value, out var decalVisuals))
                continue;

            if (decalVisuals.GridUid != tileRef.GridUid ||
                decalVisuals.DecalId is not { } decalId ||
                decals.Any(entry => entry.Index == decalId))
            {
                continue;
            }

            decalVisuals.DecalId = null;
            decalVisuals.GridUid = null;
        }
    }

    private void ClearPuddleDecalReference(TileRef tileRef, MapGridComponent grid, DecalIndex decalId)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(tileRef.GridUid, grid, tileRef.GridIndices);

        while (anchored.MoveNext(out var ent))
        {
            if (!TryComp<PuddleDecalVisualsComponent>(ent.Value, out var decalVisuals) ||
                decalVisuals.GridUid != tileRef.GridUid ||
                decalVisuals.DecalId != decalId)
            {
                continue;
            }

            decalVisuals.DecalId = null;
            decalVisuals.GridUid = null;
        }
    }
}
