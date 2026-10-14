// ReSharper disable CheckNamespace

namespace Content.Server._RMC14.Dropship;

// CMU14: whether a dropship is leaving a warship, for turbulence.
public sealed partial class DropshipSystem
{
    /// <summary>
    ///     Whether the dropship is sitting on a warship, so its takeoff drops it out of the bay.
    /// </summary>
    public bool CMUIsDepartingWarship(EntityUid dropship)
    {
        if (Transform(dropship).MapUid is not { } map)
            return false;

        return IsShipMap(_multiDeck.GetGroundMap(dropship, map));
    }
}
