// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ZLevelBuilding.Pit;

// The do-after of filling a pit in.
[Serializable, NetSerializable]
public sealed partial class PitFillDoAfterEvent : SimpleDoAfterEvent;
