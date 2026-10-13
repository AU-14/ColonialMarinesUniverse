// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ZLevelBuilding.Pit;

[Serializable, NetSerializable]
public sealed partial class PitBuildDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public PitBuildOption Option;

    public PitBuildDoAfterEvent()
    {
    }

    public PitBuildDoAfterEvent(PitBuildOption option)
    {
        Option = option;
    }

    public override DoAfterEvent Clone() => new PitBuildDoAfterEvent(Option);
}
