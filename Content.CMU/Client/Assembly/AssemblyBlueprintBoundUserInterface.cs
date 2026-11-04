// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Assembly;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Assembly;

// Client side of a blueprint reader: opens the recipe list window and tells the server which recipe was picked.
public sealed class AssemblyBlueprintBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private AssemblyBlueprintWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<AssemblyBlueprintWindow>();
        _window.OnRecipePicked += OnRecipePicked;
    }

    private void OnRecipePicked(ProtoId<AssemblyRecipePrototype> recipe)
    {
        SendPredictedMessage(new AssemblyBlueprintSetRecipeMessage(recipe));
        _window?.Close();
    }
}
