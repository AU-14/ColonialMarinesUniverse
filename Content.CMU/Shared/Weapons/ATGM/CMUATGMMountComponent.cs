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

    /// <summary>
    ///     Whether the tripod has to be anchored to count as deployed. Launchers placed by an equipment deployer,
    ///     such as a gunship hardpoint, are never anchored and only have to be out of storage.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool RequireAnchored = true;
}
