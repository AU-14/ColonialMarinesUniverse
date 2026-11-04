// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Damage;

namespace Content.Shared.CMU14.ZLevelBuilding.Pit;

// Spikes set on the floor with their points up: whoever falls onto them (from a pit, from a level above) is run through, and the higher the fall the worse. A fall of a level
// (the one a pit makes) does the damage that is written here, a fall of two levels twice that, and so on; every part of the body is wounded, not one.
[RegisterComponent]
public sealed partial class FallSpikesComponent : Component
{
    // The damage of the fall of one level, in all.
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    // The impact of the fall of one level (the speed at which a fall of one level meets the floor: the square root of twice the gravity of the levels by the height of a level).
    [DataField]
    public float BaseImpact = 4.43f;

    // The most the damage grows to, as a share of the base one, so that a fall from the end of the world is not a certain death many times over.
    [DataField]
    public float MaxMultiplier = 5f;

    // How near to the stakes the one who fell must land, in tiles.
    [DataField]
    public float Range = 0.55f;
}
