using Content.Shared.CMU14.CaveSurvey;

namespace Content.Server.CMU14.CaveSurvey;

/// <summary>Server-only terrain knowledge on a person or a physical map. Never serialized between rounds.</summary>
[RegisterComponent]
public sealed partial class CMUCaveSurveyMemoryComponent : Component
{
    public Dictionary<EntityUid, CMUCaveSurveyData> Worlds = new();
    public EntityUid SelectedWorld;
    public Vector2i LastTile;
    public EntityUid LastMap;
    public TimeSpan NextObservation;
    public TimeSpan NextRequest;
    public bool WarnedFull;
    public int LastViewRequest = -1;
    public TimeSpan NextCopy;
    public TimeSpan NextRead;
    public EntityUid? Action;
}
