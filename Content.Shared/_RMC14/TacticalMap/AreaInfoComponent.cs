using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.TacticalMap;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedTacticalMapSystem), typeof(AreaInfoSystem))]
public sealed partial class AreaInfoComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<AlertPrototype> Alert = "AreaInfo";

    /// <summary>Change through <see cref="AreaInfoSystem.SetNextUpdateTime"/> to update the schedule.</summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    [Access(typeof(AreaInfoSystem))]
    public TimeSpan NextUpdateTime;

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(2);

    // Local throttle bookkeeping. Move events can be predicted on clients, so this must not be networked or dirtied.
    public TimeSpan LastMoveUpdate;

    // Local presentation cache; repeated refreshes must still restore an alert if it was cleared.
    public (string AreaName, short CeilingLevel, string Restrictions)? LastPresentation;
    public string? LastMessage;

    // Cache formatting inputs, never permissions: the system still evaluates live state.
    public AreaInfoRestrictionState? LastRestrictionState;
    public string? LastRestrictionText;

    [DataField, AutoNetworkedField]
    public TimeSpan LastMoveInterval = TimeSpan.FromSeconds(1);
}
