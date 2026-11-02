// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Numerics;
using Content.Shared.CMU14.Assembly;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Assembly;

// Window with the list of assembly recipes of a blueprint; raises an event when the player picks one.
public sealed class AssemblyBlueprintWindow : DefaultWindow
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public event Action<ProtoId<AssemblyRecipePrototype>>? OnRecipePicked;

    public AssemblyBlueprintWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("au14-assembly-blueprint-title");
        MinSize = new Vector2(280, 200);

        var list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
        };

        foreach (var recipe in _proto.EnumeratePrototypes<AssemblyRecipePrototype>())
        {
            var recipeId = recipe.ID;
            var button = new Button { Text = recipeId, HorizontalExpand = true };
            button.OnPressed += _ => OnRecipePicked?.Invoke(recipeId);
            list.AddChild(button);
        }

        Contents.AddChild(list);
    }
}
