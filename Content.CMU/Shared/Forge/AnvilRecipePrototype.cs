// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Forge;

// A recipe of the anvil: what it makes, how many, and the tool qualities of the strikes (hammering, chiseling) in order.
[Prototype]
public sealed partial class AnvilRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name { get; private set; }

    [DataField(required: true)]
    public SpriteSpecifier Icon { get; private set; } = default!;

    [DataField(required: true)]
    public EntProtoId Output { get; private set; }

    [DataField]
    public int OutputCount { get; private set; } = 1;

    [DataField(required: true)]
    public List<ProtoId<ToolQualityPrototype>> Steps { get; private set; } = new();

    // The ingot the recipe is for, when it is for one kind only (the mold of brass is forged of brass); any ingot when not set.
    [DataField]
    public ProtoId<StackPrototype>? Ingot { get; private set; }
}
