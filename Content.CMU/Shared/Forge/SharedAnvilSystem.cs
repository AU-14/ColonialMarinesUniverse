// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Tools.Systems;

namespace Content.Shared.CMU14.Forge;

// The anvil, shared by both sides: checks a workpiece fits a recipe and reacts when a tool is used on it.
public abstract class SharedAnvilSystem : EntitySystem
{
    [Dependency] private readonly SharedToolSystem _tool = default!;

    public static readonly string[] AnvilQualities = { "Hammering", "Chiseling" };

    // A recipe for one kind of ingot only fits a workpiece of that kind; the others fit any.
    public bool RecipeFits(AnvilRecipePrototype recipe, EntityUid workpiece)
    {
        if (recipe.Ingot is not { } ingot)
            return true;

        return TryComp<StackComponent>(workpiece, out var stack) && stack.StackTypeId == ingot;
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AnvilWorkpieceComponent, InteractUsingEvent>(OnWorkpieceInteractUsing);
    }

    private void OnWorkpieceInteractUsing(Entity<AnvilWorkpieceComponent> workpiece, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!HasAnyAnvilQuality(args.Used))
            return;

        if (!Transform(workpiece.Owner).Anchored)
            return;

        args.Handled = true;

        if (!workpiece.Comp.Forging)
        {
            ChooseRecipe(workpiece, args.User);
            return;
        }

        StrikeWorkpiece(workpiece, args.User, args.Used);
    }

    protected virtual void ChooseRecipe(Entity<AnvilWorkpieceComponent> workpiece, EntityUid user)
    {
    }

    protected virtual void StrikeWorkpiece(Entity<AnvilWorkpieceComponent> workpiece, EntityUid user, EntityUid tool)
    {
    }

    private bool HasAnyAnvilQuality(EntityUid tool)
    {
        foreach (var quality in AnvilQualities)
        {
            if (_tool.HasQuality(tool, quality))
                return true;
        }

        return false;
    }
}
