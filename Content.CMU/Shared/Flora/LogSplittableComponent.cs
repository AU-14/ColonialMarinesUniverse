// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Flora;

// A log that an axe splits into firewood.
[RegisterComponent]
public sealed partial class LogSplittableComponent : Component
{
    [DataField]
    public List<ProtoId<TagPrototype>> Axes = new() { "FireAxe", "BotanyHatchet", "RUMCAxe" };

    [DataField]
    public EntProtoId Firewood = "RUMCFirewood1";

    // The pieces of firewood a log gives.
    [DataField]
    public int Pieces = 6;
}
