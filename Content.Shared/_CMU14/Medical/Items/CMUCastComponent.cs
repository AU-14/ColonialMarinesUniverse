using Content.Shared._CMU14.Medical.Bones;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Items;

/// <summary>
///     Applied-time fields use <see cref="AutoPausedField"/> so the heal countdown
///     survives round-pauses.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUCastComponent : Component
{
    [DataField, AutoNetworkedField]
    public FractureSeverity MaxSuppressed = FractureSeverity.Simple;

    [DataField, AutoNetworkedField]
    public bool ImmobilizesLimb = true;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan AppliedAt;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan HealCompletesAt;

    [DataField, AutoNetworkedField]
    public bool ReadyToRemove;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan NextRemovePrompt;
}

[ByRefEvent]
public readonly record struct CMUCastChangedEvent(EntityUid Part, bool Removed);
