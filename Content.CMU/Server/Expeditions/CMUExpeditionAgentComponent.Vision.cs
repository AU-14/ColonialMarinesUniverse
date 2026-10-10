using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    [DataField] public float DarkSightRange = 1.5f;
    [DataField] public float MinimumSightLight = 0.06f;
    [DataField] public TimeSpan FlashMemory = TimeSpan.FromSeconds(1.2);
    public EntityCoordinates? FlashPosition;
    public EntityUid? FlashShooter;
    public TimeSpan FlashUntil;
    public TimeSpan NextFlashObservation;
    public bool FiringAtFlash;
    public int FlashShots;
    public EntityUid? FlareItem;
    public EntityCoordinates? FlareDestination;
    public EntityCoordinates? FlareStartPosition;
    public TimeSpan FlareStarted;
    public TimeSpan FlareReadyAt;
    public TimeSpan FlareUntil;
    public TimeSpan NextFlare;
    public int FlaresUsed;
    public string VisionDecision = "no-contact";
}
