using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Doors;

/// <summary>
/// Marks a pod-door shutter that is meant to be operated by GOVFOR command staff, but shouldn't be able
/// to permanently trap everyone else if nobody in command shows up. The door automatically locks itself
/// open (and its linked <see cref="RMCDoorButtonComponent"/> stops responding) if:
/// - No GOVFOR Adjutant or Platoon Commander is alive and connected <see cref="CommandCheckTime"/> into the round, or
/// - The door still hasn't been opened by <see cref="FailsafeOpenTime"/> into the round, regardless of the above.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUAdjutantLockDoorComponent : Component
{
    /// <summary>
    /// How far into the round to check whether a GOVFOR Adjutant or Platoon Commander is awake.
    /// If none are found, the door immediately locks open.
    /// </summary>
    [DataField]
    public TimeSpan CommandCheckTime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// If the door still hasn't been opened by this point in the round, it locks open regardless of
    /// whether command is present.
    /// </summary>
    [DataField]
    public TimeSpan FailsafeOpenTime = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Whether <see cref="CommandCheckTime"/> has already been checked, so it only ever fires once.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CommandCheckDone;

    /// <summary>
    /// Whether this door has ever been opened, checked against <see cref="FailsafeOpenTime"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool HasEverOpened;

    /// <summary>
    /// Once true, the door is force-opened, cannot be closed again, and its linked button no longer works.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Locked;
}
