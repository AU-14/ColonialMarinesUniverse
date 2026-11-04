using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.ZLevels.Core.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), UnsavedComponent, Access(typeof(CMUZLevelShootingSystem))]
public sealed partial class CMUZLevelShooterComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool ShootDown;

    // cursor is over the level below through a hole. client syncs it, separate from ShootDown
    // so it can't clobber the blackfoot door gun's manual mode
    [DataField, AutoNetworkedField]
    public bool AimDown;
}
