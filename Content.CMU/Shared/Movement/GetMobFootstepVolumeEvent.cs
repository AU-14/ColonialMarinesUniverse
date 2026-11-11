namespace Content.Shared.CMU14.Movement;

// raised on the mover right before a footstep plays, add to Modifier (dB) to make it louder or quieter
[ByRefEvent]
public record struct GetMobFootstepVolumeEvent
{
    public float Modifier;
}
