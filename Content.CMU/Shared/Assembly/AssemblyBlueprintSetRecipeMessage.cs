// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Assembly;

// From the window: program the blueprint with this recipe.
[Serializable, NetSerializable]
public sealed class AssemblyBlueprintSetRecipeMessage(ProtoId<AssemblyRecipePrototype> recipe) : BoundUserInterfaceMessage
{
    public readonly ProtoId<AssemblyRecipePrototype> Recipe = recipe;
}
