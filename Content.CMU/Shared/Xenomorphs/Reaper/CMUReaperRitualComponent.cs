using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Xenomorphs.Reaper;

[Serializable, NetSerializable]
public enum CMUReaperRitualKind : byte { Revival, Harvest }

[Serializable, NetSerializable]
public enum CMUReaperCastState : byte { Charging, Succeeded, Cancelled }

/// <summary>Server-owned animation clock. Completion is explicit, never inferred from elapsed time.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUReaperRitualComponent : Component
{
    // Server-only references: the visual must outlive deleted bodies without resolving them in a network state.
    [DataField] public EntityUid Caster;
    [DataField] public EntityUid Target;
    [DataField, AutoNetworkedField] public Vector2 Origin;
    [DataField, AutoNetworkedField] public CMUReaperRitualKind Kind;
    [DataField, AutoNetworkedField] public CMUReaperCastState State;
    [DataField, AutoNetworkedField] public TimeSpan Duration;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartedAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndedAt;
}
