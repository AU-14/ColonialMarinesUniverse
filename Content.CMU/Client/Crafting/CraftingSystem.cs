// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Crafting;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Crafting;

// Shows what the player can craft as a strip of cards that is rolled with the wheel: the recipe in the middle is large, the ones next to it are smaller, and a click on the one in
// the middle crafts it.
public sealed class CraftingSystem : SharedCraftingSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private CraftingCarouselWindow? _carousel;
    private CraftingMinigameWindow? _minigame;

    // The recipe crafted last, so that the strip opens on it next time: the same thing is crafted over and over.
    private string? _lastRecipe;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CraftingMenuEvent>(OnMenu);
        SubscribeNetworkEvent<CraftingMinigameStartEvent>(OnMinigame);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _carousel?.Close();
        _carousel = null;
        CloseMinigame();
    }

    // The strip of cards while it is open.
    public CraftingCarouselWindow? Carousel => _carousel;

    // The game of a recipe while it is being played.
    public CraftingMinigameWindow? Minigame => _minigame;

    // The server has seen that the craft can be made and wants it played: the window of the game opens, and what comes of it goes back to the server. A window closed before
    // the end is a craft that is given up.
    private void OnMinigame(CraftingMinigameStartEvent msg)
    {
        _carousel?.Close();
        CloseMinigame();

        var title = _proto.TryIndex<CraftingRecipePrototype>(msg.Recipe, out var recipe) ? Loc.GetString(recipe.Name) : msg.Recipe;
        var window = new CraftingMinigameWindow(msg.Recipe, title, msg.Kind, msg.Seed, msg.Steps, msg.Duration, msg.Ease, msg.Stage, msg.Stages);
        window.OnFinished += score => RaiseNetworkEvent(new CraftingMinigameResultEvent(msg.Recipe, score));
        window.OnAborted += () => RaiseNetworkEvent(new CraftingCancelEvent());
        window.OnClose += () =>
        {
            if (_minigame == window)
                _minigame = null;
        };
        window.OpenCenteredAt(new System.Numerics.Vector2(0.5f, 0.6f));
        _minigame = window;
    }

    private void CloseMinigame()
    {
        // Shutting the window down is not giving the craft up: the server drops a game nobody answers.
        var window = _minigame;
        _minigame = null;
        window?.CloseQuietly();
    }

    // The server knows what the player carries and what they can do, so it is the one that says what to show.
    protected override void OpenMenu(EntityUid user)
    {
        RaiseNetworkEvent(new CraftingMenuRequestEvent());
    }

    // The strip of cards: the recipes the server listed, by name, opened on the one crafted last; with a cancel button while a craft is going on.
    private void OnMenu(CraftingMenuEvent msg)
    {
        _carousel?.Close();

        var cards = BuildCards(msg.Recipes);
        if (cards.Count == 0 && !msg.CanCancel)
            return;

        var start = _lastRecipe == null ? 0 : Math.Max(0, cards.FindIndex(card => card.Id == _lastRecipe));
        var window = new CraftingCarouselWindow(cards, msg.CanCancel, start);
        window.OnCraft += id =>
        {
            _lastRecipe = id;
            RaiseNetworkEvent(new CraftingSelectEvent(id));
        };
        window.OnCancelCraft += () => RaiseNetworkEvent(new CraftingCancelEvent());
        window.OnClose += () => _carousel = null;
        window.OpenCenteredAt(new System.Numerics.Vector2(0.5f, 0.72f));
        _carousel = window;
    }

    public List<CarouselRecipe> BuildCards(IEnumerable<string> recipes)
    {
        var cards = new List<CarouselRecipe>();

        foreach (var id in recipes)
        {
            if (!_proto.TryIndex<CraftingRecipePrototype>(id, out var recipe))
                continue;

            var tools = new List<string>();
            foreach (var quality in recipe.Tools)
            {
                if (_proto.TryIndex(quality, out var tool))
                    tools.Add(Loc.GetString(tool.ToolName));
            }

            var ingredients = new List<CarouselIngredient>();
            foreach (var consumable in recipe.Consumables)
                ingredients.Add(Describe(consumable));

            cards.Add(new CarouselRecipe
            {
                Id = recipe.ID,
                Name = Loc.GetString(recipe.Name),
                Icon = IconOf(recipe.Icon ?? new SpriteSpecifier.EntityPrototype(recipe.Output)),
                Time = Loc.GetString("au14-craft-carousel-time", ("seconds", (int) Math.Ceiling(recipe.Time.TotalSeconds))),
                Tools = tools.Count == 0 ? string.Empty : Loc.GetString("au14-craft-carousel-tools", ("tools", string.Join(", ", tools))),
                Ingredients = ingredients,
            });
        }

        cards.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        return cards;
    }

    // An ingredient: a stack (its icon, and the name of the thing it is made of) or a single thing.
    private CarouselIngredient Describe(CraftConsumable consumable)
    {
        if (consumable.Stack is { } stackId && _proto.TryIndex(stackId, out StackPrototype? stack))
        {
            var name = _proto.TryIndex(stack.Spawn, out var spawned) ? spawned.Name : stack.Name;
            var icon = stack.Icon != null ? IconOf(stack.Icon) : IconOf(new SpriteSpecifier.EntityPrototype(stack.Spawn));
            return new CarouselIngredient(icon, name, consumable.Amount);
        }

        if (consumable.Item is { } itemId && _proto.TryIndex(itemId, out var item))
            return new CarouselIngredient(IconOf(new SpriteSpecifier.EntityPrototype(itemId)), item.Name, consumable.Amount);

        return new CarouselIngredient(null, consumable.Stack?.Id ?? consumable.Item?.Id ?? "?", consumable.Amount);
    }

    private Texture? IconOf(SpriteSpecifier specifier)
    {
        try
        {
            return _sprite.Frame0(specifier);
        }
        catch (Exception e)
        {
            Log.Warning($"The picture of a recipe could not be made: {e.Message}");
            return null;
        }
    }
}
