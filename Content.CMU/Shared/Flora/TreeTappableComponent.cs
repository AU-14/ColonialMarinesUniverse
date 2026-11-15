// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Flora;

// A tree that gives more than wood. A branch can be broken off it. A hole cut in the bark with a knife lets resin run
// out for a minute, and a container held to the tree takes between 10 and 35 of it; the tree makes more only three
// minutes after it was drained.
[RegisterComponent]
public sealed partial class TreeTappableComponent : Component
{
    [DataField]
    public ProtoId<ReagentPrototype> Resin = "RUMCResin";

    [DataField]
    public int MinResin = 10;

    [DataField]
    public int MaxResin = 35;

    // The hole in the bark closes by itself after this long.
    [DataField]
    public TimeSpan HoleLasts = TimeSpan.FromMinutes(1);

    // The resin comes back this long after it was taken.
    [DataField]
    public TimeSpan ResinRegen = TimeSpan.FromMinutes(3);

    [DataField]
    public EntProtoId Branch = "RUMCBranch";

    [DataField]
    public int MaxBranches = 3;

    // A branch grows back this long after it was broken off.
    [DataField]
    public TimeSpan BranchRegen = TimeSpan.FromMinutes(5);

    // Until when the hole is open, and from when the tree has resin again.
    [ViewVariables]
    public TimeSpan? HoleUntil;

    [ViewVariables]
    public TimeSpan ResinReadyAt;

    // The branches left, filled in the first time anybody looks (so a fresh tree has all of them), and when the next grows.
    [ViewVariables]
    public int Branches = -1;

    [ViewVariables]
    public TimeSpan NextBranchAt;
}
