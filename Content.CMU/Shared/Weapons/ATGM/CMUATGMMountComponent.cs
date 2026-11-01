using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Weapons.ATGM;

/// <summary>
///     Marks an A.T.G.M. tripod. Missiles and finders are used on the deployed tripod rather than on
///     the fixed launcher inside it, so loading and linking are handled here.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUATGMSystem))]
public sealed partial class CMUATGMMountComponent : Component
{
    /// <summary>
    ///     How long the loader takes to seat a missile in the launch tube.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan LoadDelay = TimeSpan.FromSeconds(4);

    /// <summary>
    ///     How long the spotter takes to wire a finder to the launcher.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan LinkDelay = TimeSpan.FromSeconds(3);
}
