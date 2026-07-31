using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;

namespace Content.Shared.Nutrition.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(HungerSystem))]
[AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
public sealed partial class HungerComponent : Component
{
    /// <summary>
    /// The hunger value as authoritatively set by the server as of <see cref="LastAuthoritativeHungerChangeTime"/>.
    /// This value should be updated relatively infrequently. To get the current hunger, which changes with each update,
    /// use <see cref="HungerSystem.GetHunger"/>.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    [AutoNetworkedField]
    public float LastAuthoritativeHungerValue;

    /// <summary>
    /// The time at which <see cref="LastAuthoritativeHungerValue"/> was last updated.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public TimeSpan LastAuthoritativeHungerChangeTime;

    /// <summary>
    /// The base amount at which <see cref="LastAuthoritativeHungerValue"/> decays.
    /// </summary>
    /// <remarks>Any time this is modified, <see cref="HungerSystem.SetAuthoritativeHungerValue"/> should be called.</remarks>
    [DataField("baseDecayRate"), ViewVariables(VVAccess.ReadWrite)]
    public float BaseDecayRate = 0.01666666666f;

    /// <summary>
    /// The actual amount at which <see cref="LastAuthoritativeHungerValue"/> decays.
    /// Affected by <seealso cref="CurrentThreshold"/>
    /// </summary>
    /// <remarks>Any time this is modified, <see cref="HungerSystem.SetAuthoritativeHungerValue"/> should be called.</remarks>
    [DataField("actualDecayRate"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float ActualDecayRate;

    /// <summary>
    /// The last threshold this entity was at.
    /// Stored in order to prevent recalculating
    /// </summary>
    [DataField("lastThreshold"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public HungerThreshold LastThreshold;

    /// <summary>
    /// The current hunger threshold the entity is at
    /// </summary>
    /// <remarks>Any time this is modified, <see cref="HungerSystem.SetAuthoritativeHungerValue"/> should be called.</remarks>
    [DataField("currentThreshold"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public HungerThreshold CurrentThreshold;

    /// <summary>
    /// A dictionary relating HungerThreshold to the amount of <see cref="HungerSystem.GetHunger">current hunger</see> needed for each one
    /// </summary>
    [DataField("thresholds", customTypeSerializer: typeof(DictionarySerializer<HungerThreshold, float>))]
    [AutoNetworkedField]
    public Dictionary<HungerThreshold, float> Thresholds = new()
    {
        { HungerThreshold.Overfed, 200.0f },
        { HungerThreshold.Okay, 150.0f },
        { HungerThreshold.Peckish, 100.0f },
        { HungerThreshold.Starving, 50.0f },
        { HungerThreshold.Dead, 0.0f }
    };

    /// <summary>
    /// A dictionary relating hunger thresholds to corresponding alerts.
    /// </summary>
    [DataField("hungerThresholdAlerts")]
    [AutoNetworkedField]
    public Dictionary<HungerThreshold, ProtoId<AlertPrototype>> HungerThresholdAlerts = new()
    {
        { HungerThreshold.Peckish, "Peckish" },
        { HungerThreshold.Starving, "Starving" },
        { HungerThreshold.Dead, "Starving" }
    };

    [DataField]
    public ProtoId<AlertCategoryPrototype> HungerAlertCategory = "Hunger";

    /// <summary>
    /// A dictionary relating HungerThreshold to how much they modify <see cref="BaseDecayRate"/>.
    /// </summary>
    [DataField("hungerThresholdDecayModifiers", customTypeSerializer: typeof(DictionarySerializer<HungerThreshold, float>))]
    [AutoNetworkedField]
    public Dictionary<HungerThreshold, float> HungerThresholdDecayModifiers = new()
    {
        { HungerThreshold.Overfed, 1.2f },
        { HungerThreshold.Okay, 1f },
        { HungerThreshold.Peckish, 0.8f },
        { HungerThreshold.Starving, 0.6f },
        { HungerThreshold.Dead, 0.6f }
    };

    /// <summary>
    /// The amount of slowdown applied when an entity is starving
    /// </summary>
    [DataField("starvingSlowdownModifier"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float StarvingSlowdownModifier = 0.75f;

    /// <summary>
    /// Damage dealt when your current threshold is at HungerThreshold.Dead
    /// </summary>
    [DataField("starvationDamage")]
    public DamageSpecifier? StarvationDamage;

    /// <summary>
    /// The time when the hunger threshold will update next.
    /// </summary>
    [DataField("nextUpdateTime", customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan NextThresholdUpdateTime;

    /// <summary>
    /// The time between each hunger threshold update.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public TimeSpan ThresholdUpdateRate = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Movement speed multiplier applied while <see cref="CurrentThreshold"/> is Peckish or worse.
    /// How soon this kicks in is entirely a function of <see cref="BaseDecayRate"/> and the gap between the
    /// Okay and Peckish entries of <see cref="Thresholds"/>. Stacks with <see cref="StarvingSlowdownModifier"/>.
    /// </summary>
    [DataField]
    public float PeckishSlowdownModifier = 0.9f;

    /// <summary>
    /// How often the "you need to eat" reminder popup repeats while Peckish or worse.
    /// The actual throttle timer is shared with Thirst's reminder via <see cref="NutritionReminderComponent"/>
    /// so the two don't stack in the same tick.
    /// </summary>
    [DataField]
    public TimeSpan ReminderPopupInterval = TimeSpan.FromSeconds(60);
}

[Serializable, NetSerializable]
public enum HungerThreshold : byte
{
    Overfed = 1 << 3,
    Okay = 1 << 2,
    Peckish = 1 << 1,
    Starving = 1 << 0,
    Dead = 0,
}
