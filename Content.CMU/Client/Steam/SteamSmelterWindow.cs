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

// The window of the steam smelter, laid out like the one of the electric smelter: the materials, the mold and the result on
// top, below them the two regulators in place of the thermostat, the steam and the slag it holds, and the progress.
public sealed class SteamSmelterWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    private readonly ItemSlotsSystem _itemSlots;
    private readonly SharedStackSystem _stacks;

    private EntityUid _owner;

    private readonly List<SlotView> _slots = new();

    private readonly Slider _steamSlider;
    private readonly Slider _slagSlider;
    private readonly Label _steamLabel;
    private readonly Label _slagLabel;
    private readonly MachineWindowKit.FluidGauge _steamGauge;
    private readonly MachineWindowKit.FluidGauge _slagGauge;
    private readonly ProgressBar _progressBar;
    private readonly Label _progressLabel;

    private bool _steamGrabbed;
    private bool _slagGrabbed;

    public event Action<string>? OnSlotPressed;
    public event Action<bool, float>? OnRegulatorChanged;

    public SteamSmelterWindow()
    {
        IoCManager.InjectDependencies(this);
        _itemSlots = _entMan.System<ItemSlotsSystem>();
        _stacks = _entMan.System<SharedStackSystem>();

        Title = Loc.GetString("au14-steam-smelter-title");
        MinSize = new Vector2(420, 380);

        var root = MachineWindowKit.Column();

        var slotsRow = MachineWindowKit.Row(20);

        var materials = MachineWindowKit.Row();
        foreach (var id in SteamSmelterComponent.MaterialSlotIds)
            materials.AddChild(Add(id, MachineWindowKit.InputBorder));
        slotsRow.AddChild(MachineWindowKit.Section(Loc.GetString("au14-window-materials"), materials));

        slotsRow.AddChild(MachineWindowKit.Section(Loc.GetString("au14-press-mold-label"), Add(SteamSmelterComponent.MoldSlotId, MachineWindowKit.ToolBorder)));
        slotsRow.AddChild(MachineWindowKit.Section(Loc.GetString("au14-window-output"), Add(SteamSmelterComponent.OutputSlotId, MachineWindowKit.OutputBorder)));
        root.AddChild(slotsRow);

        var regulators = MachineWindowKit.Column(4);

        _steamLabel = new Label { HorizontalAlignment = Control.HAlignment.Center };
        regulators.AddChild(_steamLabel);
        _steamSlider = MakeSlider(false, v => _steamGrabbed = v);
        regulators.AddChild(_steamSlider);

        _slagLabel = new Label { HorizontalAlignment = Control.HAlignment.Center };
        regulators.AddChild(_slagLabel);
        _slagSlider = MakeSlider(true, v => _slagGrabbed = v);
        regulators.AddChild(_slagSlider);

        root.AddChild(MachineWindowKit.Section(Loc.GetString("au14-window-regulators"), regulators));

        var gauges = MachineWindowKit.Row(16);
        _steamGauge = new MachineWindowKit.FluidGauge("steam", Loc.GetString("au14-window-steam"), MachineWindowKit.SteamColor, 150);
        _slagGauge = new MachineWindowKit.FluidGauge(FluidKinds.Slag, Loc.GetString("au14-window-slag"), MachineWindowKit.SlagColor, 150);
        gauges.AddChild(_steamGauge.Root);
        gauges.AddChild(_slagGauge.Root);
        root.AddChild(gauges);

        _progressLabel = new Label { Text = Loc.GetString("au14-smelter-progress-idle"), HorizontalAlignment = Control.HAlignment.Center };
        root.AddChild(_progressLabel);

        _progressBar = MachineWindowKit.Bar(300);
        root.AddChild(_progressBar);

        Contents.AddChild(root);
    }

    private Slider MakeSlider(bool slag, Action<bool> grabbed)
    {
        var slider = new Slider { MinValue = 0, MaxValue = 1, MinSize = new Vector2(300, 20) };
        slider.OnGrabbed += _ => grabbed(true);
        slider.OnReleased += _ =>
        {
            grabbed(false);
            OnRegulatorChanged?.Invoke(slag, slider.Value);
        };
        return slider;
    }

    private Control Add(string slotId, Color border)
    {
        var slot = MachineWindowKit.MakeSlot(_entMan, slotId, border, id => OnSlotPressed?.Invoke(id));
        _slots.Add(slot);
        return slot.Button;
    }

    public void SetOwner(EntityUid owner)
    {
        _owner = owner;

        if (_entMan.TryGetComponent<SteamSmelterComponent>(owner, out var comp))
            _steamSlider.MinValue = comp.MinSteamRegulator;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_owner == EntityUid.Invalid || !_entMan.TryGetComponent<SteamSmelterComponent>(_owner, out var comp))
            return;

        foreach (var slot in _slots)
            MachineWindowKit.Refresh(_entMan, _itemSlots, _stacks, _owner, slot);

        if (_entMan.TryGetComponent<FluidTankComponent>(_owner, out var tank))
        {
            _slagGauge.Update(tank, comp.SlagLimit);
        }

        if (_entMan.TryGetComponent<SteamPortComponent>(_owner, out var port))
            _steamGauge.UpdateSteam(port);

        _steamLabel.Text = Loc.GetString("au14-steam-smelter-steam-regulator", ("percent", (int) (comp.SteamRegulator * 100)));
        _slagLabel.Text = Loc.GetString("au14-steam-smelter-slag-regulator", ("percent", (int) (comp.SlagRegulator * 100)));

        if (!_steamGrabbed)
            _steamSlider.SetValueWithoutEvent(comp.SteamRegulator);

        if (!_slagGrabbed)
            _slagSlider.SetValueWithoutEvent(comp.SlagRegulator);

        if (comp.Active)
        {
            _progressBar.Value = Math.Clamp(comp.Progress, 0f, 1f);
            _progressLabel.Text = Loc.GetString("au14-smelter-progress", ("percent", (int) (comp.Progress * 100)));
        }
        else
        {
            _progressBar.Value = Math.Clamp(comp.Progress, 0f, 1f);
            _progressLabel.Text = Loc.GetString("au14-smelter-progress-idle");
        }
    }
}
