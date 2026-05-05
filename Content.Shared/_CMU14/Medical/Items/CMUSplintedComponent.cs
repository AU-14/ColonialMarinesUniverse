using Content.Shared._CMU14.Medical.Bones;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Items;

/// <summary>
///     The actual fracture data is untouched, so removing the splint restores the
///     underlying severity. Read by <see cref="SharedFractureSystem.GetEffectiveSeverity"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUSplintedComponent : Component
{
    [DataField, AutoNetworkedField]
    public FractureSeverity MaxSuppressed = FractureSeverity.Simple;

    [DataField, AutoNetworkedField]
    public bool BreakOnDamage = true;

    [DataField, AutoNetworkedField]
    public FixedPoint2 BreakDamageThreshold = FixedPoint2.Zero;
}

public sealed class CMUSplintChangedEvent : EntityEventArgs
{
    public CMUSplintChangedEvent(EntityUid part, bool removed)
    {
        Part = part;
        Removed = removed;
    }

    public EntityUid Part { get; }

    public bool Removed { get; }
}
