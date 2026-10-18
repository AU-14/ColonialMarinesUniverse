// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Forge;

// From the player: shape this workpiece into this anvil recipe.
[Serializable, NetSerializable]
public sealed class AnvilSelectRecipeEvent : EntityEventArgs
{
    public readonly NetEntity Workpiece;
    public readonly ProtoId<AnvilRecipePrototype> Recipe;

    public AnvilSelectRecipeEvent(NetEntity workpiece, ProtoId<AnvilRecipePrototype> recipe)
    {
        Workpiece = workpiece;
        Recipe = recipe;
    }
}
