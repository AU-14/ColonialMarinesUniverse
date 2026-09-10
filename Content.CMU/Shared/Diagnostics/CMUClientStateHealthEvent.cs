using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Diagnostics;

/// <summary>Small, untrusted diagnostic sample. Never used to control state delivery or gameplay.</summary>
[Serializable, NetSerializable]
public sealed class CMUClientStateHealthEvent : EntityEventArgs
{
    public GameTick AppliedTick;
    public double AppliedAgeSeconds;
    public int BufferedStates;
    public int TargetBuffer;
    /// <summary>Average FPS, or -1 when frame timing samples are unavailable.</summary>
    public double AverageFps;
}
