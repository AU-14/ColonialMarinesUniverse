using System;
using System.Collections.Generic;
using System.Numerics;

namespace Content.Shared.CMU14.Expeditions;

/// <summary>Ranks short firing steps after the world has checked terrain, allies, and observed danger.</summary>
public static class CMUCombatMovementPolicy
{
    public static bool ShouldStrafe(bool underFire, bool usefulCover, bool committed, bool support,
        double contactAge, double shotAge) => !usefulCover && !committed && (!support || underFire) &&
        contactAge >= (underFire ? 0.35 : 1.25) && shotAge is >= 0 and <= 1.5;

    public static double SettleSeconds(bool underFire, bool support) => support ? 6 : underFire ? 3 : 4.5;

    public static int SelectStep(IReadOnlyList<CMUCombatStepOption> options, Vector2 forward,
        float range, float preferredRange, float minimumRange, float maximumRange, float exposure, float crowding)
    {
        var best = -1;
        var bestScore = 0f;
        var side = new Vector2(-forward.Y, forward.X);
        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            var length = option.Offset.Length();
            var lateral = Math.Abs(Vector2.Dot(option.Offset, side));
            if (!option.Safe || length is < 0.9f or > 1.75f || option.Range < minimumRange ||
                option.Range > maximumRange || option.Exposure > exposure + 0.25f || option.Crowding > crowding + 1 ||
                // Straight back is useful when crowded by the target; otherwise move across its aim.
                lateral < 0.65f && (range >= preferredRange || option.Range < range + 0.4f))
                continue;
            var rangeGain = Math.Abs(range - preferredRange) - Math.Abs(option.Range - preferredRange);
            var score = (exposure - option.Exposure) * 3 + (crowding - option.Crowding) * 1.25f +
                rangeGain * 0.8f + lateral * 0.35f - length * 0.12f;
            if (score <= bestScore)
                continue;
            best = index;
            bestScore = score;
        }
        return best;
    }
}

public readonly record struct CMUCombatStepOption(Vector2 Offset, float Range, float Exposure, float Crowding, bool Safe);
