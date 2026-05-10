using System;
using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._CMU14.Medical.Surgery;
using Content.Shared.Body.Part;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.Medical.Surgery;

[UsedImplicitly]
public sealed class CMULimbPrinterBui : BoundUserInterface
{
    private CMULimbPrinterWindow? _window;

    public CMULimbPrinterBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMULimbPrinterWindow>();
        _window.Title = Loc.GetString("cmu-limb-printer-window-title");
        _window.EjectBeakerButton.OnPressed += _ => SendMessage(new CMULimbPrinterEjectBeakerMessage());
        _window.EjectSyringeButton.OnPressed += _ => SendMessage(new CMULimbPrinterEjectSyringeMessage());

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
        _window.BeakerLabel.Text = state.BeakerName ?? Loc.GetString("cmu-limb-printer-no-beaker");
        _window.SyringeLabel.Text = state.SyringeName ?? Loc.GetString("cmu-limb-printer-no-syringe");
        _window.MatrixAmountLabel.Text = Loc.GetString(
            "cmu-limb-printer-fluid-amount",
            ("current", MathF.Round(state.SynthesisUnits, 1)),
            ("max", MathF.Round(state.SynthesisMaxUnits, 1)));
        _window.BloodAmountLabel.Text = Loc.GetString(
            "cmu-limb-printer-fluid-amount",
            ("current", MathF.Round(state.BloodUnits, 1)),
            ("max", MathF.Round(state.BloodMaxUnits, 1)));
        _window.MatrixCostLabel.Text = Loc.GetString("cmu-limb-printer-matrix-cost", ("cost", state.SynthesisCost));
        _window.BloodCostLabel.Text = Loc.GetString("cmu-limb-printer-blood-cost", ("cost", state.BloodCost));

        SetBar(_window.MatrixBar, state.SynthesisUnits, state.SynthesisMaxUnits);
        SetBar(_window.BloodBar, state.BloodUnits, state.BloodMaxUnits);

        _window.EjectBeakerButton.Disabled = state.BeakerName is null;
        _window.EjectSyringeButton.Disabled = state.SyringeName is null;

        _window.LeftList.RemoveAllChildren();
        _window.RightList.RemoveAllChildren();
        foreach (var option in state.Options.Where(o => o.Symmetry == BodyPartSymmetry.Left))
            _window.LeftList.AddChild(BuildOption(option));
        foreach (var option in state.Options.Where(o => o.Symmetry == BodyPartSymmetry.Right))
            _window.RightList.AddChild(BuildOption(option));
    }

    private Control BuildOption(CMULimbPrinterOption option)
    {
        var button = new Button
        {
            HorizontalExpand = true,
            MinHeight = 66,
            Disabled = !option.CanPrint,
            ToolTip = option.CanPrint ? option.Name : option.DisabledReason,
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(8, 6),
            HorizontalExpand = true,
        };

        var icon = new EntityPrototypeView
        {
            MinSize = new Vector2(48, 48),
            Scale = new Vector2(2.2f, 2.2f),
            Stretch = SpriteView.StretchMode.Fill,
            VerticalAlignment = Control.VAlignment.Center,
        };
        icon.SetPrototype(option.Prototype);
        row.AddChild(icon);

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        text.AddChild(new Label
        {
            Text = option.Name,
            StyleClasses = { "LabelKeyText" },
            FontColorOverride = CMUMedicalMachineStyle.Text,
            ClipText = true,
        });
        text.AddChild(new Label
        {
            Text = option.CanPrint
                ? Loc.GetString("cmu-limb-printer-print-ready")
                : option.DisabledReason,
            StyleClasses = { "LabelSubText" },
            FontColorOverride = option.CanPrint ? CMUMedicalMachineStyle.Cyan : CMUMedicalMachineStyle.Dim,
            ClipText = true,
        });
        row.AddChild(text);

        button.AddChild(CMUMedicalMachineStyle.Wrap(
            row,
            option.CanPrint ? CMUMedicalMachineStyle.DeepCardBg : CMUMedicalMachineStyle.Surface,
            option.CanPrint ? CMUMedicalMachineStyle.Cyan : CMUMedicalMachineStyle.MutedBorder,
            new Thickness(0),
            new Thickness(2)));

        button.OnPressed += _ => SendMessage(new CMULimbPrinterPrintMessage(option.Type, option.Symmetry));
        return button;
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
    public Label BeakerLabel = default!;
    public Label SyringeLabel = default!;
    public Label MatrixAmountLabel = default!;
    public Label BloodAmountLabel = default!;
    public Label MatrixCostLabel = default!;
    public Label BloodCostLabel = default!;
    public ProgressBar MatrixBar = default!;
    public ProgressBar BloodBar = default!;
    public Button EjectBeakerButton = default!;
    public Button EjectSyringeButton = default!;
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
