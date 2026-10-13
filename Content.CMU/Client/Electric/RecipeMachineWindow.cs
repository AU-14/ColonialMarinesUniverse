// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Machines;
using Content.Client.Power.Components;
using Content.Shared.CMU14.Electric;
using Content.Shared.CMU14.Fluids;
using Content.Shared.CMU14.Steam;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Electric;

// The window of a machine of the electric age: what goes in on the left, what comes out on the right, the circuit under them
// when the machine has recipes that ask for one, the liquids in its tank, and the list of everything it can make.
public sealed class RecipeMachineWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private readonly ItemSlotsSystem _itemSlots;
    private readonly SharedStackSystem _stacks;

    private EntityUid _owner;

    private readonly List<SlotView> _slots = new();
    private readonly BoxContainer _inputsHolder;
    private readonly BoxContainer _outputsHolder;
    private readonly Label _statusLabel;
    private readonly ProgressBar _progressBar;
    private readonly BoxContainer _circuitRow;
    private readonly Label _circuitLabel;
    private readonly Label _tankLabel;
    private readonly BoxContainer _liquids;
    private MachineWindowKit.FluidGauge? _steam;
    private readonly BoxContainer _recipeList;
    private bool _listed;

    public event Action<string>? OnSlotPressed;
    public event Action<int>? OnCircuitChosen;
    public event Action<bool, bool>? OnAutoChosen;

    private readonly CheckBox _autoOutput;
    private readonly CheckBox _autoInput;
    private bool _settingAuto;

    public RecipeMachineWindow()
    {
        IoCManager.InjectDependencies(this);
        _itemSlots = _entMan.System<ItemSlotsSystem>();
        _stacks = _entMan.System<SharedStackSystem>();

        MinSize = new Vector2(520, 520);

        var root = MachineWindowKit.Column();

        _statusLabel = new Label { HorizontalAlignment = Control.HAlignment.Center };
        root.AddChild(_statusLabel);

        var main = MachineWindowKit.Row(24);

        // The slots are made when the machine is known: a machine can have more of them than four (the fabricator takes eighteen).
        _inputsHolder = MachineWindowKit.Column(0);
        main.AddChild(MachineWindowKit.Section(Loc.GetString("au14-window-materials"), _inputsHolder));

        _outputsHolder = MachineWindowKit.Column(0);
        main.AddChild(MachineWindowKit.Section(Loc.GetString("au14-window-output"), _outputsHolder));

        _tankLabel = new Label { FontColorOverride = MachineWindowKit.SectionTitle };
        _liquids = MachineWindowKit.Column(4);
        _liquids.AddChild(_tankLabel);
        main.AddChild(MachineWindowKit.Section(Loc.GetString("au14-machine-liquids"), _liquids));

        root.AddChild(main);

        _circuitRow = MachineWindowKit.Row(6);
        _circuitRow.AddChild(new Label { Text = Loc.GetString("au14-machine-circuit"), VerticalAlignment = Control.VAlignment.Center });
        var minus = new Button { Text = "-" };
        minus.OnPressed += _ => Step(-1);
        _circuitRow.AddChild(minus);
        _circuitLabel = new Label { MinWidth = 40, HorizontalAlignment = Control.HAlignment.Center, VerticalAlignment = Control.VAlignment.Center };
        _circuitRow.AddChild(_circuitLabel);
        var plus = new Button { Text = "+" };
        plus.OnPressed += _ => Step(1);
        _circuitRow.AddChild(plus);
        _circuitRow.Visible = false;
        root.AddChild(_circuitRow);

        var autoRow = MachineWindowKit.Row(12);
        _autoOutput = new CheckBox { Text = Loc.GetString("au14-machine-auto-output") };
        _autoOutput.OnToggled += args => { if (!_settingAuto) OnAutoChosen?.Invoke(true, args.Pressed); };
        autoRow.AddChild(_autoOutput);
        _autoInput = new CheckBox { Text = Loc.GetString("au14-machine-auto-input") };
        _autoInput.OnToggled += args => { if (!_settingAuto) OnAutoChosen?.Invoke(false, args.Pressed); };
        autoRow.AddChild(_autoInput);
        root.AddChild(autoRow);

        _progressBar = MachineWindowKit.Bar(400);
        root.AddChild(_progressBar);

        root.AddChild(new Label { Text = Loc.GetString("au14-machine-recipes"), FontColorOverride = MachineWindowKit.SectionTitle });

        _recipeList = MachineWindowKit.Column(2);
        var scroll = new ScrollContainer { VerticalExpand = true, MinSize = new Vector2(480, 200), HScrollEnabled = false };
        scroll.AddChild(_recipeList);
        root.AddChild(scroll);

        Contents.AddChild(root);
    }

    private Control Add(string slotId, Color border)
    {
        var slot = MachineWindowKit.MakeSlot(_entMan, slotId, border, id => OnSlotPressed?.Invoke(id));
        _slots.Add(slot);
        return slot.Button;
    }

    private void Step(int delta)
    {
        if (!_entMan.TryGetComponent<RecipeMachineComponent>(_owner, out var comp))
            return;

        var next = comp.Circuit + delta;
        if (next < 0)
            next = comp.MaxCircuit;
        else if (next > comp.MaxCircuit)
            next = 0;

        OnCircuitChosen?.Invoke(next);
    }

    public void SetOwner(EntityUid owner)
    {
        _owner = owner;
        Title = _entMan.GetComponent<MetaDataComponent>(owner).EntityName;

        if (_slots.Count == 0 && _entMan.TryGetComponent<RecipeMachineComponent>(owner, out var machine))
        {
            // Many slots lie in rows of six; a few in two columns.
            var inputs = MachineWindowKit.Grid(machine.InputSlots > 8 ? 6 : 2);
            foreach (var id in machine.InputIds)
                inputs.AddChild(Add(id, MachineWindowKit.InputBorder));
            _inputsHolder.AddChild(inputs);

            var outputs = MachineWindowKit.Grid(machine.OutputSlots > 8 ? 6 : 2);
            foreach (var id in machine.OutputIds)
                outputs.AddChild(Add(id, MachineWindowKit.OutputBorder));
            _outputsHolder.AddChild(outputs);
        }

        // A steam machine shows the steam it has.
        if (_steam == null && _entMan.HasComponent<SteamPortComponent>(owner))
        {
            _steam = new MachineWindowKit.FluidGauge("steam", Loc.GetString("au14-window-steam"), MachineWindowKit.SteamColor);
            _liquids.AddChild(_steam.Root);
        }

        FillRecipeList();
    }

    private void FillRecipeList()
    {
        if (_listed || !_entMan.TryGetComponent<RecipeMachineComponent>(_owner, out var comp))
            return;

        _listed = true;
        var recipes = _proto.EnumeratePrototypes<MachineRecipePrototype>()
            .Where(recipe => recipe.Machine == comp.Kind)
            .OrderBy(recipe => recipe.Circuit ?? -1)
            .ThenBy(recipe => recipe.ID, StringComparer.Ordinal)
            .ToList();

        _circuitRow.Visible = recipes.Any(recipe => recipe.Circuit != null);

        foreach (var recipe in recipes)
        {
            var text = new RichTextLabel { HorizontalExpand = true };
            text.SetMessage(FormattedMessage.FromUnformatted(Describe(recipe)));
            _recipeList.AddChild(text);
        }
    }

    private string Describe(MachineRecipePrototype recipe)
    {
        var inputs = new List<string>();
        foreach (var (stackId, amount) in recipe.Inputs)
            inputs.Add($"{amount}x {StackName(stackId)}");
        foreach (var (stackId, amount) in recipe.Catalysts)
            inputs.Add($"[{amount}x {StackName(stackId)}]");
        foreach (var (proto, amount) in recipe.ItemInputs)
            inputs.Add($"{amount}x {(_proto.TryIndex<EntityPrototype>(proto, out var entity) ? entity.Name : proto.Id)}");
        foreach (var (kind, amount) in recipe.FluidInputs)
            inputs.Add($"{(int) amount} {MachineWindowKit.LiquidName(_entMan, kind)}");

        var outputs = new List<string>();
        foreach (var (stackId, amount) in recipe.Outputs)
            outputs.Add($"{amount}x {StackName(stackId)}");
        foreach (var (proto, amount) in recipe.ItemOutputs)
            outputs.Add($"{amount}x {(_proto.TryIndex<EntityPrototype>(proto, out var entity) ? entity.Name : proto.Id)}");
        foreach (var (kind, amount) in recipe.FluidOutputs)
            outputs.Add($"{(int) amount} {MachineWindowKit.LiquidName(_entMan, kind)}");

        var prefix = recipe.Circuit is { } circuit ? Loc.GetString("au14-machine-recipe-circuit", ("circuit", circuit)) + " " : string.Empty;
        return $"{prefix}{string.Join(" + ", inputs)} -> {string.Join(" + ", outputs)} ({recipe.Duration:0.#} {Loc.GetString("au14-machine-seconds")})";
    }

    private string StackName(string stackId)
    {
        if (_proto.TryIndex<StackPrototype>(stackId, out var stack) && _proto.TryIndex(stack.Spawn, out var entity))
            return entity.Name;

        return stackId;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_owner == EntityUid.Invalid || !_entMan.TryGetComponent<RecipeMachineComponent>(_owner, out var comp))
            return;

        FillRecipeList();

        foreach (var slot in _slots)
            MachineWindowKit.Refresh(_entMan, _itemSlots, _stacks, _owner, slot);

        _circuitLabel.Text = comp.Circuit.ToString();

        // The switches show what the machine has; a click on one of them is not echoed back.
        var auto = _entMan.GetComponentOrNull<AutoPortsComponent>(_owner);
        _autoOutput.Visible = _autoInput.Visible = auto != null;
        if (auto != null)
        {
            _settingAuto = true;
            _autoOutput.Pressed = auto.AutoOutput;
            _autoInput.Pressed = auto.AutoInput;
            _settingAuto = false;
        }
        _tankLabel.Text = _entMan.TryGetComponent<FluidTankComponent>(_owner, out var tank)
            ? MachineWindowKit.TankText(_entMan, tank)
            : Loc.GetString("au14-window-storage-empty");

        var steamDriven = _entMan.TryGetComponent<SteamPortComponent>(_owner, out var port);
        if (steamDriven && _steam != null)
            _steam.UpdateSteam(port!);

        var powered = steamDriven
            ? port!.Moles > 0.01f
            : _entMan.TryGetComponent<ApcPowerReceiverComponent>(_owner, out var apc) && apc.Powered;
        _progressBar.Value = comp.ActiveRecipe != null ? comp.Progress : 0f;

        _statusLabel.Text = !powered
            ? Loc.GetString("au14-machine-status-unpowered")
            : comp.Blocked
                ? Loc.GetString("au14-machine-status-blocked")
                : comp.ActiveRecipe != null
                    ? Loc.GetString("au14-machine-status-working", ("percent", (int) (comp.Progress * 100)))
                    : Loc.GetString("au14-machine-status-ready");
    }
}
