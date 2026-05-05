using System.Collections.Generic;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUImprovisedSurgeryToolComponent : Component
{
    [DataField, AutoNetworkedField]
    public float DelayMultiplier = 2f;

    [DataField, AutoNetworkedField]
    public float MishapChance = 0.12f;

    [DataField, AutoNetworkedField]
    public string MishapDamageType = "Slash";

    [DataField, AutoNetworkedField]
    public float MishapDamageAmount = 3f;

    /// <summary>
    ///     Default surgical failure penalty for this improvised tool. Most
    ///     substitutes intentionally stay at 0; bad and awful substitutes opt in.
    /// </summary>
    [DataField, AutoNetworkedField]
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
