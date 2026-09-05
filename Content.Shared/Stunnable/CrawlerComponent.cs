using Robust.Shared.GameStates;

namespace Content.Shared.Stunnable;

/// <summary>
/// This is used to denote that an entity can crawl.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedStunSystem))]
public sealed partial class CrawlerComponent : Component
{
    /// <summary>
    /// Default time we will be knocked down for.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan DefaultKnockedDuration { get; set; } = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// Minimum damage taken to extend our knockdown timer by the default time.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float KnockdownDamageThreshold = 5f;

    /// <summary>
    /// Whether damage delays recovery and interrupts the standing-up action.
    /// RMC mobs recover from knockdown without damage restarting their recovery.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool DamageInterruptsStanding = true;

    /// <summary>
    /// Time it takes us to stand up
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan StandTime = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Base modifier to the maximum movement speed of a knocked down mover.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SpeedModifier = 0.4f;

    /// <summary>
    /// Friction modifier applied to an entity in the downed state.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FrictionModifier = 1f;
}
