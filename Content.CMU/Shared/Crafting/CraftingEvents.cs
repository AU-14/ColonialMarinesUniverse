// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Crafting;

// The player opened the crafting button and wants to see what they can craft.
[Serializable, NetSerializable]
public sealed class CraftingMenuRequestEvent : EntityEventArgs;

// The recipes the player can craft right now, and whether there is a craft to cancel.
[Serializable, NetSerializable]
public sealed class CraftingMenuEvent : EntityEventArgs
{
    public readonly List<string> Recipes;
    public readonly bool CanCancel;

    public CraftingMenuEvent(List<string> recipes, bool canCancel)
    {
        Recipes = recipes;
        CanCancel = canCancel;
    }
}

// From the player: start crafting this recipe.
[Serializable, NetSerializable]
public sealed class CraftingSelectEvent : EntityEventArgs
{
    public readonly string Recipe;

    public CraftingSelectEvent(string recipe)
    {
        Recipe = recipe;
    }
}

// From the player: stop the craft that is under way.
[Serializable, NetSerializable]
public sealed class CraftingCancelEvent : EntityEventArgs;

// The do-after of crafting; carries the recipe.
[Serializable, NetSerializable]
public sealed partial class CraftDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public string Recipe = string.Empty;

    public CraftDoAfterEvent()
    {
    }

    public CraftDoAfterEvent(string recipe)
    {
        Recipe = recipe;
    }

    public override DoAfterEvent Clone() => new CraftDoAfterEvent(Recipe);
}
