using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Xenomorphs.Reaper;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUReaperRevivalComponent : Component
{
    [DataField, AutoNetworkedField]
    public int FleshCost = 500;

    [DataField, AutoNetworkedField]
    public TimeSpan ChargeTime = TimeSpan.FromSeconds(120);

    [DataField, AutoNetworkedField]
    public TimeSpan MaximumDeathAge = TimeSpan.FromMinutes(3);
}

/// <summary>Reserves a corpse for one ritual so two Reapers cannot spend flesh on it together.</summary>
[RegisterComponent]
public sealed partial class CMUReaperRevivingComponent : Component
{
    public EntityUid Effect;
}
