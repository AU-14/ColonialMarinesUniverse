namespace Content.Server._CMU14.Medical.Core;

[RegisterComponent]
public sealed partial class CMUAnesthesiaStateComponent : Component
{
    public bool DrowsinessApplied;

    public uint InductionId;

    public bool SleepApplied;

    public bool WasSleeping;
}
