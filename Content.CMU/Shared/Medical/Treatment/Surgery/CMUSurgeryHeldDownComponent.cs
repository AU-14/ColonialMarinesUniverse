using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Medical.Treatment.Surgery;

/// <summary>
///     On a surgery patient while someone else holds them down. Surgery steps on a held patient never fail.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUSurgeryHeldDownComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Holder;

    /// <summary>
    ///     The holder lets go once they get further than this from the patient.
    /// </summary>
    [DataField]
    public float MaxRange = 1.5f;
}

[Serializable, NetSerializable]
public sealed partial class CMUSurgeryHoldDownDoAfterEvent : SimpleDoAfterEvent;
