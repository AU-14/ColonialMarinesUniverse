// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Physics;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Portable;

// Makes a structure carryable: right-click it to pick it up (it comes loose from the floor and takes both hands),
// then click a tile to weld it there. Whoever carries it moves 60 percent slower, and a structure that is not
// welded down does not work.
[RegisterComponent, NetworkedComponent]
public sealed partial class PortableStructureComponent : Component
{
    // Speed of the carrier, as a share of their normal speed.
    [DataField]
    public float SpeedModifier = 0.4f;

    [DataField]
    public TimeSpan PickUpTime = TimeSpan.FromSeconds(2);

    // Remembered while the structure is carried, to put it back the way it was.
    public BodyType? OriginalBodyType;

    public bool AddedItem;

    public bool AddedMultiHanded;

    // Set when carrying it switched the machine's power off, so welding it down knows to switch it on.
    public bool PowerSwitchedOff;
}

// The do-after of picking a portable structure up.
[Serializable, NetSerializable]
public sealed partial class PortablePickUpDoAfterEvent : SimpleDoAfterEvent;

// Raised before a structure is picked up. Whatever makes it a bad idea (a crucible still in the furnace) cancels it.
public sealed class PortablePickUpAttemptEvent(EntityUid user) : CancellableEntityEventArgs
{
    public readonly EntityUid User = user;
}

// Raised on a structure that is being put down, once for each anchored thing already on the tile. A structure that is made to stand on something (a pump on
// water, a drill on a deposit) says so by setting Allowed: that thing is then not in the way.
[ByRefEvent]
public record struct PortablePlaceableOverEvent(EntityUid Other, bool Allowed = false);
