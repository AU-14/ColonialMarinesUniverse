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

    [DataField, AutoNetworkedField]
    public TimeSpan NextUpdateTime;

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(2);

    // Local throttle bookkeeping. Move events can be predicted on clients, so this must not be networked or dirtied.
    public TimeSpan LastMoveUpdate;

    [DataField, AutoNetworkedField]
    public TimeSpan LastMoveInterval = TimeSpan.FromSeconds(1);
}
