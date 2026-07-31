using System.Collections.Generic;

namespace Content.Shared._CMU14.Medical.Treatment.Surgery;

[RegisterComponent]
public sealed partial class CMUImprovisedSurgeryToolComponent : Component
{
    [DataField]
    public float DelayMultiplier = 2f;

    [DataField]
    public float MishapChance = 0.12f;

    [DataField]
    public string MishapDamageType = "Slash";

    [DataField]
    public float MishapDamageAmount = 3f;

    /// <summary>
    ///     Default surgical failure penalty for this improvised tool. Most
    ///     substitutes intentionally stay at 0; bad and awful substitutes opt in.
    /// </summary>
    [DataField]
    public int FailurePenalty;

    /// <summary>
    ///     Optional per-step-category overrides, e.g. a knife can be a fine
    ///     scalpel substitute but awful at retracting skin.
    /// </summary>
    [DataField]
    public Dictionary<string, int> FailurePenalties = new();

    public int GetFailurePenalty(string? category)
    {
        if (category is not null && FailurePenalties.TryGetValue(category, out var penalty))
            return penalty;

        return FailurePenalty;
    }
}
