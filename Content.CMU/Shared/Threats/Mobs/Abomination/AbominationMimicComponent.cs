using Robust.Shared.GameStates;

namespace Content.Shared._AU14.Abominations;

/// <summary>
/// Lives on every mimic abomination. Holds the pool of assimilated identities it can
/// transform into and the parameters for the transform.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AbominationMimicComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<AbominationAssimilationProfile> AssimilatedPool = new();

    [DataField, AutoNetworkedField]
    public TimeSpan TransformDuration = TimeSpan.FromSeconds(270);

    /// <summary>
    /// Cooldown between transforms. Stamped onto the transform action entity
    /// via SharedActionsSystem.SetCooldown when a disguise ends — the action's
    /// own per-entity cooldown grays out the button on the player's HUD.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan TransformCooldown = TimeSpan.FromSeconds(300);

    /// <summary>
    /// The transform action entity granted to this mimic. Stored at first use
    /// so we can stamp the post-revert cooldown directly onto it. Per-mimic
    /// by construction — MobStateActions creates one action entity per mob.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? TransformActionEntity;
}
