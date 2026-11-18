using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

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
    /// CMU14 ECS-703: the Blood Rage action, usable under <see cref="RageHealthThreshold"/> of health.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId RageAction = "CMUActionBadBloodBloodRage";

    [DataField, AutoNetworkedField]
    public EntityUid? RageActionEntity;

    /// <summary>
    /// Blood Rage is refused unless the Berserker has taken at least this share of the damage that would put them
    /// in crit (0.5: under half health).
    /// </summary>
    [DataField]
    public float RageHealthThreshold = 0.5f;

    [DataField]
    public TimeSpan RageDuration = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Bad Blood melee damage is multiplied by this during the rage.
    /// </summary>
    [DataField]
    public float RageMeleeMultiplier = 1.25f;

    /// <summary>
    /// How much the raging Berserker counts toward seizing a Hunt Ground.
    /// </summary>
    [DataField]
    public float RageContestWeight = 2f;

    /// <summary>
    /// D16: played at the Berserker for everyone in range when the rage starts.
    /// </summary>
    [DataField]
    public SoundSpecifier? RageSound = new SoundCollectionSpecifier("CMUYautjaRoars");

    [DataField]
    public Color RageLightColor = Color.FromHex("#c41e1e");

    [DataField]
    public float RageLightRadius = 2.5f;

    /// <summary>
    /// The threshold bonus has been applied to this body. Keeps it from being applied twice.
    /// </summary>
    [ViewVariables]
    public bool ThresholdsRaised;
}
