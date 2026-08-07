namespace Content.Shared._CMU14.Round.Objectives.Component;

/// <summary>Mappers place these to mark obj ent spawnpoints, used by FindMarkers/ResolveMarkers.
/// </summary>
[RegisterComponent]
public sealed partial class CMUObjectiveMarkerComponent : Robust.Shared.GameObjects.Component
{
    [DataField] public string FetchId = string.Empty;

    /// <summary>When true: this marker can be used as fallback when no specific marker is found</summary>
    [DataField] public bool Generic;

    /// <summary>Set to true once a spawn has consumed this marker</summary>
    public bool Used;
}
