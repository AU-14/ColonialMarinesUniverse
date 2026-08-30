using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Injuries.Wounds;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUNecroticComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan AppliedAt;
}
