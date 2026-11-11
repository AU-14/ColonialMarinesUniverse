using Content.Shared._RMC14.AlertLevel; // CMU14: mode targets an alert level
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Keycard;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(KeycardDeviceSystem))]
public sealed partial class KeycardDeviceComponent : Component
{
    // CMU14: was KeycardDeviceMode Mode = KeycardDeviceMode.None
    [DataField, AutoNetworkedField]
    public RMCAlertLevels? Mode;

    [DataField, AutoNetworkedField]
    public float Range = 10;

    [DataField, AutoNetworkedField]
    public TimeSpan Time = TimeSpan.FromSeconds(2);

    // CMU14: buffer between alert steps so announcements can play out
    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(1);

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan LastActivated;

    // CMU14: last completed alert step, stamped on the whole device cluster
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? LastStep;
}
