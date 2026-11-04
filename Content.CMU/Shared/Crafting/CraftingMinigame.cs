// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Crafting;

// The game that is played instead of waiting for a craft. None: the craft is only waited for.
[Serializable, NetSerializable]
public enum CraftMinigameKind : byte
{
    None,

    // A marker sweeps over a bar: press when it is in the zone, a few times.
    Rhythm,

    // A marker falls and rises while the mouse is held: keep it in a zone that drifts.
    Hold,

    // A dot runs along a winding track: keep the cursor with it.
    Trace,

    // The cursor is rubbed back and forth over a dull surface until it is clean.
    Polish,

    // Two crosshairs circle at different speeds: press when they lie on one another.
    Center,

    // A crack runs round the bit: keep the cursor on it.
    Drill,

    // A needle runs along a seam: press at each hole.
    Stitch,
}

// What came of a game, from its score (0 to 1).
public enum CraftOutcome : byte
{
    // The work is spoilt: half of the materials are gone and nothing is made.
    Failed,

    // As if the craft was only waited for.
    Normal,

    // Better than usual: one more of the product, or a bit of the materials saved.
    Good,
}

public static class CraftMinigames
{
    public const float FailBelow = 0.25f;
    public const float GoodFrom = 0.85f;

    public static CraftOutcome OutcomeOf(float score)
    {
        if (score < FailBelow)
            return CraftOutcome.Failed;

        return score >= GoodFrom ? CraftOutcome.Good : CraftOutcome.Normal;
    }

    // The same number every time for the same text, so that a recipe has the same track and the same zones for everyone and always.
    public static int StableHash(string text)
    {
        unchecked
        {
            var hash = (uint) 2166136261;
            foreach (var c in text)
                hash = (hash ^ c) * 16777619;

            return (int) (hash & 0x7fffffff);
        }
    }
}
