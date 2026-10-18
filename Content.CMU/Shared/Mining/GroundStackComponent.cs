// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git

namespace Content.Shared.CMU14.Mining;

// A stack of the things that are swept into one pile when they lie near each other on the ground: the stone, the black stone and the
// clay. Whatever lies still for a moment within Range of another stack of the same kind goes into it, up to the most a stack holds.
[RegisterComponent]
public sealed partial class GroundStackComponent : Component
{
    // How far, in tiles, another stack can lie and still be gathered.
    [DataField]
    public float Range = 1f;

    // Seconds it must lie still before it is gathered, so a thing that is thrown or dropped is not swept up in flight.
    [DataField]
    public float Delay = 1.5f;

    // Where it lay when last looked at, and since when. Only the server keeps them.
    public EntityUid LastParent;
    public System.Numerics.Vector2 LastPosition;
    public TimeSpan LyingSince;

    // Set when it has been gathered with what is near it, and cleared when it moves or its count changes.
    public bool Settled;
}
