using Content.Shared.CMU.Round;

namespace Content.Server.AU14.Scenario;

/// <summary>
/// Immutable view of the world and faction selections consumed by Scenario Plan resolution.
/// Runtime copies may update player count, the effective fallback preset, and a deferred threat without
/// rereading or changing the planet, forces, and ships frozen for map preloading.
/// </summary>
public readonly record struct RoundPlanSelectionSnapshot
{
    private readonly RoundForceAssignment? _govforAssignment;
    private readonly RoundForceAssignment? _opforAssignment;

    /// <summary>
    /// Compatibility constructor for callers that still provide legacy platoon selections.
    /// </summary>
    public RoundPlanSelectionSnapshot(
        string presetId,
        int playerCount,
        string? govforPlatoonId,
        string? opforPlatoonId,
        string? planetId,
        string? mapId,
        string? selectedThreatId,
        string? govforShipId,
        string? opforShipId)
        : this(
            presetId,
            playerCount,
            LegacyPlatoonAssignmentAdapter.FromLegacySelection(
                RoundSide.Govfor,
                govforPlatoonId,
                govforShipId),
            LegacyPlatoonAssignmentAdapter.FromLegacySelection(
                RoundSide.Opfor,
                opforPlatoonId,
                opforShipId),
            planetId,
            mapId,
            selectedThreatId)
    {
    }

    private RoundPlanSelectionSnapshot(
        string presetId,
        int playerCount,
        RoundForceAssignment? govforAssignment,
        RoundForceAssignment? opforAssignment,
        string? planetId,
        string? mapId,
        string? selectedThreatId)
    {
        ValidateAssignment(govforAssignment, RoundSide.Govfor, nameof(govforAssignment));
        ValidateAssignment(opforAssignment, RoundSide.Opfor, nameof(opforAssignment));

        PresetId = presetId;
        PlayerCount = playerCount;
        _govforAssignment = govforAssignment;
        _opforAssignment = opforAssignment;
        PlanetId = planetId;
        MapId = mapId;
        SelectedThreatId = selectedThreatId;
    }

    public string PresetId { get; init; }
    public int PlayerCount { get; init; }
    public RoundForceAssignment? GovforAssignment
    {
        get => _govforAssignment;
        init
        {
            ValidateAssignment(value, RoundSide.Govfor, nameof(value));
            _govforAssignment = value;
        }
    }

    public RoundForceAssignment? OpforAssignment
    {
        get => _opforAssignment;
        init
        {
            ValidateAssignment(value, RoundSide.Opfor, nameof(value));
            _opforAssignment = value;
        }
    }
    public string? PlanetId { get; init; }
    public string? MapId { get; init; }
    public string? SelectedThreatId { get; init; }

    /// <summary>
    /// Legacy projection of the GOVFOR force as its former platoon prototype ID.
    /// </summary>
    public string? GovforPlatoonId => GovforAssignment?.Force.Value;

    /// <summary>
    /// Legacy projection of the OPFOR force as its former platoon prototype ID.
    /// </summary>
    public string? OpforPlatoonId => OpforAssignment?.Force.Value;

    /// <summary>
    /// Legacy projection of the GOVFOR main ship selection.
    /// </summary>
    public string? GovforShipId => GovforAssignment?.MainShipId;

    /// <summary>
    /// Legacy projection of the OPFOR main ship selection.
    /// </summary>
    public string? OpforShipId => OpforAssignment?.MainShipId;

    /// <summary>
    /// Creates a snapshot whose faction selection is stored as typed side assignments.
    /// </summary>
    public static RoundPlanSelectionSnapshot FromAssignments(
        string presetId,
        int playerCount,
        RoundForceAssignment? govforAssignment,
        RoundForceAssignment? opforAssignment,
        string? planetId,
        string? mapId,
        string? selectedThreatId)
    {
        return new RoundPlanSelectionSnapshot(
            presetId,
            playerCount,
            govforAssignment,
            opforAssignment,
            planetId,
            mapId,
            selectedThreatId);
    }

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

    private static void ValidateAssignment(
        RoundForceAssignment? assignment,
        RoundSide expectedSide,
        string parameterName)
    {
        if (assignment == null)
            return;

        if (!assignment.Value.Force.IsValid)
        {
            throw new ArgumentException(
                "The round force identifier cannot be missing.",
                parameterName);
        }

        if (assignment.Value.Side == expectedSide)
            return;

        throw new ArgumentException(
            $"The {expectedSide.ToString().ToUpperInvariant()} assignment must identify the {expectedSide.ToString().ToUpperInvariant()} side.",
            parameterName);
    }
}
