// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.CMU14.Machines;
using Content.Shared.CMU14.Fluids;
using Content.Shared.CMU14.Steam;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Steam;

// The window of the boiler: its one fuel slot, and the water and the steam inside it.
public sealed class BoilerWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    private readonly ItemSlotsSystem _itemSlots;
    private readonly SharedStackSystem _stacks;

    private EntityUid _owner;

    private readonly SlotView _fuel;
    private readonly Label _statusLabel;
    private readonly MachineWindowKit.FluidGauge _water;
    private readonly MachineWindowKit.FluidGauge _steam;

    public event Action<string>? OnSlotPressed;

    public BoilerWindow()
    {
        IoCManager.InjectDependencies(this);
        _itemSlots = _entMan.System<ItemSlotsSystem>();
        _stacks = _entMan.System<SharedStackSystem>();

        Title = Loc.GetString("au14-boiler-title");
        MinSize = new Vector2(340, 220);

        var root = MachineWindowKit.Column();

        _statusLabel = new Label { HorizontalAlignment = Control.HAlignment.Center };
        root.AddChild(_statusLabel);

        var main = MachineWindowKit.Row(20);

        _fuel = MachineWindowKit.MakeSlot(_entMan, BoilerComponent.FuelSlotId, MachineWindowKit.FuelBorder, id => OnSlotPressed?.Invoke(id));
        main.AddChild(MachineWindowKit.Section(Loc.GetString("au14-window-fuel"), _fuel.Button));

        _water = new MachineWindowKit.FluidGauge(FluidKinds.Water, Loc.GetString("au14-window-water"), MachineWindowKit.WaterColor, 110);
        _steam = new MachineWindowKit.FluidGauge("steam", Loc.GetString("au14-window-steam"), MachineWindowKit.SteamColor, 110);
        main.AddChild(_water.Root);
        main.AddChild(_steam.Root);
        root.AddChild(main);

        Contents.AddChild(root);
    }

    public void SetOwner(EntityUid owner)
    {
        _owner = owner;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_owner == EntityUid.Invalid || !_entMan.TryGetComponent<BoilerComponent>(_owner, out var comp))
            return;

        MachineWindowKit.Refresh(_entMan, _itemSlots, _stacks, _owner, _fuel);

        if (_entMan.TryGetComponent<FluidTankComponent>(_owner, out var tank))
        {
            _water.Update(tank, comp.WaterLimit);
        }

        if (_entMan.TryGetComponent<SteamPortComponent>(_owner, out var port))
            _steam.UpdateSteam(port);

        _statusLabel.Text = Loc.GetString(comp.Burning ? "au14-boiler-status-burning" : "au14-boiler-status-cold");
    }
}
