using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared.Body.Part;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.Medical.Treatment.Surgery;

[UsedImplicitly]
public sealed class CMULimbPrinterBui : BoundUserInterface
{
    private readonly Dictionary<CMULimbPrinterOptionKey, CMULimbPrinterOptionRow> _optionRows = new();
    private readonly HashSet<CMULimbPrinterOptionKey> _seenOptionKeys = new();
    private readonly List<CMULimbPrinterOptionKey> _removedOptionKeys = new();

    private CMULimbPrinterWindow? _window;

    public CMULimbPrinterBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        ClearOptionRows();
        _window = this.CreateWindow<CMULimbPrinterWindow>();
        _window.Title = Loc.GetString("cmu-limb-printer-window-title");
        _window.EjectBeakerButton.OnPressed += _ => SendMessage(new CMULimbPrinterEjectBeakerMessage());
        _window.EjectSyringeButton.OnPressed += _ => SendMessage(new CMULimbPrinterEjectSyringeMessage());
        _window.EjectMaterialButton.OnPressed += _ => SendMessage(new CMULimbPrinterEjectMaterialMessage());

        if (State is CMULimbPrinterBuiState state)
            Refresh(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is CMULimbPrinterBuiState limbPrinter)
            Refresh(limbPrinter);
    }

    private void Refresh(CMULimbPrinterBuiState state)
    {
        if (_window is null)
            return;

        _window.StatusLabel.Text = state.Status;
        _window.MatrixTitleLabel.Text = state.SynthesisReagentName;
        _window.MetalTitleLabel.Text = state.RoboticMetalName;
        _window.BeakerLabel.Text = state.BeakerName ?? Loc.GetString("cmu-limb-printer-no-beaker");
        _window.SyringeLabel.Text = state.SyringeName ?? Loc.GetString("cmu-limb-printer-no-syringe");
        _window.MaterialLabel.Text = state.MaterialName ?? Loc.GetString("cmu-limb-printer-no-metal");
        _window.MatrixAmountLabel.Text = Loc.GetString(
            "cmu-limb-printer-fluid-amount",
            ("current", MathF.Round(state.SynthesisUnits, 1)),
            ("max", MathF.Round(state.SynthesisMaxUnits, 1)));
        _window.BloodAmountLabel.Text = Loc.GetString(
            "cmu-limb-printer-fluid-amount",
            ("current", MathF.Round(state.BloodUnits, 1)),
            ("max", MathF.Round(state.BloodMaxUnits, 1)));
        _window.MaterialAmountLabel.Text = Loc.GetString(
            "cmu-limb-printer-stack-amount",
            ("current", state.RoboticMetalUnits),
            ("max", state.RoboticMetalMaxUnits));
        _window.MatrixCostLabel.Text = Loc.GetString("cmu-limb-printer-matrix-cost", ("cost", state.SynthesisCost));
        _window.BloodCostLabel.Text = Loc.GetString("cmu-limb-printer-blood-cost", ("cost", state.BloodCost));
        _window.MaterialCostLabel.Text = Loc.GetString("cmu-limb-printer-metal-cost", ("cost", state.RoboticMetalCost));

        SetBar(_window.MatrixBar, state.SynthesisUnits, state.SynthesisMaxUnits);
        SetBar(_window.BloodBar, state.BloodUnits, state.BloodMaxUnits);
        SetBar(_window.MaterialBar, state.RoboticMetalUnits, state.RoboticMetalMaxUnits);

        _window.EjectBeakerButton.Disabled = state.BeakerName is null;
        _window.EjectSyringeButton.Disabled = state.SyringeName is null;
        _window.EjectMaterialButton.Disabled = state.MaterialName is null;

        _seenOptionKeys.Clear();
        var leftIndex = 0;
        var rightIndex = 0;
        foreach (var option in state.Options)
        {
            if (option.Symmetry is not (BodyPartSymmetry.Left or BodyPartSymmetry.Right))
                continue;

            var key = new CMULimbPrinterOptionKey(option.Kind, option.Type, option.Symmetry);
            _seenOptionKeys.Add(key);

            if (!_optionRows.TryGetValue(key, out var row))
            {
                row = new CMULimbPrinterOptionRow(option, OnOptionPressed);
                _optionRows.Add(key, row);

                if (option.Symmetry == BodyPartSymmetry.Left)
                    _window.LeftList.AddChild(row);
                else if (option.Symmetry == BodyPartSymmetry.Right)
                    _window.RightList.AddChild(row);
            }
            else
            {
                row.SetOption(option);
            }

            if (option.Symmetry == BodyPartSymmetry.Left)
            {
                if (row.GetPositionInParent() != leftIndex)
                    row.SetPositionInParent(leftIndex);
                leftIndex++;
            }
            else if (option.Symmetry == BodyPartSymmetry.Right)
            {
                if (row.GetPositionInParent() != rightIndex)
                    row.SetPositionInParent(rightIndex);
                rightIndex++;
            }
        }

        _removedOptionKeys.Clear();
        foreach (var (key, _) in _optionRows)
        {
            if (!_seenOptionKeys.Contains(key))
                _removedOptionKeys.Add(key);
        }

        foreach (var key in _removedOptionKeys)
        {
            var row = _optionRows[key];
            _optionRows.Remove(key);
            row.Release();
        }
    }

    private void OnOptionPressed(CMULimbPrinterOption option)
    {
        SendMessage(new CMULimbPrinterPrintMessage(option.Kind, option.Type, option.Symmetry));
    }

    private void ClearOptionRows()
    {
        foreach (var row in _optionRows.Values)
            row.Release();

        _optionRows.Clear();
        _seenOptionKeys.Clear();
        _removedOptionKeys.Clear();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ClearOptionRows();

        base.Dispose(disposing);
    }

    private readonly record struct CMULimbPrinterOptionKey(
        CMULimbPrinterPrintKind Kind,
        BodyPartType Type,
        BodyPartSymmetry Symmetry);

    private sealed class CMULimbPrinterOptionRow : Button
    {
        private readonly EntityPrototypeView _icon;
        private readonly Label _nameLabel;
        private readonly PanelContainer _panel;
        private readonly Label _statusLabel;
        private Action<CMULimbPrinterOption>? _onSelected;
        private CMULimbPrinterOption? _option;

        public CMULimbPrinterOptionRow(
            CMULimbPrinterOption option,
            Action<CMULimbPrinterOption> onSelected)
        {
            _onSelected = onSelected;

            HorizontalExpand = true;
            MinHeight = 66;

            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 8,
                Margin = new Thickness(8, 6),
                HorizontalExpand = true,
            };

            _icon = new EntityPrototypeView
            {
                MinSize = new Vector2(48, 48),
                Scale = new Vector2(2.2f, 2.2f),
                Stretch = SpriteView.StretchMode.Fill,
                VerticalAlignment = Control.VAlignment.Center,
            };
            row.AddChild(_icon);

            var text = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                VerticalAlignment = Control.VAlignment.Center,
            };
            _nameLabel = new Label
            {
                StyleClasses = { "LabelKeyText" },
                FontColorOverride = CMUMedicalMachineStyle.Text,
                ClipText = true,
            };
            _statusLabel = new Label
            {
                StyleClasses = { "LabelSubText" },
                ClipText = true,
            };
            text.AddChild(_nameLabel);
            text.AddChild(_statusLabel);
            row.AddChild(text);

            _panel = CMUMedicalMachineStyle.Wrap(
                row,
                CMUMedicalMachineStyle.DeepCardBg,
                CMUMedicalMachineStyle.Cyan,
                new Thickness(0),
                new Thickness(2));
            AddChild(_panel);

            OnPressed += OnButtonPressed;
            SetOption(option);
        }

        public void SetOption(CMULimbPrinterOption option)
        {
            if (_option == option)
                return;

            var prototypeChanged = _option is null || _option.Prototype != option.Prototype;
            var availabilityChanged = _option is null || _option.CanPrint != option.CanPrint;
            _option = option;
            Disabled = !option.CanPrint;
            ToolTip = option.CanPrint ? option.Name : option.DisabledReason;
            if (prototypeChanged)
                _icon.SetPrototype(option.Prototype);
            _nameLabel.Text = option.Name;
            _statusLabel.Text = option.CanPrint
                ? Loc.GetString("cmu-limb-printer-print-ready")
                : option.DisabledReason;
            _statusLabel.FontColorOverride = option.CanPrint
                ? CMUMedicalMachineStyle.Cyan
                : CMUMedicalMachineStyle.Dim;

            if (availabilityChanged)
            {
                _panel.PanelOverride = CMUMedicalMachineStyle.Flat(
                    option.CanPrint ? CMUMedicalMachineStyle.DeepCardBg : CMUMedicalMachineStyle.Surface,
                    option.CanPrint ? CMUMedicalMachineStyle.Cyan : CMUMedicalMachineStyle.MutedBorder,
                    new Thickness(2));
            }
        }

        private void OnButtonPressed(BaseButton.ButtonEventArgs args)
        {
            if (_option is { } option)
                _onSelected?.Invoke(option);
        }

        public void Release()
        {
            OnPressed -= OnButtonPressed;
            _onSelected = null;
            _option = null;
            _icon.SetPrototype(null);

            if (Parent is not null)
                Orphan();
        }
    }

    private static void SetBar(ProgressBar bar, float value, float max)
    {
        bar.MinValue = 0f;
        bar.MaxValue = 1f;
        bar.Value = max <= 0f ? 0f : Math.Clamp(value / max, 0f, 1f);
    }
}

public sealed class CMULimbPrinterWindow : FancyWindow
{
    public Label StatusLabel = default!;
    public Label MatrixTitleLabel = default!;
    public Label MetalTitleLabel = default!;
    public Label BeakerLabel = default!;
    public Label SyringeLabel = default!;
    public Label MaterialLabel = default!;
    public Label MatrixAmountLabel = default!;
    public Label BloodAmountLabel = default!;
    public Label MaterialAmountLabel = default!;
    public Label MatrixCostLabel = default!;
    public Label BloodCostLabel = default!;
    public Label MaterialCostLabel = default!;
    public ProgressBar MatrixBar = default!;
    public ProgressBar BloodBar = default!;
    public ProgressBar MaterialBar = default!;
    public Button EjectBeakerButton = default!;
    public Button EjectSyringeButton = default!;
    public Button EjectMaterialButton = default!;
    public BoxContainer LeftList = default!;
    public BoxContainer RightList = default!;

    public CMULimbPrinterWindow()
    {
        IoCManager.InjectDependencies(this);
        SetSize = new Vector2(820, 560);
        MinSize = new Vector2(760, 500);

        ContentsContainer.AddChild(BuildRoot());
    }

    private Control BuildRoot()
    {
        var rootPanel = CMUMedicalMachineStyle.Panel(CMUMedicalMachineStyle.WindowBg, CMUMedicalMachineStyle.Border, new Thickness(2));
        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(10),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        rootPanel.AddChild(root);

        var header = CMUMedicalMachineStyle.Panel(CMUMedicalMachineStyle.DeepCardBg, CMUMedicalMachineStyle.Cyan);
        var headerStack = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(9, 7),
            HorizontalExpand = true,
        };
        header.AddChild(headerStack);
        headerStack.AddChild(new Label
        {
            Text = Loc.GetString("cmu-limb-printer-header"),
            StyleClasses = { "LabelHeading" },
            FontColorOverride = CMUMedicalMachineStyle.Text,
        });
        StatusLabel = new Label
        {
            Text = string.Empty,
            FontColorOverride = CMUMedicalMachineStyle.Cyan,
            ClipText = true,
        };
        headerStack.AddChild(StatusLabel);
        root.AddChild(header);

        var fluids = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        root.AddChild(fluids);
        fluids.AddChild(BuildFluidPanel(
            Loc.GetString("cmu-limb-printer-matrix-heading"),
            CMUMedicalMachineStyle.Cyan,
            out MatrixTitleLabel,
            out BeakerLabel,
            out MatrixAmountLabel,
            out MatrixCostLabel,
            out MatrixBar,
            out EjectBeakerButton,
            Loc.GetString("cmu-limb-printer-remove-beaker")));
        fluids.AddChild(BuildFluidPanel(
            Loc.GetString("cmu-limb-printer-blood-heading"),
            CMUMedicalMachineStyle.Red,
            out _,
            out SyringeLabel,
            out BloodAmountLabel,
            out BloodCostLabel,
            out BloodBar,
            out EjectSyringeButton,
            Loc.GetString("cmu-limb-printer-remove-syringe")));
        fluids.AddChild(BuildFluidPanel(
            Loc.GetString("cmu-limb-printer-metal-heading"),
            CMUMedicalMachineStyle.Warning,
            out MetalTitleLabel,
            out MaterialLabel,
            out MaterialAmountLabel,
            out MaterialCostLabel,
            out MaterialBar,
            out EjectMaterialButton,
            Loc.GetString("cmu-limb-printer-remove-metal")));

        var columns = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        root.AddChild(columns);

        LeftList = CMUMedicalMachineStyle.MakeTitledList(columns, Loc.GetString("cmu-limb-printer-left-heading"), 360, true);
        RightList = CMUMedicalMachineStyle.MakeTitledList(columns, Loc.GetString("cmu-limb-printer-right-heading"), 360, true);

        return rootPanel;
    }

    private Control BuildFluidPanel(
        string heading,
        Color accent,
        out Label titleLabel,
        out Label containerLabel,
        out Label amountLabel,
        out Label costLabel,
        out ProgressBar bar,
        out Button ejectButton,
        string ejectText)
    {
        var panel = CMUMedicalMachineStyle.Panel(CMUMedicalMachineStyle.CardBg, CMUMedicalMachineStyle.Border);
        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
            HorizontalExpand = true,
        };
        panel.AddChild(root);

        root.AddChild(new Label
        {
            Text = heading,
            StyleClasses = { "LabelHeading" },
            FontColorOverride = CMUMedicalMachineStyle.Text,
        });

        titleLabel = new Label
        {
            Text = string.Empty,
            FontColorOverride = accent,
            ClipText = true,
        };
        root.AddChild(titleLabel);

        containerLabel = new Label
        {
            Text = string.Empty,
            FontColorOverride = CMUMedicalMachineStyle.Muted,
            ClipText = true,
        };
        root.AddChild(containerLabel);

        bar = new ProgressBar
        {
            HorizontalExpand = true,
            MinHeight = 14,
            BackgroundStyleBoxOverride = CMUMedicalMachineStyle.Flat(CMUMedicalMachineStyle.Surface, CMUMedicalMachineStyle.MutedBorder),
            ForegroundStyleBoxOverride = CMUMedicalMachineStyle.Flat(accent, accent),
        };
        root.AddChild(bar);

        var amountRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        root.AddChild(amountRow);

        amountLabel = new Label
        {
            Text = string.Empty,
            FontColorOverride = CMUMedicalMachineStyle.Text,
            HorizontalExpand = true,
            ClipText = true,
        };
        amountRow.AddChild(amountLabel);

        costLabel = new Label
        {
            Text = string.Empty,
            FontColorOverride = CMUMedicalMachineStyle.Dim,
            ClipText = true,
        };
        amountRow.AddChild(costLabel);

        ejectButton = CMUMedicalMachineStyle.ActionButton(ejectText, accent);
        root.AddChild(ejectButton);

        return panel;
    }
}
