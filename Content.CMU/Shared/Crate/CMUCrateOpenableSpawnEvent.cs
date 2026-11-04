namespace Content.Shared.CMU14.Crate;

/// <summary>
///     Raised on a pried-open crate for each thing it spawns, so the server can swap it, e.g. for the ship's own
///     platoon rations.
/// </summary>
[ByRefEvent]
public record struct CMUCrateOpenableSpawnEvent(string Prototype);
