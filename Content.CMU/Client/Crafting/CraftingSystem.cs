// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Client.UserInterface.Controls;
using Content.Shared.CMU14.Crafting;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Crafting;

// Shows what the player can craft as a radial menu; picking a recipe crafts it.
public sealed class CraftingSystem : SharedCraftingSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private static readonly SpriteSpecifier CancelIcon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/close.svg.192dpi.png"));

    private SimpleRadialMenu? _menu;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CraftingMenuEvent>(OnMenu);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _menu?.Dispose();
        _menu = null;
    }

    // The server knows what the player carries and what they can do, so it is the one that says what to show.
    protected override void OpenMenu(EntityUid user)
    {
        RaiseNetworkEvent(new CraftingMenuRequestEvent());
    }

    // One button per recipe the server listed, and a cancel button while a craft is going on.
    public List<RadialMenuOptionBase> BuildOptions(IEnumerable<string> recipes, bool canCancel)
    {
        var options = new List<RadialMenuOptionBase>();

        if (canCancel)
        {
            options.Add(new RadialMenuActionOption<string>(_ => RaiseNetworkEvent(new CraftingCancelEvent()), "cancel")
            {
                ToolTip = Loc.GetString("au14-craft-cancel-option"),
                IconSpecifier = RadialMenuIconSpecifier.With(CancelIcon),
            });
        }

        foreach (var id in recipes)
        {
            if (!_proto.TryIndex<CraftingRecipePrototype>(id, out var recipe))
                continue;

            var recipeId = recipe.ID;
            var icon = recipe.Icon ?? new SpriteSpecifier.EntityPrototype(recipe.Output);
            options.Add(new RadialMenuActionOption<string>(_ => RaiseNetworkEvent(new CraftingSelectEvent(recipeId)), recipeId)
            {
                ToolTip = Loc.GetString(recipe.Name),
                IconSpecifier = RadialMenuIconSpecifier.With(icon),
            });
        }

        return options;
    }

    private void OnMenu(CraftingMenuEvent msg)
    {
        _menu?.Dispose();

        var menu = new SimpleRadialMenu();
        menu.SetButtons(BuildOptions(msg.Recipes, msg.CanCancel));
        menu.OnClose += () => _menu = null;
        menu.OpenOverMouseScreenPosition();
        _menu = menu;
    }
}
