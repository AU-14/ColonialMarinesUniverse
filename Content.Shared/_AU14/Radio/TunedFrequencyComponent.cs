using Content.Shared.Radio;

namespace Content.Shared._AU14.Radio;

[RegisterComponent]
public sealed partial class TunedFrequencyComponent : Component
{
    public RadioFrequency Frequency = RadioFrequency.Off;

    public EntityUid Source = EntityUid.Invalid;
}
