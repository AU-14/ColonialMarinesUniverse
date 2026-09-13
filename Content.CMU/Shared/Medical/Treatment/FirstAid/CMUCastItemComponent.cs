using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Treatment.FirstAid;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
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

    [DataField, AutoNetworkedField]
    public int Uses = 1;

    [DataField]
    public float PostOpHealMinutes = 5f;

    [DataField]
    public Dictionary<FractureSeverity, float> HealMinutesPerSeverity = new()
    {
        { FractureSeverity.Hairline, 5f },
        { FractureSeverity.Simple, 5f },
    };
}
