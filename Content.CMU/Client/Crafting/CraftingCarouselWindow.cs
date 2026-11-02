// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Collections.Generic;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Crafting;

// What one ingredient of a recipe looks like in the details of the card: its picture, its name and how many.
public readonly record struct CarouselIngredient(Texture? Icon, string Name, int Amount);

// One recipe as the carousel shows it.
public sealed class CarouselRecipe
{
    public required string Id;
    public required string Name;
    public required Texture? Icon;
    public required string Time;
    public required string Tools;
    public required List<CarouselIngredient> Ingredients;
}

// The crafting menu as a strip of cards: the recipe picked is in the middle, large, the ones next to it are smaller and fainter, and the wheel of the mouse rolls the strip.
// Below it are the details of the recipe in the middle. A click on the one in the middle crafts it; a click on another card brings it to the middle. It is low and wide, so that it
// takes a strip of the screen instead of a ring of it, and any number of recipes fit in it.
public sealed class CraftingCarouselWindow : BaseWindow
{
    private const float StripHeight = 118f;
    private const float WindowWidth = 680f;

    private readonly CarouselStrip _strip;
    private readonly BoxContainer _details;
    private readonly Label _counter;

    private readonly List<CarouselRecipe> _recipes;

    public event Action<string>? OnCraft;
    public event Action? OnCancelCraft;

    public CraftingCarouselWindow(List<CarouselRecipe> recipes, bool canCancel, int startIndex)
    {
        _recipes = recipes;
        Resizable = false;
        MinSize = new Vector2(WindowWidth, 0);

        // A control hears the mouse only if it does not ignore it, and a plain control does: so the window, which is dragged and rolls the strip, and the panel it is made of, say that they
        // take the mouse (the window stops it, the panel passes it up to the window).
        MouseFilter = MouseFilterMode.Stop;

        _strip = new CarouselStrip(recipes) { MinSize = new Vector2(WindowWidth - 16, StripHeight), MouseFilter = MouseFilterMode.Pass };
        _strip.OnSelected += ShowDetails;
        _strip.OnActivated += index => Craft(index);

        _details = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 3,
        };

        _counter = new Label
        {
            HorizontalExpand = true,
            HorizontalAlignment = HAlignment.Right,
            FontColorOverride = Color.FromHex("#9aa3ad"),
        };

        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        if (canCancel)
        {
            var cancel = new Button { Text = Loc.GetString("au14-craft-cancel-option"), StyleClasses = { "OpenBoth" } };
            cancel.OnPressed += _ =>
            {
                OnCancelCraft?.Invoke();
                Close();
            };
            header.AddChild(cancel);
        }

        header.AddChild(_counter);

        var close = new Button { Text = "✕", MinWidth = 28 };
        close.OnPressed += _ => Close();
        header.AddChild(close);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(8, 6, 8, 8),
        };
        root.AddChild(header);
        root.AddChild(_strip);
        root.AddChild(new PanelContainer { PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#ffffff18") }, MinHeight = 1 });
        root.AddChild(_details);

        AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#12161cdd"),
                BorderColor = Color.FromHex("#4a5360"),
                BorderThickness = new Thickness(1),
            },
            MouseFilter = MouseFilterMode.Pass,
            Children = { root },
        });

        _strip.Select(Math.Clamp(startIndex, 0, Math.Max(0, recipes.Count - 1)), instant: true);
    }

    // The recipe in the middle of the strip, and the two things that can be done with it from outside: roll the strip, craft what is in the middle.
    public int SelectedIndex => _strip.SelectedIndex;

    public void Roll(int step) => _strip.Roll(step);

    public void CraftSelected() => Craft(_strip.SelectedIndex);

    public int Count => _recipes.Count;

    // The cards of the strip, for what looks at the layout.
    public IReadOnlyList<CardView> Cards => _strip.Cards;

    // The place of the strip on the screen, in pixels.
    public UIBox2i StripPixelRect => _strip.PixelRect;

    // The middle of the strip on the screen, in pixels.
    public Vector2 StripCenterOnScreen => _strip.GlobalPixelPosition + _strip.PixelSize / 2f;

    private void Craft(int index)
    {
        if (index < 0 || index >= _recipes.Count)
            return;

        OnCraft?.Invoke(_recipes[index].Id);
        Close();
    }

    // The details of the recipe in the middle: its name, the time, the tools and the ingredients with their pictures.
    private void ShowDetails(int index)
    {
        _details.RemoveAllChildren();
        _counter.Text = _recipes.Count == 0 ? string.Empty : $"{index + 1} / {_recipes.Count}";

        if (index < 0 || index >= _recipes.Count)
            return;

        var recipe = _recipes[index];

        var title = new Label { Text = recipe.Name, HorizontalAlignment = HAlignment.Center, FontColorOverride = Color.FromHex("#f2f4f7") };
        _details.AddChild(title);

        var meta = recipe.Time;
        if (!string.IsNullOrEmpty(recipe.Tools))
            meta += "   ·   " + recipe.Tools;

        _details.AddChild(new Label { Text = meta, HorizontalAlignment = HAlignment.Center, FontColorOverride = Color.FromHex("#9aa3ad") });

        var chips = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Center,
            SeparationOverride = 14,
        };

        foreach (var ingredient in recipe.Ingredients)
        {
            var chip = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 3 };
            if (ingredient.Icon != null)
                chip.AddChild(new TextureRect { Texture = ingredient.Icon, MinSize = new Vector2(24, 24), Stretch = TextureRect.StretchMode.KeepAspectCentered });

            chip.AddChild(new Label { Text = $"{ingredient.Amount} × {ingredient.Name}", VerticalAlignment = VAlignment.Center });
            chips.AddChild(chip);
        }

        _details.AddChild(chips);
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos) => DragMode.Move;

    // The wheel rolls the strip wherever the cursor is over the window.
    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);
        _strip.Roll(args.Delta.Y < 0 ? 1 : -1);
        args.Handle();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            Close();
            args.Handle();
            return;
        }

        base.KeyBindDown(args);
    }
}

// The strip of the cards. The card in the middle is large; every other one is smaller and fainter the farther it is from the middle, and the strip glides to a new card, it does
// not jump.
public sealed class CarouselStrip : Control
{
    private const float CenterWidth = 128f;
    private const float CenterHeight = 100f;
    private const float Gap = 12f;

    private readonly List<CarouselRecipe> _recipes;
    private readonly List<CardView> _cards = new();

    private int _selected;

    // Where the strip is now, in cards: it glides to the selected one.
    private float _visual;

    public event Action<int>? OnSelected;
    public event Action<int>? OnActivated;

    public CarouselStrip(List<CarouselRecipe> recipes)
    {
        _recipes = recipes;
        RectClipContent = true;

        foreach (var recipe in recipes)
        {
            var card = new CardView(recipe);
            _cards.Add(card);
            AddChild(card);
        }
    }

    public int SelectedIndex => _selected;

    public IReadOnlyList<CardView> Cards => _cards;

    public void Select(int index, bool instant = false)
    {
        if (_recipes.Count == 0)
            return;

        _selected = Math.Clamp(index, 0, _recipes.Count - 1);
        if (instant)
            _visual = _selected;

        OnSelected?.Invoke(_selected);
        InvalidateArrange();
    }

    // One card on: the strip does not roll round, it stops at the ends.
    public void Roll(int step)
    {
        Select(_selected + step);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var difference = _selected - _visual;
        if (Math.Abs(difference) < 0.002f)
        {
            if (_visual != _selected)
            {
                _visual = _selected;
                InvalidateArrange();
            }

            return;
        }

        _visual += difference * Math.Min(1f, args.DeltaSeconds * 14f);
        InvalidateArrange();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        foreach (var card in _cards)
            card.Measure(new Vector2(CenterWidth, CenterHeight));

        return new Vector2(Math.Min(availableSize.X, MinSize.X), MinSize.Y);
    }

    // How large a card is, as a share of the middle one, at this distance (in cards) from the middle.
    private static float ScaleAt(float distance) => Math.Max(0.5f, 1f - 0.27f * Math.Min(distance, 1.8f));

    // How far the middle of a card at this distance from the middle is from the middle of the strip: the half widths of the cards between add up, with a gap, so that no card is on top of
    // another (a card is not as far as a fixed step, because the nearer ones are larger).
    private static float DistanceFromMiddle(float distance)
    {
        var whole = (int) Math.Floor(distance);
        var total = 0f;
        for (var k = 0; k < whole; k++)
            total += Step(k);

        return total + (distance - whole) * Step(whole);
    }

    // From the middle of the card k cards from the middle to the middle of the next one.
    private static float Step(int k) => CenterWidth * ScaleAt(k) / 2f + CenterWidth * ScaleAt(k + 1) / 2f + Gap;

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var centerX = finalSize.X / 2f;
        var centerY = finalSize.Y / 2f;

        for (var i = 0; i < _cards.Count; i++)
        {
            var card = _cards[i];
            var offset = i - _visual;
            var distance = Math.Abs(offset);

            if (distance > 3.6f)
            {
                card.Visible = false;
                continue;
            }

            card.Visible = true;

            // Nearer to the middle, larger; the size falls off smoothly with the distance, down to about half.
            var scale = ScaleAt(distance);
            var width = CenterWidth * scale;
            var height = CenterHeight * scale;
            var x = centerX + Math.Sign(offset) * DistanceFromMiddle(distance) - width / 2f;
            var y = centerY - height / 2f;

            card.Arrange(UIBox2.FromDimensions(x, y, width, height));
            card.SetLook(Math.Max(0f, 1f - distance * 0.28f), distance < 0.5f);
        }

        return finalSize;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        // The card under the cursor: the nearest one in the middle wins over the ones it overlaps.
        var best = -1;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < _cards.Count; i++)
        {
            var card = _cards[i];
            if (!card.Visible || !card.PixelRect.Contains((Vector2i) (args.PointerLocation.Position - GlobalPixelPosition)))
                continue;

            var distance = Math.Abs(i - _visual);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        if (best < 0)
            return;

        if (best == _selected)
            OnActivated?.Invoke(best);
        else
            Select(best);

        args.Handle();
    }
}

// One card: the picture of what the recipe makes, and its name when it is in the middle.
public sealed class CardView : PanelContainer
{
    private readonly StyleBoxFlat _box = new();
    private readonly TextureRect _picture;
    private readonly Label _name;

    public CardView(CarouselRecipe recipe)
    {
        PanelOverride = _box;
        MouseFilter = MouseFilterMode.Ignore;

        _picture = new TextureRect
        {
            Texture = recipe.Icon,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            VerticalExpand = true,
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
            Margin = new Thickness(6),
        };

        _name = new Label
        {
            Text = recipe.Name,
            ClipText = true,
            HorizontalAlignment = HAlignment.Center,
            Visible = false,
            MouseFilter = MouseFilterMode.Ignore,
        };

        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, MouseFilter = MouseFilterMode.Ignore };
        column.AddChild(_picture);
        column.AddChild(_name);
        AddChild(column);
    }

    // How strong the card looks (1 in the middle, less farther), and whether it is the one in the middle.
    public void SetLook(float strength, bool center)
    {
        _box.BackgroundColor = center ? Color.FromHex("#27303b") : Color.FromHex("#1b2129").WithAlpha(0.55f + 0.35f * strength);
        _box.BorderColor = center ? Color.FromHex("#e0b25a") : Color.FromHex("#4a5360");
        _box.BorderThickness = new Thickness(center ? 2 : 1);
        _picture.Modulate = Color.White.WithAlpha(0.45f + 0.55f * strength);
        _name.Visible = center;
    }
}
