using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 ARCH-605: the Berserker caste, the clan's frontline brute. Added by the Berserker caste prototype.
/// It takes more to put down, takes less from each hit, sprints slower, staggers longer with Bad Blood melee
/// (BadBloodMeleeSystem) and carries no stun bolt (BadBloodEradicatorSystem). BadBloodBerserkerSystem.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodBerserkerComponent : Component
{
    /// <summary>
    /// Added to the critical and dead damage thresholds when the caste is taken up.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ThresholdBonus = 80;

    /// <summary>
    /// Incoming damage is multiplied by this. Healing is not.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DamageTakenMultiplier = 0.85f;

    /// <summary>
    /// Sprint speed is multiplied by this. Walking is not.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SprintMultiplier = 0.9f;

    /// <summary>
    /// A Bad Blood melee stagger from this Berserker lasts this many times as long.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StaggerMultiplier = 1.5f;

    /// <summary>
    /// The threshold bonus has been applied to this body. Keeps it from being applied twice.
    /// </summary>
    [ViewVariables]
    public bool ThresholdsRaised;
}
