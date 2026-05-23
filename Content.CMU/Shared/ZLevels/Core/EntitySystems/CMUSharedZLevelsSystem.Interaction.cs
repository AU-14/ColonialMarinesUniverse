using System.Numerics;
using Content.Shared._CMU14.ZLevels.Core.Components;
using JetBrains.Annotations;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Shared._CMU14.ZLevels.Core.EntitySystems;

public abstract partial class CMUSharedZLevelsSystem
{
    [Dependency] private IConfigurationManager _configuration = default!;

    /// <summary>
    /// Adds players whose Z-level probe eye can see <paramref name="coordinates"/> to a normal PVS filter.
    /// </summary>
    [PublicAPI]
    public Filter AddZLevelViewers(Filter filter, MapCoordinates coordinates, float rangeMultiplier = 2f)
    {
        if (coordinates.MapId == MapId.Nullspace ||
            !_configuration.GetCVar(CVars.NetPVS))
        {
            return filter;
        }

        var pvsRange = _configuration.GetCVar(CVars.NetMaxUpdateRange) * rangeMultiplier;
        var pvsRangeSquared = pvsRange * pvsRange;

        return filter.AddWhereAttachedEntity(attached =>
        {
            if (!TryComp<CMUZLevelViewerComponent>(attached, out var viewer))
                return false;

            foreach (var eye in viewer.Eyes)
            {
                if (!_xformQuery.TryComp(eye, out var eyeXform) ||
                    eyeXform.MapID != coordinates.MapId)
                {
                    continue;
                }

                var eyePosition = _transform.GetWorldPosition(eyeXform, _xformQuery);
                if (Vector2.DistanceSquared(eyePosition, coordinates.Position) <= pvsRangeSquared)
                    return true;
            }

            return false;
        });
    }
}
