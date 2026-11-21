using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.CMU14.Machines;
using Content.Client.Power.Components;
using Content.Shared.CMU14.Botany;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Botany;

// CMU14: the window of the batch seed extractor: produce on the left, seed packets on the right, a progress bar and the
// Start / Eject seeds / Auto buttons underneath. Built in code like the grinder window.
public sealed class CMUBatchSeedExtractorWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly ItemSlotsSystem _itemSlots;
    private readonly SharedStackSystem _stacks;

    private EntityUid _owner;

    private readonly List<SlotView> _slots = new();
    private readonly Label _statusLabel;
    private readonly ProgressBar _progressBar;
    private readonly GridContainer _inputs;
    private readonly GridContainer _outputs;
    private readonly Button _autoButton;

    public event Action<string>? OnSlotPressed;
    public event Action? OnStartPressed;
    public event Action? OnEjectAllPressed;
    public event Action? OnToggleAutoPressed;

    public CMUBatchSeedExtractorWindow()
    {
        IoCManager.InjectDependencies(this);
        _itemSlots = _entMan.System<ItemSlotsSystem>();
        _stacks = _entMan.System<SharedStackSystem>();

        Title = Loc.GetString("cmu-batch-seed-extractor-title");
        MinSize = new Vector2(420, 340);

        var root = MachineWindowKit.Column();

        _statusLabel = new Label { HorizontalAlignment = Control.HAlignment.Center };
        root.AddChild(_statusLabel);

        var main = MachineWindowKit.Row(16);

        _inputs = MachineWindowKit.Grid(2);
        main.AddChild(MachineWindowKit.Section(Loc.GetString("cmu-batch-seed-extractor-produce"), _inputs));

        _outputs = MachineWindowKit.Grid(4);
        main.AddChild(MachineWindowKit.Section(Loc.GetString("cmu-batch-seed-extractor-seeds"), _outputs));

        root.AddChild(main);

        _progressBar = MachineWindowKit.Bar();
        root.AddChild(_progressBar);

        var buttons = MachineWindowKit.Row();
        buttons.HorizontalAlignment = Control.HAlignment.Center;

        var start = new Button { Text = Loc.GetString("cmu-batch-seed-extractor-start") };
        start.OnPressed += _ => OnStartPressed?.Invoke();
        buttons.AddChild(start);

        var eject = new Button { Text = Loc.GetString("cmu-batch-seed-extractor-eject-all") };
        eject.OnPressed += _ => OnEjectAllPressed?.Invoke();
        buttons.AddChild(eject);

        _autoButton = new Button { Text = Loc.GetString("cmu-batch-seed-extractor-auto-on") };
        _autoButton.OnPressed += _ => OnToggleAutoPressed?.Invoke();
        buttons.AddChild(_autoButton);

        root.AddChild(buttons);

        Contents.AddChild(root);
    }

    public void SetOwner(EntityUid owner)
    {
        _owner = owner;

        if (_slots.Count > 0 || !_entMan.TryGetComponent<CMUBatchSeedExtractorComponent>(owner, out var comp))
            return;

        for (var i = 0; i < comp.InputSlotCount; i++)
            _inputs.AddChild(Add(comp.InputSlotId(i), MachineWindowKit.InputBorder));

        for (var i = 0; i < comp.OutputSlotCount; i++)
            _outputs.AddChild(Add(comp.OutputSlotId(i), MachineWindowKit.OutputBorder));
    }

    private Control Add(string slotId, Color border)
    {
        var slot = MachineWindowKit.MakeSlot(_entMan, slotId, border, id => OnSlotPressed?.Invoke(id));
        _slots.Add(slot);
        return slot.Button;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_owner == EntityUid.Invalid || !_entMan.TryGetComponent<CMUBatchSeedExtractorComponent>(_owner, out var comp))
            return;

        foreach (var slot in _slots)
            MachineWindowKit.Refresh(_entMan, _itemSlots, _stacks, _owner, slot);

        _autoButton.Text = Loc.GetString(comp.AutoStart ? "cmu-batch-seed-extractor-auto-on" : "cmu-batch-seed-extractor-auto-off");

        var powered = !_entMan.TryGetComponent<ApcPowerReceiverComponent>(_owner, out var apc) || apc.Powered;

        if (comp.EndTime is { } endTime && comp.Duration is { } duration && duration > TimeSpan.Zero)
        {
            var left = endTime - _timing.CurTime;
            var fraction = Math.Clamp(1f - (float) (left / duration), 0f, 1f);
            _progressBar.Value = fraction;
            _statusLabel.Text = Loc.GetString("cmu-batch-seed-extractor-status-working", ("percent", (int) (fraction * 100)));
            return;
        }

        _progressBar.Value = 0;
        if (!powered)
            _statusLabel.Text = Loc.GetString("cmu-batch-seed-extractor-status-unpowered");
        else if (comp.Active)
            _statusLabel.Text = Loc.GetString("cmu-batch-seed-extractor-status-paused");
        else
            _statusLabel.Text = Loc.GetString("cmu-batch-seed-extractor-status-ready");
    }
}
