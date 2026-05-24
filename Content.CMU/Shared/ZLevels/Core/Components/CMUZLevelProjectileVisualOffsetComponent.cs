using System.Numerics;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.ZLevels.Core.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), UnsavedComponent, Access(typeof(CMUZLevelShootingSystem))]
public sealed partial class CMUZLevelProjectileVisualOffsetComponent : Component
{
    [DataField, AutoNetworkedField]
    public Vector2 Offset;
}
