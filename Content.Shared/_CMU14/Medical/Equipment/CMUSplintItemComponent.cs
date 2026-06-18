using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Equipment;

/// <summary>
///     The suppression cap (<see cref="MaxSuppressed"/>) prevents splints from
///     hiding compound or shattered fractures; those need the cast or surgery.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMUOrthopedicEquipmentSystem))]
public sealed partial class CMUSplintItemComponent : Component
{
    [DataField]
    public TimeSpan ApplyDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public FractureSeverity MaxSuppressed = FractureSeverity.Simple;

    [DataField]
    public bool BreakOnDamage = true;

    [DataField]
    public FixedPoint2 BreakDamageThreshold = FixedPoint2.Zero;

    [DataField]
    public SoundSpecifier? ApplySound;

    [DataField]
    public bool ConsumedOnApply = true;

    [DataField]
    public int Uses = 1;
}
