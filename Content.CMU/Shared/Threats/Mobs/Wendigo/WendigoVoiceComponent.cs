using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Threats.Mobs.Wendigo;

[RegisterComponent, NetworkedComponent]
public sealed partial class WendigoVoiceComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionWendigoVoice";

    [DataField]
    public EntityUid? ActionEntity;
}
