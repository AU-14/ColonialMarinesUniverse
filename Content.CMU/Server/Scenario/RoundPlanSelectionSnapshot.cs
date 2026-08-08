namespace Content.Server.AU14.Scenario;

/// <summary>
/// Immutable view of every round selection consumed by Scenario Plan resolution.
/// Capture this once when round-start selections are frozen instead of rereading mutable systems per stage.
/// </summary>
public readonly record struct RoundPlanSelectionSnapshot(
    string PresetId,
    int PlayerCount,
    string? GovforPlatoonId,
    string? OpforPlatoonId,
    string? PlanetId,
    string? MapId,
    string? SelectedThreatId,
    string? GovforShipId,
    string? OpforShipId)
{
    /// <summary>
    /// Whether the snapshot identifies the preset and world required to bind a live round plan.
    /// </summary>
    public bool HasWorldSelection =>
        !string.IsNullOrWhiteSpace(PresetId) &&
        !string.IsNullOrWhiteSpace(PlanetId) &&
        !string.IsNullOrWhiteSpace(MapId);

    /// <summary>
    /// Converts the frozen selection into the existing Scenario Plan resolver contract.
    /// </summary>
    public ScenarioPlanValidationRequest ToScenarioPlanRequest() => new(
        PresetId,
        PlayerCount,
        GovforPlatoonId,
        OpforPlatoonId,
        PlanetId,
        MapId,
        SelectedThreatId,
        GovforShipId,
        OpforShipId);
}
