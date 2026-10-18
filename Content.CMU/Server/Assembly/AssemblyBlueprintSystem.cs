// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Assembly;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Assembly;

// Server side of a blueprint: remembers which recipe the player picked in its window.
public sealed class AssemblyBlueprintSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AssemblyBlueprintComponent, AssemblyBlueprintSetRecipeMessage>(OnSetRecipe);
    }

    private void OnSetRecipe(EntityUid uid, AssemblyBlueprintComponent comp, AssemblyBlueprintSetRecipeMessage args)
    {
        if (!_proto.HasIndex<AssemblyRecipePrototype>(args.Recipe))
            return;

        comp.ProgrammedRecipe = args.Recipe;
        Dirty(uid, comp);
    }
}
