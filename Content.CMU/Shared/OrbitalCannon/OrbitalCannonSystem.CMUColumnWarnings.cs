using Content.Shared.CMU14.ZLevels.Ordnance;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.OrbitalCannon;

public sealed partial class OrbitalCannonSystem
{
    private readonly HashSet<MapId> _cmuWarnedMaps = new();

    // on a multi-z map the impact column runs down to the deepest solid floor, and the
    // warnings only went to that floor's map. anyone standing on the levels above, which the
    // blast still hits, got nothing until the explosions started
    private void CMUPopupColumnWarning(CMUTopDownOrdnanceResult column, float range, LocId warning, LocId warningAbove)
    {
        _cmuWarnedMaps.Clear();
        foreach (var surface in column.Surfaces)
        {
            if (_cmuWarnedMaps.Add(surface.Coordinates.MapId))
                _mortar.PopupWarning(surface.Coordinates, range, warning, warningAbove, true);
        }
    }
}
