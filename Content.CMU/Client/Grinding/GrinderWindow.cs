// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.CMU14.Machines;
using Content.Client.Power.Components;
using Content.Shared.CMU14.Fluids;
using Content.Shared.CMU14.Grinding;
using Content.Shared.CMU14.Steam;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Grinding;

// The window of the grinder, and of the steam grinder: the materials and the crushing attachments on the left, what comes
// out on the right, a progress bar underneath.
public sealed class GrinderWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly ItemSlotsSystem _itemSlots;
    private readonly SharedStackSystem _stacks;

    private EntityUid _owner;

    private readonly List<SlotView> _slots = new();
    private readonly Label _statusLabel;
    private readonly ProgressBar _progressBar;
    private readonly BoxContainer _left;
    private MachineWindowKit.FluidGauge? _steam;

    public event Action<string>? OnSlotPressed;

    public GrinderWindow()
    {
        IoCManager.InjectDependencies(this);
        _itemSlots = _entMan.System<ItemSlotsSystem>();
        _stacks = _entMan.System<SharedStackSystem>();

        Title = Loc.GetString("au14-grinder-title");
        MinSize = new Vector2(500, 380);

        var root = MachineWindowKit.Column();

        _statusLabel = new Label { HorizontalAlignment = Control.HAlignment.Center };
        root.AddChild(_statusLabel);

        var main = MachineWindowKit.Row(16);

        _left = MachineWindowKit.Column();

        var materials = MachineWindowKit.Grid(3);
        foreach (var id in GrinderComponent.MaterialSlotIds)
            materials.AddChild(Add(id, MachineWindowKit.InputBorder));
        _left.AddChild(MachineWindowKit.Section(Loc.GetString("au14-window-materials"), materials));

        var attachments = MachineWindowKit.Row();
        foreach (var id in GrinderComponent.BladeSlotIds)
            attachments.AddChild(Add(id, MachineWindowKit.ToolBorder));
        _left.AddChild(MachineWindowKit.Section(Loc.GetString("au14-window-attachments"), attachments));

        main.AddChild(_left);

        var outputs = MachineWindowKit.Grid(3);
        foreach (var id in GrinderComponent.OutputSlotIds)
            outputs.AddChild(Add(id, MachineWindowKit.OutputBorder));
        main.AddChild(MachineWindowKit.Section(Loc.GetString("au14-window-output"), outputs));

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

    public void SetOwner(EntityUid owner)
    {
        _owner = owner;

        // A steam grinder shows the steam it has.
        if (_steam == null && _entMan.HasComponent<SteamPortComponent>(owner))
        {
            _steam = new MachineWindowKit.FluidGauge("steam", Loc.GetString("au14-window-steam"), MachineWindowKit.SteamColor);
            _left.AddChild(_steam.Root);
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_owner == EntityUid.Invalid || !_entMan.TryGetComponent<GrinderComponent>(_owner, out var comp))
            return;

        foreach (var slot in _slots)
            MachineWindowKit.Refresh(_entMan, _itemSlots, _stacks, _owner, slot);

        var steamDriven = _entMan.TryGetComponent<SteamPortComponent>(_owner, out var port);
        if (steamDriven && _steam != null)
            _steam.UpdateSteam(port!);

        var powered = steamDriven
            ? port!.Moles > 0.01f
            : _entMan.TryGetComponent<ApcPowerReceiverComponent>(_owner, out var apc) && apc.Powered;

        if (comp.Active && comp.GrindEndsAt is { } endsAt && comp.GrindDuration is { } duration && duration > TimeSpan.Zero)
        {
            var left = endsAt - _timing.CurTime;
            var fraction = Math.Clamp(1f - (float) (left / duration), 0f, 1f);
            _progressBar.Value = fraction;
            _statusLabel.Text = Loc.GetString("au14-grinder-status-grinding", ("percent", (int) (fraction * 100)));
            return;
        }

        _statusLabel.Text = powered
            ? Loc.GetString("au14-grinder-status-ready")
            : Loc.GetString(steamDriven ? "au14-grinder-status-no-steam" : "au14-grinder-status-unpowered");
        _progressBar.Value = 0;
    }
}
