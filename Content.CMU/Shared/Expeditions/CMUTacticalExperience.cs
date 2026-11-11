namespace Content.Shared.CMU14.Expeditions;

/// <summary>Anonymous bounded outcome averages, never player identities or hidden positions.</summary>
public sealed class CMUTacticalExperience
{
    public int Samples { get; set; }
    public float FlankReturn { get; set; } = 0.5f;
    public float PeekReturn { get; set; } = 0.5f;

    public void Observe(bool flank, bool success)
    {
        Samples = Math.Min(10000, Samples + 1);
        if (flank)
            FlankReturn = Math.Clamp(FlankReturn * 0.9f + (success ? 0.1f : 0), 0, 1);
        else
            PeekReturn = Math.Clamp(PeekReturn * 0.9f + (success ? 0.1f : 0), 0, 1);
    }

    public float FlankCost => Math.Clamp(1.25f - FlankReturn * 0.5f, 0.75f, 1.25f);
    public float DangerCost => Math.Clamp(1.25f - PeekReturn * 0.5f, 0.75f, 1.25f);
}
