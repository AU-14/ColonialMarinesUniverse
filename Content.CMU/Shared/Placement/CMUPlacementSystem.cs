using System.Diagnostics.CodeAnalysis;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared.CMU14.Placement;

/// <summary>
///     Keeps deployables from being placed on or near certain anchored entities, ported from RMC-14.
/// </summary>
public sealed class CMUPlacementSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    /// <summary>
    ///     Whether <paramref name="placed"/> can be deployed at <paramref name="coordinates"/> under its
    ///     <see cref="CMUPlacementRestrictionsComponent"/>, telling <paramref name="user"/> what's in the way if not.
    /// </summary>
    public bool CanPlacePopup(EntityUid placed, EntityCoordinates coordinates, EntityUid user)
    {
        if (!TryComp(placed, out CMUPlacementRestrictionsComponent? restrictions) ||
            !TryFindBlocker(coordinates, restrictions.Restrictions, out var blocker, placed))
        {
            return true;
        }

        var msg = Loc.GetString("emplacement-mount-too-close", ("mount", blocker));
        _popup.PopupEntity(msg, user, user, PopupType.SmallCaution);
        return false;
    }

    /// <summary>
    ///     Tries to find an anchored entity that blocks placement.
    /// </summary>
    public bool TryFindBlocker(EntityCoordinates coordinates, IReadOnlyList<CMUPlacementRestriction> restrictions, [NotNullWhen(true)] out EntityUid? blocker, EntityUid? ignored = null)
    {
        blocker = null;
        if (restrictions.Count == 0)
            return false;

        var grid = _transform.GetGrid(coordinates);
        if (!TryComp(grid, out MapGridComponent? mapGrid))
            return false;

        var position = _map.LocalToTile(grid.Value, mapGrid, coordinates);
        foreach (var restriction in restrictions)
        {
            if (restriction.Radius < 0)
                continue;

            var radius = restriction.Radius;
            var area = new Box2(position.X - radius, position.Y - radius, position.X + radius + 1, position.Y + radius + 1);

            foreach (var anchored in _map.GetLocalAnchoredEntities(grid.Value, mapGrid, area))
            {
                if (anchored == ignored || !_whitelist.IsValid(restriction.Blacklist, anchored))
                    continue;

                blocker = anchored;
                return true;
            }
        }

        return false;
    }
}
