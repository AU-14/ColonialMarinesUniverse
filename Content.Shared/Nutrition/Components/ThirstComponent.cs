using Content.Shared.Alert;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Nutrition.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(ThirstSystem))]
[AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
public sealed partial class ThirstComponent : Component
{
    // Base stuff
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("baseDecayRate")]
    [AutoNetworkedField]
    public float BaseDecayRate = 0.1f;

    [ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float ActualDecayRate;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public ThirstThreshold CurrentThirstThreshold;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public ThirstThreshold LastThirstThreshold;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("startingThirst")]
    [AutoNetworkedField]
    public float CurrentThirst = -1f;

    /// <summary>
    /// The time when the hunger will update next.
    /// </summary>
    [DataField("nextUpdateTime", customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan NextUpdateTime;

    /// <summary>
    /// The time between each update.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public TimeSpan UpdateRate = TimeSpan.FromSeconds(1);

    [DataField("thresholds")]
    [AutoNetworkedField]
    public Dictionary<ThirstThreshold, float> ThirstThresholds = new()
    {
        {ThirstThreshold.OverHydrated, 600.0f},
        {ThirstThreshold.Okay, 450.0f},
        {ThirstThreshold.Thirsty, 300.0f},
        {ThirstThreshold.Parched, 150.0f},
        {ThirstThreshold.Dead, 0.0f},
    };

    [DataField]
    public ProtoId<AlertCategoryPrototype> ThirstyCategory = "Thirst";

    public static readonly Dictionary<ThirstThreshold, ProtoId<AlertPrototype>> ThirstThresholdAlertTypes = new()
    {
        {ThirstThreshold.Thirsty, "Thirsty"},
        {ThirstThreshold.Parched, "Parched"},
        {ThirstThreshold.Dead, "Parched"},
    };

    /// <summary>
    /// Movement speed multiplier applied while <see cref="CurrentThirstThreshold"/> is Thirsty or worse.
    /// How soon this kicks in is entirely a function of <see cref="BaseDecayRate"/> and the gap between the
    /// Okay and Thirsty entries of <see cref="ThirstThresholds"/>.
    /// </summary>
    [DataField]
    public float ThirstySlowdownModifier = 0.9f;

    /// <summary>
    /// Movement speed multiplier applied while <see cref="CurrentThirstThreshold"/> is Parched or worse.
    /// Stacks with <see cref="ThirstySlowdownModifier"/>.
    /// </summary>
    [DataField]
    public float ParchedSlowdownModifier = 0.8f;

    /// <summary>
    /// How often the "you need to drink" reminder popup repeats while Thirsty or worse.
    /// The actual throttle timer is shared with Hunger's reminder via <see cref="NutritionReminderComponent"/>
    /// so the two don't stack in the same tick.
    /// </summary>
    [DataField]
    public TimeSpan ReminderPopupInterval = TimeSpan.FromSeconds(60);
}

[Flags]
public enum ThirstThreshold : byte
{
    // Hydrohomies
    Dead = 0,
    Parched = 1 << 0,
    Thirsty = 1 << 1,
    Okay = 1 << 2,
    OverHydrated = 1 << 3,
}
