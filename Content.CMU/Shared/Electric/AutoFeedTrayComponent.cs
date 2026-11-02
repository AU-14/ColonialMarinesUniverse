// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Electric;

// A hydroponics tray that farms itself: it harvests the plant when it is ripe, and plants the same one again (the seed is kept inside).
// It also feeds itself: from the tank joined to it (by a pipe, or poured in by hand) it tops up its water and its
// nutrients whenever they run low, while it has power. The water is the water of the tank, the nutrients the fertilizer the
// fertilizer machine makes.
[RegisterComponent, NetworkedComponent]
public sealed partial class AutoFeedTrayComponent : Component
{
    // The tray is fed when the water or the nutrients are under this share of the most it holds.
    [DataField]
    public float Threshold = 0.7f;

    // How much of the liquid is taken in a second at most.
    [DataField]
    public float WaterPerSecond = 4f;

    [DataField]
    public float FertilizerPerSecond = 1f;

    // How many points of nutrition a unit of fertilizer is worth.
    [DataField]
    public float NutritionPerUnit = 3f;

    [DataField]
    public string FertilizerKind = "RUMCFertilizer";

    // Harvests the plant when it is ripe, and plants the same plant again when the harvest took it or it died.
    [DataField]
    public bool AutoHarvest = true;

    [DataField]
    public bool AutoReplant = true;

    [ViewVariables]
    public float NextFeed;

    // The plant in the tray that was last looked at, what it is, and the copy of its data that the next one is planted with.
    [ViewVariables]
    public EntityUid? Remembered;

    [ViewVariables]
    public EntProtoId? PlantProto;

    [ViewVariables]
    public EntityUid? Snapshot;
}
