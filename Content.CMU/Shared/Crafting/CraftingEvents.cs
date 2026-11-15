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

// A recipe with a game: the server has seen that the craft can be made and asks the player to play. Nothing is used up until the result comes back.
[Serializable, NetSerializable]
public sealed class CraftingMinigameStartEvent : EntityEventArgs
{
    public readonly string Recipe;
    public readonly CraftMinigameKind Kind;

    // The same for the same recipe: the shape of the track, the places of the zones.
    public readonly int Seed;

    // The number of presses (the rhythm) or the number of the seconds (the other games).
    public readonly int Steps;
    public readonly float Duration;

    // How wide the zones are, as a share of the usual: the skilled have it more.
    public readonly float Ease;

    // Which of the games of the recipe this is, counted from 0, and how many there are.
    public readonly int Stage;
    public readonly int Stages;

    public CraftingMinigameStartEvent(string recipe, CraftMinigameKind kind, int seed, int steps, float duration, float ease, int stage = 0, int stages = 1)
    {
        Stage = stage;
        Stages = stages;
        Recipe = recipe;
        Kind = kind;
        Seed = seed;
        Steps = steps;
        Duration = duration;
        Ease = ease;
    }
}

// How the game went, from 0 to 1. The server does not take this on trust blindly: a game that is over faster than it can be played counts for nothing.
[Serializable, NetSerializable]
public sealed class CraftingMinigameResultEvent : EntityEventArgs
{
    public readonly string Recipe;
    public readonly float Score;

    public CraftingMinigameResultEvent(string recipe, float score)
    {
        Recipe = recipe;
        Score = score;
    }
}

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
