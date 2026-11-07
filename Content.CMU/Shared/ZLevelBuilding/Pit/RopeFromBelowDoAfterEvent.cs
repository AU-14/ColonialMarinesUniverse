// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ZLevelBuilding.Pit;

// The do-after of hanging a rope up into a pit from below; carries the pit.
[Serializable, NetSerializable]
public sealed partial class RopeFromBelowDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public NetEntity Pit;

    public RopeFromBelowDoAfterEvent()
    {
    }

    public RopeFromBelowDoAfterEvent(NetEntity pit)
    {
        Pit = pit;
    }

    public override DoAfterEvent Clone() => new RopeFromBelowDoAfterEvent(Pit);
}
