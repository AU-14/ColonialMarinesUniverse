using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Scenario;

[RegisterComponent]
[NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class ScenarioSpawnMarkerCooldownComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(5);

    [AutoNetworkedField]
    public TimeSpan NextAvailableAt = TimeSpan.Zero;
}
