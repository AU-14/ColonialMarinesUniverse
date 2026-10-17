using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Threats.Mobs.WorkingJoe;

[RegisterComponent, NetworkedComponent]
public sealed partial class WorkingJoeVoiceComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionWorkingJoeVoice";

    [DataField]
    public EntityUid? ActionEntity;
}
