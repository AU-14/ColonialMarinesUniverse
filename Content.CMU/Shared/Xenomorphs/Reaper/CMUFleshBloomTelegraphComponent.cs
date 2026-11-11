using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Xenomorphs.Reaper;

/// <summary>The cast's origin and clock, shared with everyone watching the tendril grow.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUFleshBloomTelegraphComponent : Component
{
    // Server-only lifecycle owner; clients draw from the captured origin even after it is deleted.
    [DataField]
    public EntityUid Caster;

    [DataField, AutoNetworkedField]
    public CMUReaperCastState State;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndedAt;

    /// <summary>Reaper position relative to the bloom center, in the target grid's axes.</summary>
    [DataField, AutoNetworkedField]
    public Vector2 Origin;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartedAt;

    [DataField, AutoNetworkedField]
    public TimeSpan Duration;
}
