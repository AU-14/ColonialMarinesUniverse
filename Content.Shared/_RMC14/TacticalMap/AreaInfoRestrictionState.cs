namespace Content.Shared._RMC14.TacticalMap;

/// <summary>All inputs to the local restriction text, independent of the area's name.</summary>
public readonly record struct AreaInfoRestrictionState(
    short CeilingLevel,
    bool HiveCoreProtection,
    bool PylonProtection,
    bool OrbitalBombardment,
    bool OrbitalRedirected,
    bool CloseAirSupport,
    bool SupplyDrops,
    bool MortarFire,
    bool MortarRedirected,
    bool MortarPlacement,
    bool LaserDesignation,
    bool Medevac,
    bool Paradropping,
    bool NoTunnel,
    bool Unweedable,
    bool ResinAllowed);
