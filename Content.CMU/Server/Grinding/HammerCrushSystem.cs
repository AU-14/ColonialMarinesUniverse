// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Grinding;
using Content.Shared.CMU14.Mining;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Grinding;

// Ore and crystals can be beaten into powder by hand: a click of a hammer (anything with the quality Hammering) on a piece of ore or a crystal crushes one piece by the recipe
// of the grinder. By hand it is a third worse - two powder where the grinder makes three, one of stone dust where it makes two - and one piece at a click, so
// the grinder stays the way to do it in bulk.
public sealed class HammerCrushSystem : EntitySystem
{
    public const string HammerQuality = "Hammering";

    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier CrushSound = new SoundCollectionSpecifier("MetalThud");

    public override void Initialize()
    {
        base.Initialize();

        // On the ore, not on the hammer, so that it does not share the event with the systems of tools.
        SubscribeLocalEvent<GroundStackComponent, InteractUsingEvent>(OnInteractUsing);
    }

    // What is left of an amount of the grinder when it is done by hand: two thirds of it, and never nothing.
    public static int ByHand(int amount) => Math.Max(1, amount * 2 / 3);

    private void OnInteractUsing(Entity<GroundStackComponent> ore, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, HammerQuality))
            return;

        if (Crush(ore.Owner, args.User))
            args.Handled = true;
    }

    // Crushes one recipe's worth of the stack by hand; false when the stack is not a thing the grinder grinds.
    public bool Crush(EntityUid ore, EntityUid user)
    {
        if (!TryComp<StackComponent>(ore, out var stack) || FindRecipe(stack.StackTypeId) is not { } recipe || stack.Count < recipe.InputAmount)
            return false;

        SpawnPowder(ore, recipe.Result, ByHand(recipe.ResultAmount));
        foreach (var extra in recipe.Extras)
        {
            SpawnPowder(ore, extra.Result, ByHand(extra.Amount));
        }

        _audio.PlayPvs(CrushSound, ore);
        _popup.PopupEntity(Loc.GetString("rumc-hammer-crush", ("ore", Name(ore))), user, user);

        // SetCount deletes a stack that is down to nothing.
        _stack.SetCount(ore, stack.Count - recipe.InputAmount, stack);
        return true;
    }

    private void SpawnPowder(EntityUid next, ProtoId<StackPrototype> stackId, int amount)
    {
        if (!_proto.TryIndex(stackId, out var stackProto) || string.IsNullOrEmpty(stackProto.Spawn.Id))
            return;

        var powder = SpawnNextToOrDrop(stackProto.Spawn, next);
        if (TryComp<StackComponent>(powder, out var powderStack))
            _stack.SetCount(powder, amount, powderStack);
    }

    private GrinderRecipePrototype? FindRecipe(ProtoId<StackPrototype> input)
    {
        foreach (var recipe in _proto.EnumeratePrototypes<GrinderRecipePrototype>())
        {
            if (recipe.Input == input)
                return recipe;
        }

        return null;
    }
}
