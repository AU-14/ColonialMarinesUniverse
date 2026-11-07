using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Clock;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUMilitaryClockComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan StartTime = TimeSpan.FromHours(4);
}
