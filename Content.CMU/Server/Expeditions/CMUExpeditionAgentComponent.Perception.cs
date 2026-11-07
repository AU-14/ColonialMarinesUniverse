using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    [DataField] public float ImmediateRecognitionRange = 5;
    [DataField] public TimeSpan RecognitionTime = TimeSpan.FromSeconds(0.6);
    [DataField] public int SightChecksPerThink = 8;
    [DataField] public TimeSpan ContactSearchDuration = TimeSpan.FromSeconds(12);
    [DataField] public TimeSpan ContactInspectDuration = TimeSpan.FromSeconds(1.5);
    public readonly Dictionary<EntityUid, CMUContactRecognition> ContactRecognition = new();
    public int ContactScanCursor;
    public int SightChecksLastThink;
    public EntityCoordinates? ContactSearchPoint;
    public TimeSpan ContactSearchObservedAt;
    public TimeSpan ContactSearchUntil;
    public TimeSpan? ContactInspectUntil;
    public bool SearchingSound;
}

public struct CMUContactRecognition
{
    public TimeSpan LastObserved;
    public TimeSpan RecognizedUntil;
    public double Exposure;
}
