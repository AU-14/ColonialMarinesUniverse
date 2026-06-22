using Content.Shared.Actions;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Formations;

/// <summary>Fired when the formation action is activated. Server opens the BUI.</summary>
public sealed partial class AU14FormationMenuActionEvent : InstantActionEvent { }

[NetSerializable, Serializable]
public enum FormationMenuUiKey : byte
{
    Key
}

/// <summary>
/// Controls how eagerly followers reposition toward their formation slot.
/// </summary>
[NetSerializable, Serializable]
public enum FormationFollowMode : byte
{
    /// <summary>
    /// Followers advance exactly one tile each time the leader moves.
    /// Movement is synchronized and lockstep — nobody gets ahead of the leader.
    /// Best for tight formations on open ground where everyone is already close
    /// to their slot.  Members may lag if the leader moves faster than they can
    /// catch up in one step.
    /// </summary>
    Hold,

    /// <summary>
    /// Followers continuously reposition toward their slot every server tick,
    /// regardless of whether the leader has moved.  Gaps close much faster
    /// after turns or sprints.
    /// Best when members have fallen behind or the formation has complex shapes.
    /// May look slightly less synchronized on flat open ground.
    /// </summary>
    Chase,
}

// ── BUI State ────────────────────────────────────────────────────────────────

[NetSerializable, Serializable]
public sealed class FormationMenuBuiState : BoundUserInterfaceState
{
    public readonly List<FormationPendingDotNet> PendingDots;
    public readonly bool IsInPlanningMode;
    public readonly bool IsPlacingLeaderDot;
    public readonly bool FormationFrozen;
    public readonly int ActiveDotCount;
    public readonly int SlottedCount;
    public readonly bool DebugShowSlots;
    public readonly FormationFollowMode FollowMode;
    public readonly bool CollisionsDisabled;
    public readonly bool ExtendedDotLifetime;

    public FormationMenuBuiState(
        List<FormationPendingDotNet> pendingDots,
        bool isInPlanningMode,
        bool isPlacingLeaderDot,
        bool frozen,
        int activeDots,
        int slotted,
        bool debugShowSlots,
        FormationFollowMode followMode,
        bool collisionsDisabled,
        bool extendedDotLifetime)
    {
        PendingDots = pendingDots;
        IsInPlanningMode = isInPlanningMode;
        IsPlacingLeaderDot = isPlacingLeaderDot;
        FormationFrozen = frozen;
        ActiveDotCount = activeDots;
        SlottedCount = slotted;
        DebugShowSlots = debugShowSlots;
        FollowMode = followMode;
        CollisionsDisabled = collisionsDisabled;
        ExtendedDotLifetime = extendedDotLifetime;
    }
}

[NetSerializable, Serializable]
public sealed class FormationPendingDotNet
{
    public int TileX;
    public int TileY;
    public Direction Facing;
    public bool IsLeaderDot;
}

// ── BUI Messages (client → server) ───────────────────────────────────────────

/// <summary>Client clicked "Place Leader Dot" or "Place Follower Dot" — signals placement mode intent.</summary>
[NetSerializable, Serializable]
public sealed class FormationEnterPlacementMsg : BoundUserInterfaceMessage
{
    public bool IsLeaderDot;
}

/// <summary>Client confirmed placement of one dot at a specific tile and facing.</summary>
[NetSerializable, Serializable]
public sealed class FormationPlaceDotMsg : BoundUserInterfaceMessage
{
    public int TileX;
    public int TileY;
    public Direction Facing;
    public bool IsLeaderDot;
}

/// <summary>Remove the last pending dot (undo).</summary>
[NetSerializable, Serializable]
public sealed class FormationUndoLastDotMsg : BoundUserInterfaceMessage { }

/// <summary>Spawn all pending dots as real entities.</summary>
[NetSerializable, Serializable]
public sealed class FormationConfirmMsg : BoundUserInterfaceMessage { }

/// <summary>Cancel pending placements without spawning anything.</summary>
[NetSerializable, Serializable]
public sealed class FormationCancelPlanningMsg : BoundUserInterfaceMessage { }

/// <summary>Delete all active formation dots placed by this leader.</summary>
[NetSerializable, Serializable]
public sealed class FormationClearMsg : BoundUserInterfaceMessage { }

/// <summary>Clear all dots and force-unslot all followers.</summary>
[NetSerializable, Serializable]
public sealed class FormationDisbandMsg : BoundUserInterfaceMessage { }

/// <summary>Toggle formation movement freeze on/off.</summary>
[NetSerializable, Serializable]
public sealed class FormationFreezeToggleMsg : BoundUserInterfaceMessage { }

/// <summary>Toggle the debug slot-position visualization on/off.</summary>
[NetSerializable, Serializable]
public sealed class FormationDebugToggleMsg : BoundUserInterfaceMessage { }

/// <summary>Toggle whether formation members ignore each other's collisions.</summary>
[NetSerializable, Serializable]
public sealed class FormationCollisionToggleMsg : BoundUserInterfaceMessage { }

/// <summary>Toggle whether newly spawned dots last 15 minutes instead of the default 2 minutes.</summary>
[NetSerializable, Serializable]
public sealed class FormationDotLifetimeToggleMsg : BoundUserInterfaceMessage { }

/// <summary>Set the follower repositioning mode.</summary>
[NetSerializable, Serializable]
public sealed class FormationSetFollowModeMsg : BoundUserInterfaceMessage
{
    public FormationFollowMode Mode;
}
