// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.CMU14.Machines;
using Content.Shared.CMU14.Smelting;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Smelting;

// The window of the electric smelter: the materials and the result on top, the temperature in the middle, the progress below.
public sealed class ElectricSmelterWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly ItemSlotsSystem _itemSlots;
    private readonly SharedStackSystem _stacks;

    private EntityUid _owner;

    private readonly List<SlotView> _slots = new();

    private readonly Label _temperatureLabel;
    private readonly Slider _temperatureSlider;
    private readonly ProgressBar _progressBar;
    private readonly Label _progressLabel;

    private bool _sliderGrabbed;

    public event Action<string>? OnSlotPressed;
    public event Action<float>? OnTemperatureChanged;

    public ElectricSmelterWindow()
    {
        IoCManager.InjectDependencies(this);
        _itemSlots = _entMan.System<ItemSlotsSystem>();
        _stacks = _entMan.System<SharedStackSystem>();

        Title = Loc.GetString("au14-smelter-title");
        MinSize = new Vector2(380, 300);

        var root = MachineWindowKit.Column();

        var slotsRow = MachineWindowKit.Row(20);

        var materials = MachineWindowKit.Row();
        foreach (var id in ElectricSmelterComponent.MaterialSlotIds)
            materials.AddChild(Add(id, MachineWindowKit.InputBorder));
        slotsRow.AddChild(MachineWindowKit.Section(Loc.GetString("au14-window-materials"), materials));

        slotsRow.AddChild(MachineWindowKit.Section(Loc.GetString("au14-window-output"), Add(ElectricSmelterComponent.OutputSlotId, MachineWindowKit.OutputBorder)));
        root.AddChild(slotsRow);

        var heat = MachineWindowKit.Column(4);
        _temperatureLabel = new Label { HorizontalAlignment = Control.HAlignment.Center };
        heat.AddChild(_temperatureLabel);

        _temperatureSlider = new Slider
        {
            MinValue = 0,
            MaxValue = 1,
            MinSize = new Vector2(300, 20),
        };
        _temperatureSlider.OnGrabbed += _ => _sliderGrabbed = true;
        _temperatureSlider.OnReleased += _ =>
        {
            _sliderGrabbed = false;
            OnTemperatureChanged?.Invoke(_temperatureSlider.Value);
        };
        heat.AddChild(_temperatureSlider);
        root.AddChild(MachineWindowKit.Section(Loc.GetString("au14-window-temperature"), heat));

        _progressLabel = new Label { Text = Loc.GetString("au14-smelter-progress-idle"), HorizontalAlignment = Control.HAlignment.Center };
        root.AddChild(_progressLabel);

        _progressBar = MachineWindowKit.Bar(300);
        root.AddChild(_progressBar);

        Contents.AddChild(root);
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

        if (_entMan.TryGetComponent<ElectricSmelterComponent>(owner, out var comp))
        {
            _temperatureSlider.MinValue = comp.MinTemperature;
            _temperatureSlider.MaxValue = comp.MaxTemperature;
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_owner == EntityUid.Invalid || !_entMan.TryGetComponent<ElectricSmelterComponent>(_owner, out var comp))
            return;

        foreach (var slot in _slots)
            MachineWindowKit.Refresh(_entMan, _itemSlots, _stacks, _owner, slot);

        _temperatureLabel.Text = Loc.GetString("au14-smelter-temperature",
            ("current", (int) comp.CurrentTemperature),
            ("target", (int) comp.TargetTemperature));

        if (!_sliderGrabbed)
            _temperatureSlider.SetValueWithoutEvent(comp.TargetTemperature);

        if (comp.Active && comp.BatchEndsAt is { } endsAt && comp.BatchDuration is { } duration && duration > TimeSpan.Zero)
        {
            var left = endsAt - _timing.CurTime;
            var fraction = Math.Clamp(1f - (float) (left / duration), 0f, 1f);
            _progressBar.Value = fraction;
            _progressLabel.Text = Loc.GetString("au14-smelter-progress", ("percent", (int) (fraction * 100)));
        }
        else
        {
            _progressBar.Value = 0;
            _progressLabel.Text = Loc.GetString("au14-smelter-progress-idle");
        }
    }
}
