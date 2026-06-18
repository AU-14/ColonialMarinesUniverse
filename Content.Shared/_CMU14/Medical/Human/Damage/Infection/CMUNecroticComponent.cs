using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Human.Damage.Infection;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUNecroticComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan AppliedAt;
}
