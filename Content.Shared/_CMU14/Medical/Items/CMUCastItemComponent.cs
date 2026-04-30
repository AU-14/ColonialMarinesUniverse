using Content.Shared._CMU14.Medical.Bones;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Items;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUSplintItemSystem))]
public sealed partial class CMUCastItemComponent : Component
{
    [DataField]
    public TimeSpan ApplyDelay = TimeSpan.FromSeconds(6);

    [DataField]
    public FractureSeverity MaxSuppressed = FractureSeverity.Simple;

    [DataField]
    public SoundSpecifier? ApplySound;

    [DataField]
    public bool ConsumedOnApply = true;

    [DataField]
    public Dictionary<FractureSeverity, float> HealMinutesPerSeverity = new()
    {
        { FractureSeverity.Hairline, 5f },
        { FractureSeverity.Simple, 15f },
    };
}
