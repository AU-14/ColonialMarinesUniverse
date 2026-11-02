// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Crafting;

// A recipe in the crafting radial menu. Picking it crafts the thing on its own; the menu only shows the recipes
// whose skills the crafter has and whose consumables they are carrying.
[Prototype("craftRecipe")]
public sealed partial class CraftingRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name { get; private set; }

    // The icon of the menu button; the result is shown when this is not set.
    [DataField]
    public SpriteSpecifier? Icon { get; private set; }

    [DataField(required: true)]
    public EntProtoId Output { get; private set; }

    [DataField]
    public int OutputCount { get; private set; } = 1;

    [DataField]
    public TimeSpan Time { get; private set; } = TimeSpan.FromSeconds(3);

    // The games that are played one after another instead of waiting out the time. How well they go (the average of them) decides how well the craft turns out.
    [DataField("minigames")]
    public List<CraftMinigameKind> Minigames { get; private set; } = new();

    // The first of the games, or None when the craft is only waited for.
    public CraftMinigameKind Minigame => Minigames.Count > 0 ? Minigames[0] : CraftMinigameKind.None;

    // Skills the crafter needs, with the level of each.
    [DataField]
    public Dictionary<EntProtoId<SkillDefinitionComponent>, int> Skills { get; private set; } = new();

    // Tools the crafter must be carrying. They are never used up.
    [DataField]
    public List<ProtoId<ToolQualityPrototype>> Tools { get; private set; } = new();

    // Used up when the craft is done.
    [DataField]
    public List<CraftConsumable> Consumables { get; private set; } = new();
}

// One ingredient of a craft that is used up: an item or a stack, and how many.
[DataDefinition]
public sealed partial class CraftConsumable
{
    // A single entity of this prototype...
    [DataField]
    public EntProtoId? Item;

    // ...or an amount taken out of any stack of this type.
    [DataField]
    public ProtoId<StackPrototype>? Stack;

    [DataField]
    public int Amount = 1;
}
