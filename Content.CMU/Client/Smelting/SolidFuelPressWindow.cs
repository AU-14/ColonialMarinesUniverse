// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.CMU14.Machines;
using Content.Shared.CMU14.Fluids;
using Content.Shared.CMU14.Smelting;
using Content.Shared.CMU14.Steam;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Smelting;

// The window of the press, of the solid-fuel one and the steam one: the mold, the ingots and the fuel on the left, what
// comes out on the right, the liquids it holds and a progress bar of the piece that is being pressed.
public sealed class SolidFuelPressWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly ItemSlotsSystem _itemSlots;
    private readonly SharedStackSystem _stacks;

    private EntityUid _owner;

    private readonly List<SlotView> _slots = new();
    private readonly Label _storageLabel = new();
    private MachineWindowKit.FluidGauge? _steamGauge;
    private readonly Label _statusLabel;
    private readonly ProgressBar _progressBar;
    private readonly BoxContainer _left;
    private readonly BoxContainer _right;
    private bool _built;

    public event Action<string>? OnSlotPressed;

    public SolidFuelPressWindow()
    {
        IoCManager.InjectDependencies(this);
        _itemSlots = _entMan.System<ItemSlotsSystem>();
        _stacks = _entMan.System<SharedStackSystem>();

        Title = Loc.GetString("au14-press-title");
        MinSize = new Vector2(460, 330);

        var root = MachineWindowKit.Column();

        _statusLabel = new Label { HorizontalAlignment = Control.HAlignment.Center };
        root.AddChild(_statusLabel);

        var main = MachineWindowKit.Row(20);
        _left = MachineWindowKit.Column();
        _right = MachineWindowKit.Column();
        main.AddChild(_left);
        main.AddChild(_right);
        root.AddChild(main);

        _progressBar = MachineWindowKit.Bar();
        root.AddChild(_progressBar);

        Contents.AddChild(root);
    }

    private Control Add(string slotId, Color border)
    {
        var slot = MachineWindowKit.MakeSlot(_entMan, slotId, border, id => OnSlotPressed?.Invoke(id));
        _slots.Add(slot);
        return slot.Button;
    }

    // What the window shows depends on the press: a solid-fuel one has a slot for planks, a steam one shows its steam.
    public void SetOwner(EntityUid owner)
    {
        _owner = owner;
        if (_built)
            return;

        _built = true;

        _left.AddChild(MachineWindowKit.Section(Loc.GetString("au14-press-mold-label"), Add(SolidFuelPressComponent.MoldSlotId, MachineWindowKit.ToolBorder)));

        var inputs = MachineWindowKit.Row();
        foreach (var id in SolidFuelPressComponent.InputSlotIds)
            inputs.AddChild(Add(id, MachineWindowKit.InputBorder));
        _left.AddChild(MachineWindowKit.Section(Loc.GetString("au14-press-inputs-label"), inputs));

        var steam = _entMan.TryGetComponent<SolidFuelPressComponent>(owner, out var press) && press.SteamPerSecond > 0f;
        if (!steam)
            _left.AddChild(MachineWindowKit.Section(Loc.GetString("au14-press-fuel-label"), Add(SolidFuelPressComponent.FuelSlotId, MachineWindowKit.FuelBorder)));

        var outputs = MachineWindowKit.Grid(2);
        foreach (var id in SolidFuelPressComponent.OutputSlotIds)
            outputs.AddChild(Add(id, MachineWindowKit.OutputBorder));
        _right.AddChild(MachineWindowKit.Section(Loc.GetString("au14-press-outputs-label"), outputs));

        // The liquids the press holds: the steam it runs on, and the water it keeps for the pieces that need it.
        if (_entMan.HasComponent<FluidTankComponent>(owner))
        {
            var storage = MachineWindowKit.Row(10);
            if (steam)
            {
                _steamGauge = new MachineWindowKit.FluidGauge("steam", Loc.GetString("au14-window-steam"), MachineWindowKit.SteamColor, 100);
                storage.AddChild(_steamGauge.Root);
            }

            storage.AddChild(_storageLabel);
            _right.AddChild(MachineWindowKit.Section(Loc.GetString("au14-window-storage"), storage));
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_owner == EntityUid.Invalid || !_entMan.TryGetComponent<SolidFuelPressComponent>(_owner, out var comp))
            return;

        foreach (var slot in _slots)
            MachineWindowKit.Refresh(_entMan, _itemSlots, _stacks, _owner, slot);

        if (_steamGauge != null && _entMan.TryGetComponent<SteamPortComponent>(_owner, out var steamPort))
            _steamGauge.UpdateSteam(steamPort);

        if (_entMan.TryGetComponent<FluidTankComponent>(_owner, out var tank))
            _storageLabel.Text = MachineWindowKit.TankText(_entMan, tank);

        if (comp.Working && comp.WorkEndsAt is { } endsAt && comp.WorkDuration is { } duration && duration > TimeSpan.Zero)
        {
            var left = endsAt - _timing.CurTime;
            var fraction = Math.Clamp(1f - (float) (left / duration), 0f, 1f);
            _progressBar.Value = fraction;
            _statusLabel.Text = Loc.GetString("au14-press-status-pressing", ("percent", (int) (fraction * 100)));
            return;
        }

        _statusLabel.Text = Loc.GetString("au14-press-status-ready");
        _progressBar.Value = 0;
    }
}
