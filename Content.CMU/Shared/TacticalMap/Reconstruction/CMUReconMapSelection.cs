using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.TacticalMap.Reconstruction;

[Serializable, NetSerializable]
public enum CMUReconMapChoice : byte { Automatic, Planet, Ship }

public static class CMUReconMapSelection
{
    public static CMUReconMapChoice Choose(CMUReconMapChoice requested, bool aboardShip,
        bool preferPlanetOnShip, bool hasPlanet, bool hasShip)
    {
        if (requested == CMUReconMapChoice.Planet && hasPlanet) return requested;
        if (requested == CMUReconMapChoice.Ship && hasShip) return requested;
        if (aboardShip && hasShip && (!preferPlanetOnShip || !hasPlanet)) return CMUReconMapChoice.Ship;
        if (hasPlanet) return CMUReconMapChoice.Planet;
        return hasShip ? CMUReconMapChoice.Ship : CMUReconMapChoice.Automatic;
    }
}
