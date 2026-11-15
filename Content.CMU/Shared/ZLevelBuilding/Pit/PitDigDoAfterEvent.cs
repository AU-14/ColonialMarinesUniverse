// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ZLevelBuilding.Pit;

// The do-after of digging a pit; carries the place and whether it is dug up or down.
[Serializable, NetSerializable]
public sealed partial class PitDigDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public NetCoordinates Location;

    // Whether the pit is dug up into the level above (Location is then a place on that level), and not down. Kept with the dig, so that
    // switching the tool's mode while it is in progress does not change what is checked when it ends.
    [DataField]
    public bool Up;

    public PitDigDoAfterEvent()
    {
    }

    public PitDigDoAfterEvent(NetCoordinates location, bool up = false)
    {
        Location = location;
        Up = up;
    }

    public override DoAfterEvent Clone() => new PitDigDoAfterEvent(Location, Up);
}
