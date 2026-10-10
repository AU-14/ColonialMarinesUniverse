namespace Content.Server.CMU14.Item.HoldUp;

/// <summary>
///     Tracks when a mob may next hold up an item. Added the first time they do.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CMUHoldUpItemSystem))]
public sealed partial class CMUHoldUpItemCooldownComponent : Component
{
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(3);

    [DataField, AutoPausedField]
    public TimeSpan NextHoldUpAt;
}
