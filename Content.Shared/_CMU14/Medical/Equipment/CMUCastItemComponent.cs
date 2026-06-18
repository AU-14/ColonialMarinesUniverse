using Content.Shared._CMU14.Medical.Foundation;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Equipment;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUOrthopedicEquipmentSystem))]
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
