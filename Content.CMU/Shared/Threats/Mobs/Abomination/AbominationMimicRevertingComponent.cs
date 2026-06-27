using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CMU14.Threats.Mobs.Abomination;

/// <summary>
///     Applied to a disguised mimic while it's actively reverting back to its
///     combat form. During this phase it shakes and screams; once the timer
///     elapses AbominationMimicSystem polymorphs it back into the mimic entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AbominationMimicRevertingComponent : Component
{
    /// <summary>
    ///     How long the shake/scream/knockdown sequence runs before the
    ///     polymorph revert actually fires.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan JitterDuration = TimeSpan.FromSeconds(7);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan RevertAt;
}

public sealed partial class AbominationMimicRevertActionEvent : InstantActionEvent;
