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

// The window of the condenser, in the same layout as the other machines: the material slot and the result on top, what the
// condenser holds of each liquid below them, and the progress of the piece being made.
public sealed class CondenserWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    private readonly ItemSlotsSystem _itemSlots;
    private readonly SharedStackSystem _stacks;

    private EntityUid _owner;

    private readonly List<SlotView> _slots = new();
    private readonly Label _contents = new();
    private readonly Label _statusLabel;
    private readonly ProgressBar _progressBar;

    public event Action<string>? OnSlotPressed;

    public CondenserWindow()
    {
        IoCManager.InjectDependencies(this);
        _itemSlots = _entMan.System<ItemSlotsSystem>();
        _stacks = _entMan.System<SharedStackSystem>();

        Title = Loc.GetString("au14-condenser-title");
        MinSize = new Vector2(320, 260);

        var root = MachineWindowKit.Column();

        _statusLabel = new Label { HorizontalAlignment = Control.HAlignment.Center };
        root.AddChild(_statusLabel);

        var slotsRow = MachineWindowKit.Row(20);
        slotsRow.AddChild(MachineWindowKit.Section(Loc.GetString("au14-condenser-material"), Add(CondenserComponent.MaterialSlotId, MachineWindowKit.InputBorder)));
        slotsRow.AddChild(MachineWindowKit.Section(Loc.GetString("au14-window-output"), Add(CondenserComponent.OutputSlotId, MachineWindowKit.OutputBorder)));
        root.AddChild(slotsRow);

        root.AddChild(MachineWindowKit.Section(Loc.GetString("au14-window-storage"), _contents));

        _progressBar = MachineWindowKit.Bar(260);
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
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_owner == EntityUid.Invalid || !_entMan.TryGetComponent<CondenserComponent>(_owner, out var comp))
            return;

        foreach (var slot in _slots)
            MachineWindowKit.Refresh(_entMan, _itemSlots, _stacks, _owner, slot);

        if (_entMan.TryGetComponent<FluidTankComponent>(_owner, out var tank))
            _contents.Text = MachineWindowKit.TankText(_entMan, tank);

        _progressBar.Value = Math.Clamp(comp.Progress, 0f, 1f);
        _statusLabel.Text = comp.Active
            ? Loc.GetString("au14-condenser-status-working", ("percent", (int) (comp.Progress * 100)))
            : Loc.GetString("au14-condenser-status-idle");
    }
}
