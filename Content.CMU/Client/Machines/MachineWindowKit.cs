// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Numerics;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.CMU14.Fluids;
using Content.Shared.CMU14.Steam;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Machines;

// The look all the windows of the machines made here share: titled sections with slot buttons of one size, gauges for the
// liquids, a status line on top and a progress bar underneath. Laying them out the same way is what keeps them tidy.
public sealed class SlotView(string id, ContainerButton button, SpriteView sprite, Label count)
{
    public readonly string Id = id;
    public readonly ContainerButton Button = button;
    public readonly SpriteView Sprite = sprite;
    public readonly Label Count = count;
}

// Shared pieces for the windows of the machines: slot sizes, border colors by slot kind (input, output, fuel, tool) and the colors of water, steam and slag.
public static class MachineWindowKit
{
    public const int SlotSize = 44;
    public const int WindowPadding = 8;

    public static readonly Color SlotBorder = new(90, 90, 90);
    public static readonly Color InputBorder = new(90, 120, 160);
    public static readonly Color OutputBorder = new(130, 120, 60);
    public static readonly Color FuelBorder = new(200, 120, 40);
    public static readonly Color ToolBorder = new(140, 90, 60);
    public static readonly Color SlotBackground = new(30, 30, 30, 200);
    public static readonly Color SectionTitle = new(170, 170, 170);

    public static readonly Color WaterColor = new(60, 120, 220);
    public static readonly Color SteamColor = new(200, 200, 215);
    public static readonly Color SlagColor = new(110, 80, 60);

    // The name of a liquid as the player reads it.
    public static string LiquidName(IEntityManager entMan, string kind)
    {
        return entMan.System<RMCReagentSystem>().TryIndex(kind, out var reagent) ? reagent.LocalizedName : kind;
    }

    // A line for each liquid in a tank and how much there is of it.
    public static string TankText(IEntityManager entMan, FluidTankComponent tank)
    {
        if (tank.Contents.Count == 0)
            return Loc.GetString("au14-window-storage-empty");

        var lines = new System.Collections.Generic.List<string>();
        foreach (var (kind, amount) in tank.Contents)
            lines.Add($"{LiquidName(entMan, kind)}: {(int) amount}");

        return string.Join("\n", lines) + $"\n{(int) tank.Total}/{(int) tank.Capacity}";
    }

    public static SlotView MakeSlot(IEntityManager entMan, string slotId, Color border, Action<string>? onPressed, int size = SlotSize)
    {
        var button = new ContainerButton
        {
            MinSize = new Vector2(size, size),
            StyleBoxOverride = new StyleBoxFlat
            {
                BackgroundColor = SlotBackground,
                BorderColor = border,
                BorderThickness = new Thickness(2),
            },
        };

        var sprite = new SpriteView(entMan)
        {
            MinSize = new Vector2(size - 8, size - 8),
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Center,
        };

        var count = new Label
        {
            HorizontalAlignment = Control.HAlignment.Right,
            VerticalAlignment = Control.VAlignment.Bottom,
            Margin = new Thickness(0, 0, 2, 0),
            Visible = false,
        };

        button.AddChild(sprite);
        button.AddChild(count);

        if (onPressed != null)
            button.OnPressed += _ => onPressed(slotId);

        return new SlotView(slotId, button, sprite, count);
    }

    // Shows what lies in the slot, and how many if it is a stack.
    public static void Refresh(IEntityManager entMan, ItemSlotsSystem slots, SharedStackSystem stacks, EntityUid owner, SlotView slot)
    {
        var item = slots.GetItemOrNull(owner, slot.Id);
        slot.Sprite.SetEntity(item);

        if (item is { } uid && entMan.TryGetComponent<StackComponent>(uid, out var stack))
        {
            var count = stacks.GetCount((uid, stack));
            slot.Count.Visible = count > 1;
            slot.Count.Text = count.ToString();
        }
        else
        {
            slot.Count.Visible = false;
        }
    }

    // A titled block: a small grey title over what it holds.
    public static BoxContainer Section(string title, Control body)
    {
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
        };

        box.AddChild(new Label { Text = title, FontColorOverride = SectionTitle });
        box.AddChild(body);
        return box;
    }

    public static GridContainer Grid(int columns)
    {
        return new GridContainer { Columns = columns, HSeparationOverride = 3, VSeparationOverride = 3 };
    }

    public static BoxContainer Row(int separation = 4)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = separation };
    }

    public static BoxContainer Column(int separation = 8)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = separation };
    }

    public static ProgressBar Bar(int width = 280)
    {
        return new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            MinSize = new Vector2(width, 18),
            HorizontalAlignment = Control.HAlignment.Center,
        };
    }

    // A gauge of one fluid in a machine's tank: a coloured bar with how much is in it.
    public sealed class FluidGauge
    {
        public readonly string Kind;
        public readonly ProgressBar Bar;
        public readonly Label Label;
        public readonly BoxContainer Root;

        public FluidGauge(string kind, string title, Color color, int width = 140)
        {
            Kind = kind;
            Root = Column(2);
            Root.AddChild(new Label { Text = title, FontColorOverride = SectionTitle });

            Bar = new ProgressBar { MinValue = 0, MaxValue = 1, MinSize = new Vector2(width, 16) };
            Bar.ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = color };
            Root.AddChild(Bar);

            Label = new Label { HorizontalAlignment = Control.HAlignment.Center };
            Root.AddChild(Label);
        }

        // limit is the most of this fluid the machine keeps; without one the tank's capacity counts.
        public void Update(FluidTankComponent tank, float? limit = null)
        {
            var max = limit ?? tank.Capacity;
            var amount = tank.Get(Kind);
            Bar.Value = max <= 0f ? 0f : Math.Clamp(amount / max, 0f, 1f);
            Label.Text = $"{(int) amount}/{(int) max}";
        }

        // The same gauge for steam, which is not in a tank but in the gas pipes next to the machine.
        public void UpdateSteam(SteamPortComponent port)
        {
            Bar.Value = port.GaugeMoles <= 0f ? 0f : Math.Clamp(port.Moles / port.GaugeMoles, 0f, 1f);
            Label.Text = Loc.GetString("au14-window-steam-gauge", ("moles", MathF.Round(port.Moles, 1)), ("pressure", (int) port.Pressure));
        }
    }
}
