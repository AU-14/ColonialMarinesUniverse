namespace Content.Server.CMU14.Camera.Television;

/// <summary>
/// A television set that keeps one channel for everyone. Whoever changes the channel changes it for every viewer,
/// and the set stays on it, still repeating what boom mics on that camera hear, until someone changes it again.
/// Its screen can be opened and watched from anywhere the set is on screen and in line of sight, but its buttons
/// only work from next to it.
/// </summary>
[RegisterComponent, Access(typeof(CMUTelevisionSystem))]
public sealed partial class CMUTelevisionComponent : Component
{
    /// <summary>
    /// Half the size, in tiles, of the area a player sees around their eye at 1x zoom. The width follows the
    /// server's widest allowed viewport.
    /// </summary>
    [DataField]
    public float ViewHalfHeight = 7.5f;

    [ViewVariables]
    public EntityUid? Network;

    [ViewVariables]
    public EntityUid? Camera;
}
