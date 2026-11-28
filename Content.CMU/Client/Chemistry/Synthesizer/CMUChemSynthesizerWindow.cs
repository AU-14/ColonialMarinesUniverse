using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.CMU14.Machines;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.CMU14.Chemistry.Synthesizer;
using Content.Shared.CMU14.Fluids;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Chemistry.Synthesizer;

/// <summary>
/// Window of the chemical synthesizer: the beaker, report and sample slots, the fluid tank, the program, stage and
/// fault, the chamber temperature, a batch count (locked while a cycle runs), start/stop, "program from sample", the
/// chain toggle and the chain plan with its current step highlighted (M4), and the continuous mode check box with its
/// waiting / run-size line (FLOW-02). Reads the networked
/// <see cref="CMUChemSynthesizerComponent"/> and <see cref="FluidTankComponent"/> every frame.
/// </summary>
public sealed class CMUChemSynthesizerWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    private static readonly Color CurrentStepColor = Color.FromHex("#E0C050");

    private readonly ItemSlotsSystem _itemSlots;
    private readonly SharedStackSystem _stacks;
    private readonly RMCReagentSystem _reagents;
    private readonly SharedSolutionContainerSystem _solutions;

    private EntityUid _owner;
    private int _lastBatches = -1;
    private string _lastPlan = string.Empty;

    private readonly List<SlotView> _slots = new();
    private readonly Label _programLabel;
    private readonly Label _stageLabel;
    private readonly Label _runLabel;
    private readonly Label _faultLabel;
    private readonly Label _temperatureLabel;
    private readonly Label _tankLabel;
    private readonly Label _batchesLabel;
    private readonly SpinBox _batches;
    private readonly Button _chain;
    private readonly CheckBox _continuous;
    private readonly BoxContainer _plan;
    private readonly Label _bufferLabel;

    public event Action<string>? OnSlotPressed;
    public event Action<int>? OnBatchesChanged;
    public event Action? OnStart;
    public event Action? OnStop;
    public event Action? OnProgramFromSample;
    public event Action<bool>? OnChainToggled;
    public event Action<bool>? OnContinuousToggled;

    public CMUChemSynthesizerWindow()
    {
        IoCManager.InjectDependencies(this);
        _itemSlots = _entMan.System<ItemSlotsSystem>();
        _stacks = _entMan.System<SharedStackSystem>();
        _reagents = _entMan.System<RMCReagentSystem>();
        _solutions = _entMan.System<SharedSolutionContainerSystem>();

        Title = Loc.GetString("cmu-chem-synth-title");
        MinSize = new Vector2(480, 460);

        var root = MachineWindowKit.Column();

        var slotsRow = MachineWindowKit.Row(16);
        var inputs = MachineWindowKit.Row();
        foreach (var id in new[] { "input1", "input2", "input3" })
        {
            inputs.AddChild(Add(id, MachineWindowKit.InputBorder));
        }

        slotsRow.AddChild(MachineWindowKit.Section(Loc.GetString("cmu-chem-synth-inputs"), inputs));
        slotsRow.AddChild(MachineWindowKit.Section(Loc.GetString("cmu-chem-synth-output"), Add("output", MachineWindowKit.OutputBorder)));
        slotsRow.AddChild(MachineWindowKit.Section(Loc.GetString("cmu-chem-synth-waste"), Add("waste", MachineWindowKit.SlotBorder)));
        slotsRow.AddChild(MachineWindowKit.Section(Loc.GetString("cmu-chem-synth-program-slot"), Add("program", MachineWindowKit.ToolBorder)));
        slotsRow.AddChild(MachineWindowKit.Section(Loc.GetString("cmu-chem-synth-sample-slot"), Add("sample", MachineWindowKit.ToolBorder)));
        root.AddChild(slotsRow);

        _tankLabel = new Label();
        root.AddChild(MachineWindowKit.Section(Loc.GetString("cmu-chem-synth-tank"), _tankLabel));

        var status = MachineWindowKit.Column(4);
        _programLabel = new Label();
        _stageLabel = new Label();
        _runLabel = new Label { FontColorOverride = CurrentStepColor };
        _temperatureLabel = new Label();
        _faultLabel = new Label { FontColorOverride = Color.FromHex("#E05050") };
        status.AddChild(_programLabel);
        status.AddChild(_stageLabel);
        status.AddChild(_runLabel);
        status.AddChild(_temperatureLabel);
        status.AddChild(_faultLabel);
        root.AddChild(MachineWindowKit.Section(Loc.GetString("cmu-chem-synth-status"), status));

        _plan = MachineWindowKit.Column(2);
        _bufferLabel = new Label();
        var planSection = MachineWindowKit.Column(2);
        planSection.AddChild(_plan);
        planSection.AddChild(_bufferLabel);
        root.AddChild(MachineWindowKit.Section(Loc.GetString("cmu-chem-synth-plan"), planSection));

        var controls = MachineWindowKit.Row(8);
        _batchesLabel = new Label { Text = Loc.GetString("cmu-chem-synth-batches") };
        controls.AddChild(_batchesLabel);
        _batches = new SpinBox { MinSize = new Vector2(120, 0) };
        _batches.IsValid = value => value >= 1;
        _batches.InitDefaultButtons();
        _batches.OverrideValue(1);
        _batches.ValueChanged += args => OnBatchesChanged?.Invoke(args.Value);
        controls.AddChild(_batches);

        var start = new Button { Text = Loc.GetString("cmu-chem-synth-start") };
        start.OnPressed += _ => OnStart?.Invoke();
        controls.AddChild(start);

        var stop = new Button { Text = Loc.GetString("cmu-chem-synth-stop") };
        stop.OnPressed += _ => OnStop?.Invoke();
        controls.AddChild(stop);
        root.AddChild(controls);

        var programming = MachineWindowKit.Row(8);
        var fromSample = new Button { Text = Loc.GetString("cmu-chem-synth-program-from-sample") };
        fromSample.OnPressed += _ => OnProgramFromSample?.Invoke();
        programming.AddChild(fromSample);

        _chain = new Button { ToggleMode = true, Text = Loc.GetString("cmu-chem-synth-chain-off") };
        _chain.OnToggled += args => OnChainToggled?.Invoke(args.Pressed);
        programming.AddChild(_chain);

        _continuous = new CheckBox { Text = Loc.GetString("cmu-chem-synth-continuous") };
        _continuous.OnToggled += args => OnContinuousToggled?.Invoke(args.Pressed);
        programming.AddChild(_continuous);
        root.AddChild(programming);

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

    private string ReagentName(string? id)
    {
        if (id == null)
            return string.Empty;

        return _reagents.TryIndex(id, out var reagent) ? reagent.LocalizedName : id;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_owner == EntityUid.Invalid || !_entMan.TryGetComponent<CMUChemSynthesizerComponent>(_owner, out var comp))
            return;

        foreach (var slot in _slots)
        {
            MachineWindowKit.Refresh(_entMan, _itemSlots, _stacks, _owner, slot);
        }

        var program = comp.ProgramProduct != null
            ? ReagentName(comp.ProgramProduct)
            : Loc.GetString("cmu-chem-synth-program-none");

        _programLabel.Text = Loc.GetString("cmu-chem-synth-program", ("name", program));

        var running = comp.Stage is not (CMUChemSynthesizerStage.Idle or CMUChemSynthesizerStage.Fault);
        var stage = comp.Waiting
            ? Loc.GetString("cmu-chem-synth-stage-waiting")
            : Loc.GetString($"cmu-chem-synth-stage-{comp.Stage.ToString().ToLowerInvariant()}");
        _stageLabel.Text = comp.Plan.Count > 0 && comp.Stage != CMUChemSynthesizerStage.Idle
            ? Loc.GetString("cmu-chem-synth-stage-step",
                ("stage", stage),
                ("step", Math.Min(comp.PlanIndex + 1, comp.Plan.Count)),
                ("steps", comp.Plan.Count))
            : Loc.GetString("cmu-chem-synth-stage", ("stage", stage));

        _runLabel.Text = RunText(comp, running);

        _temperatureLabel.Text = float.IsNaN(comp.TargetTemperature)
            ? Loc.GetString("cmu-chem-synth-temperature-idle", ("current", (int) comp.ChamberTemperature))
            : Loc.GetString("cmu-chem-synth-temperature",
                ("current", (int) comp.ChamberTemperature),
                ("target", (int) comp.TargetTemperature));

        _faultLabel.Text = comp.Fault != null
            ? Loc.GetString(comp.Fault, ("reagent", ReagentName(comp.PlanBlockReagent)), ("max", comp.MaxPlanDepth))
            : string.Empty;

        _bufferLabel.Text = BufferText(comp);

        _tankLabel.Text = _entMan.TryGetComponent<FluidTankComponent>(_owner, out var tank)
            ? MachineWindowKit.TankText(_entMan, tank)
            : string.Empty;

        if (comp.Batches != _lastBatches)
        {
            _lastBatches = comp.Batches;
            _batches.OverrideValue(comp.Batches);
        }

        // the server refuses batch changes mid-cycle (the tank reservation reads them)
        _batches.SetButtonDisabled(running);
        _batches.LineEditDisabled = running;

        _chain.Pressed = comp.ChainEnabled;
        _chain.Text = Loc.GetString(comp.ChainEnabled ? "cmu-chem-synth-chain-on" : "cmu-chem-synth-chain-off");

        _continuous.Pressed = comp.Continuous;
        _batchesLabel.Text = Loc.GetString(comp.Continuous ? "cmu-chem-synth-batches-max" : "cmu-chem-synth-batches");

        UpdatePlan(comp);
    }

    /// <summary>FLOW-02: what continuous mode is doing: waiting (and for what), or the size of the running batch.</summary>
    private string RunText(CMUChemSynthesizerComponent comp, bool running)
    {
        if (comp.Waiting)
        {
            return comp.PlanBlockReagent != null
                ? Loc.GetString("cmu-chem-synth-waiting-reagent", ("reagent", ReagentName(comp.PlanBlockReagent)))
                : Loc.GetString("cmu-chem-synth-waiting");
        }

        if (!running || !comp.Continuous)
            return string.Empty;

        var run = comp.RunBatches > 0 ? comp.RunBatches : comp.Batches;
        return Loc.GetString("cmu-chem-synth-run-batches", ("run", run), ("max", comp.Batches));
    }

    /// <summary>The intermediate buffer's contents, so leftovers between chain steps are never hidden.</summary>
    private string BufferText(CMUChemSynthesizerComponent comp)
    {
        if (!_solutions.TryGetSolution(_owner, comp.BufferSolution, out _, out var buffer) ||
            buffer.Contents.Count == 0)
        {
            return Loc.GetString("cmu-chem-synth-buffer", ("contents", Loc.GetString("cmu-chem-synth-buffer-empty")));
        }

        var entries = new List<string>();
        foreach (var quantity in buffer.Contents)
        {
            entries.Add(Loc.GetString("cmu-chem-synth-buffer-entry",
                ("name", ReagentName(quantity.Reagent.Prototype)),
                ("amount", quantity.Quantity.ToString())));
        }

        return Loc.GetString("cmu-chem-synth-buffer", ("contents", string.Join(", ", entries)));
    }

    private void UpdatePlan(CMUChemSynthesizerComponent comp)
    {
        var key = $"{comp.PlanIndex}|{comp.Stage}";
        foreach (var step in comp.Plan)
        {
            key += $"|{step.Reaction}:{step.Product}:{step.Batches}";
        }

        if (key == _lastPlan)
            return;

        _lastPlan = key;
        _plan.RemoveAllChildren();
        if (comp.Plan.Count == 0)
        {
            _plan.AddChild(new Label { Text = Loc.GetString("cmu-chem-synth-plan-none") });
            return;
        }

        for (var i = 0; i < comp.Plan.Count; i++)
        {
            var step = comp.Plan[i];
            var label = new Label
            {
                Text = Loc.GetString("cmu-chem-synth-plan-step",
                    ("index", i + 1),
                    ("name", ReagentName(step.Product)),
                    ("batches", step.Batches)),
            };

            if (i == comp.PlanIndex && comp.Stage != CMUChemSynthesizerStage.Idle)
                label.FontColorOverride = CurrentStepColor;

            _plan.AddChild(label);
        }
    }
}
