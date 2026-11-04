namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DLiveSceneSystem
{
    private void ReleaseSceneData()
    {
        CancelRefresh();
        _catalog = null;
        _actor = null;
        _selected = null;
        _selectedModel = null;
        _cachedParts = 0;

        // Clearing reference entries alone keeps large scene buffers allocated on this
        // long-lived entity system. Return their capacity when the view closes as well.
        ReleaseBuffer(_combined);
        ReleaseBuffer(_roofTiles);
        ReleaseBuffer(_spriteEntities);
        ReleaseBuffer(_fallbackSprites);
        ReleaseBuffer(_tileColors);
        ReleaseBuffer(_candidates);
        ReleaseBuffer(_wallTargets);
        ReleaseBuffer(_ordered);
        ReleaseBuffer(_grids);
        ReleaseBuffer(_boxes);
        ReleaseBuffer(_entityBoxes);
        ReleaseBuffer(_animatedSprites);
        ReleaseBuffer(_activeAnimatedSprites);
        ReleaseBuffer(_surfaces);
        ReleaseBuffer(_connectedParts);
        ReleaseBuffer(_insideWallParts);
        ReleaseBuffer(_surfaceOffsets);
        ReleaseBuffer(_elevationOffsets);
        ReleaseBuffer(_surfaceProps);
        ReleaseBuffer(_surfaceRearWalls);
        ReleaseBuffer(_surfaceMountFallbacks);
        ReleaseBuffer(_geometryCache);
        ReleaseBuffer(_geometryUsed);
        ReleaseBuffer(_geometryStale);
        ReleaseBuffer(_publishedSources);
        ReleaseBuffer(_mapDemand);
        ReleaseBuffer(_nearSources);
        ReleaseBuffer(_nextNearSources);
        ReleaseBuffer(_detailedWalls);
        ReleaseBuffer(_nextDetailedWalls);
        ReleaseBuffer(_distantWalls);
        ReleaseBuffer(_prisonWindowParts);
        ReleaseBuffer(_prisonWallReliefParts);
        ReleaseBuffer(_slabTiles);
        ReleaseBuffer(_slabPlans);
        ReleaseBuffer(_slabAdmitted);
        ReleaseBuffer(_slabCladdings);
        ReleaseBuffer(_terrainSources);
        ReleaseBuffer(_terrainCutouts);
        ReleaseBuffer(_terrainVolumes);
        ReleaseBuffer(_terrainTargetPrototypes);
        ReleaseBuffer(_terrainBounds);
        ReleaseBuffer(_paperOffsets);
        ReleaseBuffer(_paperCandidates);
        ReleaseBuffer(_zStairEntities);
        ReleaseBuffer(_wornLayerIndices);

        var lease = _modelLease;
        _modelLease = null;
        lease?.Dispose();
    }

    private static void ReleaseBuffer<T>(List<T> buffer)
    {
        buffer.Clear();
        buffer.TrimExcess();
    }

    private static void ReleaseBuffer<T>(HashSet<T> buffer)
    {
        buffer.Clear();
        buffer.TrimExcess();
    }

    private static void ReleaseBuffer<TKey, TValue>(Dictionary<TKey, TValue> buffer) where TKey : notnull
    {
        buffer.Clear();
        buffer.TrimExcess();
    }
}
