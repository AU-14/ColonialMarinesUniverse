using Content.Shared.Radio;

namespace Content.Shared.CMU14.Radio;

[RegisterComponent]
public sealed partial class TunedFrequencyComponent : Component
{
    public RadioFrequency Frequency = RadioFrequency.Off;

    public EntityUid Source = EntityUid.Invalid;
}
