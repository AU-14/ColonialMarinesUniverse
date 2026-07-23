namespace Content.Client._CMU14.ZLevels.Core;

/// <summary>
/// Client-only ownership state for an active <c>CMUZPhysics</c> sprite offset.
/// </summary>
[RegisterComponent, Access(typeof(CMUClientZLevelsSystem))]
public sealed partial class CMUZPhysicsVisualComponent : Component
{
    internal CMUZPhysicsSpriteState Baseline;
    internal CMUZPhysicsSpriteState AppliedState;
    internal bool Applied;
}
