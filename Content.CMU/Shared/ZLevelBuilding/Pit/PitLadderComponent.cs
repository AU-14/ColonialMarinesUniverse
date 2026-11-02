// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ZLevelBuilding.Pit;

// The lower end of a ladder (or of a ladder under a hatch) that was built in a pit: whoever stands under it can take the ladder and the hatch down. It takes DismantleTime, and the metal that
// the ladder (or the hatch) cost comes back.
[RegisterComponent]
public sealed partial class PitLadderComponent : Component
{
    [DataField]
    public TimeSpan DismantleTime = TimeSpan.FromSeconds(30);
}

// The do-after of taking a ladder down from below.
[Serializable, NetSerializable]
public sealed partial class LadderDismantleDoAfterEvent : SimpleDoAfterEvent
{
}
