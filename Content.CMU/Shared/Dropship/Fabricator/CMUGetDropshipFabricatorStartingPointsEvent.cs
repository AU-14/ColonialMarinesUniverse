namespace Content.Shared.CMU14.Dropship.Fabricator;

/// <summary>
/// Raised broadcast when a new dropship fabricator points account is created.
/// <see cref="Points"/> starts at the CVar default and may be overridden, e.g. by the active game preset.
/// </summary>
[ByRefEvent]
public record struct CMUGetDropshipFabricatorStartingPointsEvent(int Points);
