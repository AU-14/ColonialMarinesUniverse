using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CMU14.Medical.Stabilizers;

[RegisterComponent]
public sealed partial class CMUTraumaGovernorAttachmentComponent : Component
{
}

[RegisterComponent]
public sealed partial class CMUTraumaGovernorVialComponent : Component
{
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedCMUTraumaGovernorSystem))]
public sealed partial class CMUTraumaGovernorComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId ActionId = "CMUActionTraumaGovernor";

    [DataField, AutoNetworkedField]
    public EntityUid? Action;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(4);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextUse;

    [DataField, AutoNetworkedField]
    public bool HasInternalCharge = true;

    [DataField, AutoNetworkedField]
    public bool VialLoaded;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedCMUTraumaGovernorSystem))]
public sealed partial class CMUOrganStabilizedComponent : Component
{
    [DataField, AutoNetworkedField]
    public CMUOrganStabilizerTarget Target;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
}

public sealed partial class CMUTraumaGovernorActionEvent : InstantActionEvent;
