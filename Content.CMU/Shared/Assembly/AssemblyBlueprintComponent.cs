// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Assembly;

// A blueprint: remembers which assembly recipe it is programmed with.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AssemblyBlueprintComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<AssemblyRecipePrototype>? ProgrammedRecipe;
}
