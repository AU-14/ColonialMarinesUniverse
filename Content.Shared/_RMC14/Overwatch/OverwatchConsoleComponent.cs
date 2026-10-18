using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Overwatch;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
[Access(typeof(SharedOverwatchConsoleSystem))]
public sealed partial class OverwatchConsoleComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity? Squad;

    [DataField, AutoNetworkedField]
    public string? Operator;

    [DataField, AutoNetworkedField]
    public OverwatchLocation? Location;

    [DataField, AutoNetworkedField]
    public bool ShowDead = true;

    [DataField, AutoNetworkedField]
    public bool ShowHidden;

    [DataField, AutoNetworkedField]
    public HashSet<NetEntity> Hidden = new();

    [DataField, AutoNetworkedField]
    public OverwatchSavedLocation?[] SavedLocations = new OverwatchSavedLocation?[3];

    [DataField, AutoNetworkedField]
    public int LastLocation;

    // cmu edit start: orbital bombardment keeps its own saved coordinates (SharedOverwatchConsoleSystem.CMU)
    [DataField, AutoNetworkedField]
    public OverwatchSavedLocation?[] OrbitalSavedLocations = new OverwatchSavedLocation?[3];

    [DataField, AutoNetworkedField]
    public int LastOrbitalLocation;
    // cmu edit end

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan LastMessage;

    [DataField, AutoNetworkedField]
    public TimeSpan MessageCooldown = TimeSpan.FromSeconds(0.5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan LastObjectiveUpdate;

    [DataField, AutoNetworkedField]
    public bool CanMessageSquad = true;

    [DataField, AutoNetworkedField]
    public bool HasOrbital;

    // CMU14 Ship Weapons Begin
    [DataField, AutoNetworkedField]
    public Content.Shared.CMU14.ShipWeapons.CMUShipWeaponStatus[] CMUShipWeapons = [];

    [DataField, AutoNetworkedField]
    public Robust.Shared.Prototypes.ProtoId<Content.Shared.CMU14.ShipWeapons.CMUShipWeaponPrototype>? CMUSelectedShipWeapon;

    [DataField, AutoNetworkedField]
    public Content.Shared.CMU14.ShipWeapons.CMUShipWeaponBearing CMUShipWeaponBearing;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan CMUNextShipWeaponLaunch;
    // CMU14 End

    [DataField, AutoNetworkedField]
    public Vector2i OrbitalCoordinates;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextOrbitalLaunch;

    [DataField]
    public string Group = "UNMC";

    [DataField, AutoNetworkedField]
    public bool CanOrbitalBombardment = true;
}
