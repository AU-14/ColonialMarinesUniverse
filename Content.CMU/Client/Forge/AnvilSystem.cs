// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Client.UserInterface.Controls;
using Content.Shared.CMU14.Forge;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Forge;

// Client side of the anvil: shows a radial menu of recipes for the workpiece and tells the server the chosen one.
public sealed class AnvilSystem : SharedAnvilSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private SimpleRadialMenu? _menu;

    public override void Shutdown()
    {
        base.Shutdown();
        _menu?.Dispose();
        _menu = null;
    }

    protected override void ChooseRecipe(Entity<AnvilWorkpieceComponent> workpiece, EntityUid user)
    {
        _menu?.Dispose();

        var workpieceUid = workpiece.Owner;
        var options = new List<RadialMenuOptionBase>();
        foreach (var recipe in _proto.EnumeratePrototypes<AnvilRecipePrototype>())
        {
            if (!RecipeFits(recipe, workpieceUid))
                continue;

            var recipeId = recipe.ID;
            options.Add(new RadialMenuActionOption<ProtoId<AnvilRecipePrototype>>(_ => OnRecipeChosen(workpieceUid, recipeId), recipeId)
            {
                ToolTip = Loc.GetString(recipe.Name),
                IconSpecifier = RadialMenuIconSpecifier.With(recipe.Icon),
            });
        }

        var menu = new SimpleRadialMenu();
        menu.SetButtons(options);
        menu.Track(workpieceUid);
        menu.OnClose += () => _menu = null;
        menu.OpenOverMouseScreenPosition();
        _menu = menu;
    }

    private void OnRecipeChosen(EntityUid workpiece, ProtoId<AnvilRecipePrototype> recipe)
    {
        RaiseNetworkEvent(new AnvilSelectRecipeEvent(GetNetEntity(workpiece), recipe));
    }
}
