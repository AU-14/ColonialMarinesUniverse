using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Antags.GovForVeteran;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVeteranCloakComponent : Component
{
    /// <summary>
    ///     Mobs within this range that have line of sight count as watching him. A 1080p client at the default
    ///     eye zoom sees about 15 tiles across and 8.5 tall, so 12 covers anything a watcher can actually look at
    ///     without stretching to the map-wide distances an observer cannot perceive anyway.
    /// </summary>
    [DataField]
    public float SightRange = 12f;

    /// <summary>
    ///     How far the reveal message carries when the field drops.
    /// </summary>
    [DataField]
    public float RevealRange = 14f;

    /// <summary>
    ///     Interaction, attack and throwing gates stay shut this long after the field drops.
    /// </summary>
    [DataField]
    public TimeSpan UncloakWeaponLock = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public TimeSpan UncloakLockUntil;

    [DataField]
    public TimeSpan DeniedPopupCooldown = TimeSpan.FromSeconds(2);

    /// <summary>
    ///     Rate limit for the "not while you are cloaked" popups. Attempt events fire continuously.
    /// </summary>
    [DataField]
    public TimeSpan NextDeniedPopup;

    /// <summary>
    ///     True while the field is up. Tracked here rather than inferred from the invisibility component, which is
    ///     still attached while its own shutdown runs - that would leave the speed bonus applied after the field drops.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Cloaked;

    /// <summary>
    ///     Walk and sprint speed multiplier applied while the field is up.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CloakSpeedMultiplier = 1.5f;

    /// <summary>
    ///     Items hidden together with him (whatever he was holding when the field went up).
    /// </summary>
    [DataField]
    public List<EntityUid> HiddenItems = new();

    /// <summary>
    ///     Collision masks replaced while the field is up, keyed by fixture id. Server-side restore data.
    /// </summary>
    [DataField]
    public Dictionary<string, int> OriginalMasks = new();

    /// <summary>
    ///     Whether the door-bump tag was taken off him and still needs restoring.
    /// </summary>
    [DataField]
    public bool RemovedDoorBumpTag;

    /// <summary>
    ///     Set while the component is shutting down, so cleanup does not fire the reveal message.
    /// </summary>
    public bool Cleaning;
}
