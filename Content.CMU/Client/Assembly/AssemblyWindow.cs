// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Numerics;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Assembly;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Stacks;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Assembly;

// Window of an assembly machine: draws its slots (inputs, output, blueprint, upgrades) and shows what an empty slot wants as a faded ghost.
public sealed class AssemblyWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const int SlotSize = 40;
    private const int OutputSlotSize = 56;

    private static readonly Color SlotBorderColor = new(90, 90, 90);
    private static readonly Color SlotBackgroundColor = new(30, 30, 30, 200);
    private static readonly Color OutputBorderColor = new(120, 120, 60);
    private static readonly Color BlueprintBorderColor = new(70, 100, 150);
    private static readonly Color UpgradeBorderColor = new(70, 140, 90);
    private static readonly Color GhostTint = new(1f, 1f, 1f, 0.35f);

    private readonly ItemSlotsSystem _itemSlots;
    private readonly SharedStackSystem _stacks;
    private readonly PowerCellSystem _powerCell;
    private readonly SharedBatterySystem _battery;
    private readonly SpriteSystem _sprite;

    private EntityUid _owner;

    private readonly (ContainerButton Button, SpriteView Sprite, Label Count, TextureRect Ghost, Label GhostCount)[] _materialSlots;
    private readonly (ContainerButton Button, SpriteView Sprite, Label Count) _outputSlot;
    private readonly (ContainerButton Button, SpriteView Sprite, Label Count) _blueprintSlot;
    private readonly (ContainerButton Button, SpriteView Sprite, Label Count)[] _upgradeSlots;

    private readonly ProgressBar _chargeBar;
    private readonly (ContainerButton Button, SpriteView Sprite, Label Count) _cellSlot;

    private readonly Label _statusLabel;
    private readonly ProgressBar _progressBar;

    public event Action<string>? OnSlotPressed;

    public AssemblyWindow()
    {
        IoCManager.InjectDependencies(this);
        _itemSlots = _entMan.System<ItemSlotsSystem>();
        _stacks = _entMan.System<SharedStackSystem>();
        _powerCell = _entMan.System<PowerCellSystem>();
        _battery = _entMan.System<SharedBatterySystem>();
        _sprite = _entMan.System<SpriteSystem>();

        Title = Loc.GetString("au14-assembly-title");
        MinSize = new Vector2(580, 440);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
        };

        _statusLabel = new Label { HorizontalAlignment = HAlignment.Center };
        root.AddChild(_statusLabel);

        var mainRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 12,
        };

        var leftColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };

        var topRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 16,
        };

        var materialGrid = new GridContainer { Columns = 3 };
        _materialSlots = new (ContainerButton, SpriteView, Label, TextureRect, Label)[AssemblyMachineComponent.MaterialSlotIds.Length];
        for (var i = 0; i < AssemblyMachineComponent.MaterialSlotIds.Length; i++)
        {
            var slotId = AssemblyMachineComponent.MaterialSlotIds[i];
            var slot = MakeSlotButton(SlotBorderColor, SlotSize);
            slot.Button.OnPressed += _ => OnSlotPressed?.Invoke(slotId);
            _materialSlots[i] = slot;
            materialGrid.AddChild(slot.Button);
        }

        topRow.AddChild(Section(Loc.GetString("au14-window-materials"), materialGrid));

        var resultColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };
        var outputSlot = MakeSlotButton(OutputBorderColor, OutputSlotSize);
        outputSlot.Button.OnPressed += _ => OnSlotPressed?.Invoke(AssemblyMachineComponent.OutputSlotId);
        _outputSlot = (outputSlot.Button, outputSlot.Sprite, outputSlot.Count);
        resultColumn.AddChild(Section(Loc.GetString("au14-assembly-result-label"), outputSlot.Button));
        topRow.AddChild(resultColumn);

        leftColumn.AddChild(topRow);

        var bottomRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
        };

        var blueprintSlot = MakeSlotButton(BlueprintBorderColor, SlotSize);
        blueprintSlot.Button.OnPressed += _ => OnSlotPressed?.Invoke(AssemblyMachineComponent.BlueprintSlotId);
        _blueprintSlot = (blueprintSlot.Button, blueprintSlot.Sprite, blueprintSlot.Count);
        bottomRow.AddChild(Section(Loc.GetString("au14-window-blueprint"), blueprintSlot.Button));

        _progressBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            MinSize = new Vector2(200, 20),
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        };
        bottomRow.AddChild(_progressBar);

        leftColumn.AddChild(bottomRow);
        mainRow.AddChild(leftColumn);

        var sidebar = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalAlignment = HAlignment.Right,
        };

        var charge = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        var cellSlot = MakeSlotButton(UpgradeBorderColor, SlotSize);
        cellSlot.Button.OnPressed += _ => OnSlotPressed?.Invoke("cell_slot");
        _cellSlot = (cellSlot.Button, cellSlot.Sprite, cellSlot.Count);
        charge.AddChild(cellSlot.Button);
        _chargeBar = new ProgressBar { MinValue = 0, MaxValue = 1, MinSize = new Vector2(90, 20) };
        charge.AddChild(_chargeBar);
        sidebar.AddChild(Section(Loc.GetString("au14-assembly-charge-label"), charge));

        var upgrades = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        _upgradeSlots = new (ContainerButton, SpriteView, Label)[AssemblyMachineComponent.UpgradeSlotIds.Length];
        for (var i = 0; i < AssemblyMachineComponent.UpgradeSlotIds.Length; i++)
        {
            var slotId = AssemblyMachineComponent.UpgradeSlotIds[i];
            var slot = MakeSlotButton(UpgradeBorderColor, SlotSize);
            slot.Button.OnPressed += _ => OnSlotPressed?.Invoke(slotId);
            _upgradeSlots[i] = (slot.Button, slot.Sprite, slot.Count);
            upgrades.AddChild(slot.Button);
        }

        sidebar.AddChild(Section(Loc.GetString("au14-window-upgrades"), upgrades));

        mainRow.AddChild(sidebar);
        root.AddChild(mainRow);

        Contents.AddChild(root);
    }

    // A titled block, the same as in the other windows of the machines.
    private static BoxContainer Section(string title, Control body)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 3 };
        box.AddChild(new Label { Text = title, FontColorOverride = new Color(170, 170, 170) });
        box.AddChild(body);
        return box;
    }

    private (ContainerButton Button, SpriteView Sprite, Label Count, TextureRect Ghost, Label GhostCount) MakeSlotButton(Color borderColor, int size)
    {
        var button = new ContainerButton
        {
            MinSize = new Vector2(size, size),
            StyleBoxOverride = new StyleBoxFlat
            {
                BackgroundColor = SlotBackgroundColor,
                BorderColor = borderColor,
                BorderThickness = new Thickness(2),
            },
        };

        var ghost = new TextureRect
        {
            MinSize = new Vector2(size - 8, size - 8),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            Modulate = GhostTint,
            Visible = false,
        };

        var sprite = new SpriteView(_entMan)
        {
            MinSize = new Vector2(size - 8, size - 8),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };

        var count = new Label
        {
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Bottom,
            Margin = new Thickness(0, 0, 2, 0),
            Visible = false,
        };

        var ghostCount = new Label
        {
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Bottom,
            Margin = new Thickness(0, 0, 2, 0),
            Modulate = new Color(1f, 1f, 1f, 0.6f),
            Visible = false,
        };

        button.AddChild(ghost);
        button.AddChild(sprite);
        button.AddChild(count);
        button.AddChild(ghostCount);

        return (button, sprite, count, ghost, ghostCount);
    }

    public void SetOwner(EntityUid owner)
    {
        _owner = owner;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_owner == EntityUid.Invalid || !_entMan.TryGetComponent<AssemblyMachineComponent>(_owner, out var comp))
            return;

        var recipe = GetActiveRecipe();

        for (var i = 0; i < _materialSlots.Length; i++)
            RefreshMaterialSlot(i, recipe);

        RefreshSlot(_outputSlot, AssemblyMachineComponent.OutputSlotId);
        RefreshSlot(_blueprintSlot, AssemblyMachineComponent.BlueprintSlotId);
        RefreshSlot(_cellSlot, "cell_slot");

        for (var i = 0; i < _upgradeSlots.Length; i++)
            RefreshSlot(_upgradeSlots[i], AssemblyMachineComponent.UpgradeSlotIds[i]);

        _chargeBar.Value = _powerCell.TryGetBatteryFromSlot(_owner, out var battery)
            ? _battery.GetChargeLevel(battery.Value.AsNullable())
            : 0f;

        if (comp.Active && comp.AssembleEndsAt is { } endsAt && comp.AssembleDuration is { } duration && duration > TimeSpan.Zero)
        {
            var left = endsAt - _timing.CurTime;
            var fraction = Math.Clamp(1f - (float) (left / duration), 0f, 1f);
            _progressBar.Value = fraction;
            _statusLabel.Text = Loc.GetString("au14-assembly-status-assembling", ("percent", (int) (fraction * 100)));
            return;
        }

        _progressBar.Value = 0;
        _statusLabel.Text = recipe == null
            ? Loc.GetString("au14-assembly-status-no-blueprint")
            : Loc.GetString("au14-assembly-status-ready");
    }

    private AssemblyRecipePrototype? GetActiveRecipe()
    {
        if (_itemSlots.GetItemOrNull(_owner, AssemblyMachineComponent.BlueprintSlotId) is not { } blueprint ||
            !_entMan.TryGetComponent<AssemblyBlueprintComponent>(blueprint, out var bp) || bp.ProgrammedRecipe is not { } recipeId)
        {
            return null;
        }

        return _proto.TryIndex<AssemblyRecipePrototype>(recipeId, out var recipe) ? recipe : null;
    }

    private void RefreshMaterialSlot(int index, AssemblyRecipePrototype? recipe)
    {
        var slot = _materialSlots[index];
        var slotId = AssemblyMachineComponent.MaterialSlotIds[index];
        var item = _itemSlots.GetItemOrNull(_owner, slotId);
        slot.Sprite.SetEntity(item);

        if (item is { } uid && _entMan.TryGetComponent<StackComponent>(uid, out var stack))
        {
            var count = _stacks.GetCount((uid, stack));
            slot.Count.Visible = count > 1;
            slot.Count.Text = count.ToString();
        }
        else
        {
            slot.Count.Visible = false;
        }

        slot.Ghost.Visible = false;
        slot.GhostCount.Visible = false;

        if (item != null || recipe == null || !recipe.Layout.TryGetValue(index, out var requirement))
            return;

        if (!_proto.TryIndex(requirement.Material, out var stackProto) || !_proto.TryIndex(stackProto.Spawn, out var entProto))
            return;

        slot.Ghost.Texture = _sprite.GetPrototypeIcon(entProto).GetFrame(RsiDirection.South, 0);
        slot.Ghost.Visible = true;

        if (requirement.Amount <= 1)
            return;

        slot.GhostCount.Text = requirement.Amount.ToString();
        slot.GhostCount.Visible = true;
    }

    private void RefreshSlot((ContainerButton Button, SpriteView Sprite, Label Count) slot, string slotId)
    {
        var item = _itemSlots.GetItemOrNull(_owner, slotId);
        slot.Sprite.SetEntity(item);

        if (item is { } uid && _entMan.TryGetComponent<StackComponent>(uid, out var stack))
        {
            var count = _stacks.GetCount((uid, stack));
            slot.Count.Visible = count > 1;
            slot.Count.Text = count.ToString();
        }
        else
        {
            slot.Count.Visible = false;
        }
    }
}
