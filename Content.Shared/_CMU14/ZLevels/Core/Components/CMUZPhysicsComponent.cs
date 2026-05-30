using System.Numerics;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._CMU14.ZLevels.Core.Components;

/// <summary>
/// Allows an entity to move up and down the z-levels by gravity or jumping
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true),
 Access(typeof(CMUSharedZLevelsSystem))]
public sealed partial class CMUZPhysicsComponent : Component
{
    /// <summary>
    /// The current speed of movement between z-levels.
    /// If greater than 0, the entity moves upward. If less than 0, the entity moves downward.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Velocity;

    /// <summary>
    /// The current height of the entity within the current Z-level.
    /// Takes values from 0 to 1. If the value rises above 1, the entity moves up to the next level and the value is normalized.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LocalPosition;

    // Physics

    [DataField, AutoNetworkedField]
    public float Bounciness = 0.3f;

    // Visuals

    /// <summary>
    /// Used only by the client.
    /// Blocks the rotation of an object if it has <see cref="LocalPosition"/> > 0,
    /// and saves the original NoRot value in SpriteComponent here so that it can be restored in the future.
    /// </summary>
    [DataField]
    public bool NoRotDefault;

    /// <summary>
    /// The original DrawDepth of the object is automatically saved here. Increases by 1 when the creature has <see cref="LocalPosition"/> > 0
    /// </summary>
    [DataField]
    public int DrawDepthDefault;

    /// <summary>
    /// When the mapinit entity is created, its initial Sprite Offset value is written here in order to apply an offset based on the Z position relative to this value.
    /// </summary>
    [DataField]
    public Vector2 SpriteOffsetDefault = Vector2.Zero;

    public EntityUid LastFallCheckMap = EntityUid.Invalid;

    public Vector2i LastFallCheckTile;
}
