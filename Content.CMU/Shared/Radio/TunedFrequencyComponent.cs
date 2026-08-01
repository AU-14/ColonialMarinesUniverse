namespace Content.Shared._AU14.Radio;

[RegisterComponent]
public sealed partial class TunedFrequencyComponent : Component
{
    public int Frequency = 0;

    public EntityUid Source = EntityUid.Invalid;
}
