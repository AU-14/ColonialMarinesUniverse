namespace Content.Server.AU14.Scenario;

/// <summary>
/// Immutable view of the world and faction selections consumed by Scenario Plan resolution.
/// Runtime copies may update player count, the effective fallback preset, and a deferred threat without
/// rereading or changing the planet, platoons, and ships frozen for map preloading.
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
    /// Derives a runtime request while retaining the world and faction maps frozen at preload.
    /// The effective preset may differ when GameTicker starts its configured fallback after maps were loaded.
    /// </summary>
    public RoundPlanSelectionSnapshot WithRuntimeContext(
        int playerCount,
        string presetId,
        string? selectedThreatId)
    {
        return this with
        {
            PresetId = string.IsNullOrWhiteSpace(presetId) ? PresetId : presetId,
            PlayerCount = playerCount,
            SelectedThreatId = selectedThreatId,
        };
    }

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
