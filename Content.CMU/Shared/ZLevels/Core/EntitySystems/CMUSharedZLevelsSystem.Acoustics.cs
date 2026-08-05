using System.Numerics;
using Content.Shared._CMU14.ZLevels.Core.Components;

namespace Content.Shared._CMU14.ZLevels.Core.EntitySystems;

public abstract partial class CMUSharedZLevelsSystem
{
    /// <summary>
    /// Resolves one adjacent Z-level crossing and the opening in the crossed floor.
    /// </summary>
    public bool TryTraverseAcousticBoundary(
        Entity<CMUZLevelMapComponent?> currentMap,
        Vector2 searchOrigin,
        int step,
        float openingRadius,
        out CMUZLevelAcousticPathStep pathStep)
    {
        pathStep = default;

        if (step is not -1 and not 1 ||
            !Resolve(currentMap, ref currentMap.Comp, false) ||
            !TryMapOffset(currentMap, step, out var targetMap))
        {
            return false;
        }

        var boundaryMap = step < 0
            ? currentMap.Owner
            : targetMap.Value.Owner;

        if (!TryFindOpeningNear(boundaryMap, searchOrigin, openingRadius, out var openingPosition))
            return false;

        pathStep = new CMUZLevelAcousticPathStep(targetMap.Value, openingPosition);
        return true;
    }

    /// <summary>
    /// Builds the sequence of openings crossed while sound travels through adjacent Z-levels.
    /// </summary>
    /// <remarks>
    /// A downward crossing uses the current level's floor. An upward crossing uses the target
    /// level's floor. Each selected opening becomes the origin for the next boundary search.
    /// </remarks>
    public void BuildAcousticPath(
        Entity<CMUZLevelMapComponent?> sourceMap,
        Vector2 sourcePosition,
        int step,
        int maxDepth,
        float openingRadius,
        List<CMUZLevelAcousticPathStep> path)
    {
        path.Clear();

        if (step is not -1 and not 1 ||
            maxDepth <= 0)
        {
            return;
        }

        var currentMap = sourceMap;
        var searchOrigin = sourcePosition;

        for (var depth = 0; depth < maxDepth; depth++)
        {
            if (!TryTraverseAcousticBoundary(
                    currentMap,
                    searchOrigin,
                    step,
                    openingRadius,
                    out var pathStep))
            {
                return;
            }

            path.Add(pathStep);
            currentMap = (pathStep.TargetMap.Owner, pathStep.TargetMap.Comp);
            searchOrigin = pathStep.OpeningPosition;
        }
    }
}

public readonly record struct CMUZLevelAcousticPathStep(
    Entity<CMUZLevelMapComponent> TargetMap,
    Vector2 OpeningPosition);
