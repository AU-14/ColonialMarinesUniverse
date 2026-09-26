using Robust.Shared.Audio;

namespace Content.Shared.Movement.Events;

/// <summary>Raised on the mover before footwear and terrain sounds. A handled null sound is silent.</summary>
[ByRefEvent]
public record struct GetMobFootstepSoundEvent
{
    public bool Handled;
    public SoundSpecifier? Sound;
}
