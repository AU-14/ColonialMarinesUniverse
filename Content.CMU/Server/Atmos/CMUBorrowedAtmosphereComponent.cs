namespace Content.Server.CMU14.Atmos;

/// <summary>
///     A grid with no air of its own, like a dropship's lower deck and lowered ramp. Anything on it breathes the air of
///     the grid it's over (a ship's hangar, a colony's landing pad), and the map's atmosphere where there's none.
/// </summary>
[RegisterComponent]
public sealed partial class CMUBorrowedAtmosphereComponent : Component;
