using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Nutrition.Components;

/// <summary>
/// Shared throttle so the Thirst and Hunger "you need to eat/drink" reminder popups don't stack on top of
/// each other in the same tick - e.g. for GOVFOR troops who spawn already needing both breakfast and water.
/// Whichever of <see cref="ThirstSystem"/>/<see cref="HungerSystem"/> pops a reminder first claims this
/// timer, pushing the other one's next reminder back.
/// </summary>
[RegisterComponent, Access(typeof(ThirstSystem), typeof(HungerSystem))]
[AutoGenerateComponentPause]
public sealed partial class NutritionReminderComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextReminderPopupTime;
}
