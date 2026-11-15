// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Forge;

// Two stones to strike sparks from: held to a fire that is not lit (a campfire, a stove) it lights it, held to a thing that burns it sets it on fire. A strike does not always catch.
[RegisterComponent]
public sealed partial class FlintStrikerComponent : Component
{
    // The seconds a strike takes, and the share of the strikes that catch.
    [DataField]
    public float Seconds = 2.5f;

    [DataField]
    public float Chance = 0.7f;

    // The fire stacks put on a thing that burns, to set it alight.
    [DataField]
    public float FireStacks = 2f;
}

[Serializable, NetSerializable]
public sealed partial class FlintStrikeDoAfterEvent : SimpleDoAfterEvent;
