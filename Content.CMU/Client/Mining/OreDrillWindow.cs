// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.CMU14.Machines;
using Content.Shared.CMU14.Mining;
using Content.Shared.CMU14.Steam;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Mining;

// The window of a drill, laid out like the others: the ore that came out in the middle, what the drill is doing on top and a bar
// of the turn below.
public sealed class OreDrillWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    private readonly ItemSlotsSystem _itemSlots;
    private readonly SharedStackSystem _stacks;

    private EntityUid _owner;

    private readonly List<SlotView> _slots = new();
    private readonly Label _statusLabel;
    private readonly ProgressBar _progressBar;
    private readonly BoxContainer _main;
    private MachineWindowKit.FluidGauge? _steam;

    public event Action<string>? OnSlotPressed;

    public OreDrillWindow()
    {
        IoCManager.InjectDependencies(this);
        _itemSlots = _entMan.System<ItemSlotsSystem>();
        _stacks = _entMan.System<SharedStackSystem>();

        Title = Loc.GetString("au14-drill-title");
        MinSize = new Vector2(420, 280);

        var root = MachineWindowKit.Column();

        _statusLabel = new Label { HorizontalAlignment = Control.HAlignment.Center };
        root.AddChild(_statusLabel);

        _main = MachineWindowKit.Row(20);
        var main = _main;

        var outputs = MachineWindowKit.Grid(3);
        foreach (var id in OreDrillComponent.OutputSlotIds)
        {
            var slot = MachineWindowKit.MakeSlot(_entMan, id, MachineWindowKit.OutputBorder, slotId => OnSlotPressed?.Invoke(slotId));
            _slots.Add(slot);
            outputs.AddChild(slot.Button);
        }

        main.AddChild(MachineWindowKit.Section(Loc.GetString("au14-window-output"), outputs));
        root.AddChild(main);

        _progressBar = MachineWindowKit.Bar(300);
        root.AddChild(_progressBar);

        Contents.AddChild(root);
    }

    // A steam drill shows the steam it has, like the other steam machines.
    public void SetOwner(EntityUid owner)
    {
        _owner = owner;

        if (_steam == null && _entMan.HasComponent<SteamPortComponent>(owner))
        {
            _steam = new MachineWindowKit.FluidGauge("steam", Loc.GetString("au14-window-steam"), MachineWindowKit.SteamColor, 90);
            _main.AddChild(_steam.Root);
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_owner == EntityUid.Invalid || !_entMan.TryGetComponent<OreDrillComponent>(_owner, out var comp))
            return;

        foreach (var slot in _slots)
            MachineWindowKit.Refresh(_entMan, _itemSlots, _stacks, _owner, slot);

        _progressBar.Value = Math.Clamp(comp.Progress, 0f, 1f);

        if (_steam != null && _entMan.TryGetComponent<SteamPortComponent>(_owner, out var steamPort))
            _steam.UpdateSteam(steamPort);

        if (comp.Active)
            _statusLabel.Text = Loc.GetString("au14-drill-status-working", ("percent", (int) (comp.Progress * 100)));
        else if (!comp.OnDeposit)
            _statusLabel.Text = Loc.GetString("au14-drill-status-no-deposit");
        else
            _statusLabel.Text = Loc.GetString("au14-drill-status-idle");
    }
}
