// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Farming;

// A spawner that every so often puts one of its things near itself, as long as there are fewer of them around than the limit. A place where mushrooms grow.
[RegisterComponent]
public sealed partial class PeriodicSpawnerComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> Prototypes = new();

    // How often it tries, in seconds; the first try comes after a random part of that.
    [DataField]
    public float Interval = 180f;

    // How far from itself the things appear, and how far it counts what has already grown.
    [DataField]
    public float Radius = 3f;

    // At most this many of the things around it.
    [DataField]
    public int MaxNearby = 6;

    // The chance that a try gives a thing.
    [DataField]
    public float Chance = 0.75f;

    [ViewVariables]
    public TimeSpan NextAt;
}
