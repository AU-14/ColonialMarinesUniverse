using Content.Client.CMU14.UI;
using System.Linq;
using Content.Client._RMC14.UserInterface;
using Content.Client.Chemistry.Containers.EntitySystems;
using Content.Client.Resources;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._RMC14.Chemistry.SmartFridge;
using Content.Shared._RMC14.Chemistry.TuringDispenser;
using Content.Shared._RMC14.UserInterface;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using static Content.Client._RMC14.UserInterface.RMCMachineUiStyle;

namespace Content.Client._RMC14.Chemistry.TuringDispenser;

[UsedImplicitly]
public sealed partial class RMCTuringDispenserBui : BoundUserInterface, IRefreshableBui
{
    private readonly SharedCMAutomatedVendorSystem _cmVendor;
    private readonly ItemSlotsSystem _itemSlots;
    private readonly IPrototypeManager _prototype;
    private readonly IResourceCache _resourceCache;
    private readonly RMCReagentSystem _rmcReagent;
    private readonly SharedRMCSmartFridgeSystem _smartFridge;
    private readonly SolutionContainerSystem _solution;
    private readonly SharedRMCTuringDispenserSystem _turing;

    private RMCTuringDispenserWindow? _window;
    private readonly List<RMCTuringDispenserBeakerRow> _beakerRows = new();

    public RMCTuringDispenserBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _cmVendor = EntMan.System<SharedCMAutomatedVendorSystem>();
        _itemSlots = EntMan.System<ItemSlotsSystem>();
        _prototype = IoCManager.Resolve<IPrototypeManager>();
        _resourceCache = IoCManager.Resolve<IResourceCache>();
        _rmcReagent = EntMan.System<RMCReagentSystem>();
        _smartFridge = EntMan.System<SharedRMCSmartFridgeSystem>();
        _solution = EntMan.System<SolutionContainerSystem>();
        _turing = EntMan.System<SharedRMCTuringDispenserSystem>();
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<RMCTuringDispenserWindow>();

        if (!EntMan.TryGetComponent(Owner, out RMCTuringDispenserComponent? comp))
            return;

        var autoRow = new RMCTuringDispenserBeakerRow { Id = null };
        autoRow.Icon.Texture = _resourceCache.GetTexture(CMUIcons.ArrowsRotate);
        autoRow.Icon.ModulateSelfOverride = CMUIcons.Idle;
        autoRow.NameLabel.Text = Loc.GetString("cmu-turing-dispenser-beaker-auto"); // CMU14: fluent
        autoRow.OnPressed += _ => SendPredictedMessage(new RMCTuringDispenserSetPreferredBeakerBuiMsg(null));
        _window.BeakerSelectorContainer.AddChild(autoRow);
        _beakerRows.Add(autoRow);

        foreach (var option in comp.BeakerOptions)
        {
            var id = option.Id;
            var row = new RMCTuringDispenserBeakerRow { Id = id };

            var name = id.Id;
            if (_prototype.TryIndex(id, out var entityProto))
            {
                name = entityProto.Name;
                var textures = SpriteComponent.GetPrototypeTextures(entityProto, _resourceCache).Select(o => o.Default).ToList();
                if (textures.Count > 0)
                    row.Icon.Texture = textures[0];
            }

            row.NameLabel.Text = $"{name} ({option.MaxVolume}u)";
            row.OnPressed += _ => SendPredictedMessage(new RMCTuringDispenserSetPreferredBeakerBuiMsg(id));

            _window.BeakerSelectorContainer.AddChild(row);
            _beakerRows.Add(row);
        }

        _window.SaveToMemoryButton.OnPressed += _ => SendPredictedMessage(new RMCTuringDispenserSaveToMemoryBuiMsg());
        _window.EjectBoxButton.OnPressed += _ => SendPredictedMessage(new RMCTuringDispenserEjectBoxBuiMsg());
        _window.DisposeBeakerButton.OnPressed += _ => SendPredictedMessage(new RMCTuringDispenserDisposeBeakerBuiMsg());
        _window.EjectBeakerButton.OnPressed += _ => SendPredictedMessage(new RMCTuringDispenserEjectBeakerBuiMsg());
        _window.ClearMemoryButton.OnPressed += _ => SendPredictedMessage(new RMCTuringDispenserClearMemoryBuiMsg());
        _window.RunProgramButton.OnPressed += _ => SendPredictedMessage(new RMCTuringDispenserRunProgramBuiMsg());
        _window.AutoRunButton.OnPressed += _ => SendPredictedMessage(new RMCTuringDispenserToggleAutoRunBuiMsg());
        _window.SmartLinkButton.OnPressed += _ => SendPredictedMessage(new RMCTuringDispenserToggleSmartLinkBuiMsg());
        _window.OutputModeButton.OnPressed += _ => SendPredictedMessage(new RMCTuringDispenserToggleOutputModeBuiMsg());

        var minMultiplier = comp.MinMultiplier.Float();
        var maxMultiplier = comp.MaxMultiplier.Float();
        _window.MultiplierSpinBox.IsValid = value => value >= minMultiplier && value <= maxMultiplier;
        _window.MultiplierSpinBox.OnValueChanged += args =>
            SendPredictedMessage(new RMCTuringDispenserSetMultiplierBuiMsg(FixedPoint2.New(args.Value)));

        var minCycles = comp.MinCycles;
        var maxCycles = comp.MaxCycles;
        _window.CyclesSpinBox.IsValid = value => value >= minCycles && value <= maxCycles;
        _window.CyclesSpinBox.OnValueChanged += args =>
            SendPredictedMessage(new RMCTuringDispenserSetCyclesBuiMsg((int) args.Value));

        Refresh();
    }

    public void Refresh()
    {
        if (_window is not { IsOpen: true })
            return;

        if (!EntMan.TryGetComponent(Owner, out RMCTuringDispenserComponent? comp))
            return;

        var maxEnergy = comp.MaxEnergy;
        var energy = comp.Energy;
        _window.EnergyBar.MaxValue = maxEnergy.Float();
        _window.EnergyBar.Value = energy.Float();
        // CMU14: fluent energy label
        var percent = maxEnergy.Float() > 0 ? energy.Float() / maxEnergy.Float() * 100 : 0;
        _window.EnergyLabel.Text = Loc.GetString("cmu-turing-dispenser-energy", ("percent", percent.ToString("0")));

        // CMU14: fluent status text
        var (statusText, statusColor) = comp.Status switch
        {
            TuringDispenserStatus.Idle => (Loc.GetString("cmu-turing-dispenser-status-idle"), TanColor),
            TuringDispenserStatus.Running => (Loc.GetString("cmu-turing-dispenser-status-running"), OrangeColor),
            TuringDispenserStatus.Finished => (Loc.GetString("cmu-turing-dispenser-status-finished"), GreenColor),
            TuringDispenserStatus.Stuck => (Loc.GetString("cmu-turing-dispenser-status-stuck", ("error", comp.Error ?? string.Empty)), RedColor),
            _ => (Loc.GetString("cmu-turing-dispenser-status-idle"), TanColor),
        };
        _window.SetStatusRow(_window.StatusPanel, _window.StatusLabel, statusText, statusColor);

        UpdateBeakerRows((Owner, comp));

        UpdateProgramRows(comp, _window.MemoryProgramContainer, _window.MemoryEmptyPanel, _window.MemoryEmptyLabel, comp.MemoryProgram);
        UpdateProgramRows(comp, _window.BoxProgramContainer, _window.BoxEmptyPanel, _window.BoxEmptyLabel, comp.BoxProgram);

        if (_itemSlots.GetItemOrNull(Owner, comp.InputBoxSlotId) != null)
            _window.SetStatusRow(_window.BoxAlertPanel, _window.BoxStatusLabel, Loc.GetString("cmu-turing-dispenser-box-loaded"), TanColor);
        else
            _window.SetStatusRow(_window.BoxAlertPanel, _window.BoxStatusLabel, Loc.GetString("cmu-turing-dispenser-box-missing"), RedColor);

        if (_itemSlots.GetItemOrNull(Owner, comp.OutputBeakerSlotId) is { } beaker &&
            _solution.TryGetMixableSolution(beaker, out _, out var solution))
        {
            _window.SetStatusRow(_window.BeakerAlertPanel, _window.BeakerStatusLabel, Loc.GetString("cmu-turing-dispenser-beaker-volume", ("volume", solution.Volume), ("max", solution.MaxVolume)), TanColor);
        }
        else
        {
            _window.SetStatusRow(_window.BeakerAlertPanel, _window.BeakerStatusLabel, Loc.GetString("cmu-turing-dispenser-beaker-missing"), RedColor);
        }

        _window.SetAutoRun(comp.AutoRun);
        _window.SetSmartLink(comp.SmartLink);
        // CMU14: fluent output mode
        _window.SetOutputMode(comp.OutputMode switch
        {
            TuringDispenserOutputMode.SmartFridge => Loc.GetString("cmu-turing-dispenser-output-smartfridge"),
            TuringDispenserOutputMode.Centrifuge => Loc.GetString("cmu-turing-dispenser-output-centrifuge"),
            _ => Loc.GetString("cmu-turing-dispenser-output-container"),
        });

        _window.SetSpinBoxValues(comp.Multiplier.Float(), comp.CycleLimit);
    }

    private void UpdateBeakerRows(Entity<RMCTuringDispenserComponent> ent)
    {
        var comp = ent.Comp;
        var coords = EntMan.GetComponent<TransformComponent>(Owner).Coordinates;
        var hasFridge = _turing.TryGetSmartFridge(ent, out var fridge);

        foreach (var row in _beakerRows)
        {
            if (row.Id is { } id)
            {
                var vendorStocked = comp.BeakerOptions.FirstOrDefault(o => o.Id == id).VendorStocked;

                if (!vendorStocked)
                {
                    row.StockLabel.Text = "∞";
                }
                else
                {
                    var stock = (hasFridge ? _smartFridge.GetEmptyContainerCount(fridge, id) : 0) +
                                _cmVendor.GetStockedAmount(coords, comp.SmartFridgeRange, id);
                    row.StockLabel.Text = Loc.GetString("cmu-turing-dispenser-stock", ("count", stock)); // CMU14: fluent
                }
            }
            else
            {
                row.StockLabel.Text = string.Empty;
            }

            RMCTuringDispenserWindow.SetBeakerRowSelected(row, row.Id == comp.PreferredBeaker);
        }
    }

    private void UpdateProgramRows(
        RMCTuringDispenserComponent comp,
        BoxContainer container,
        Control emptyPanel,
        Label emptyLabel,
        List<TuringProgramEntry> program)
    {
        emptyPanel.Visible = program.Count == 0;
        if (program.Count == 0)
            _window!.SetStatusRow(emptyPanel, emptyLabel, emptyLabel.Text ?? string.Empty, RedColor);

        var vialVolume = comp.BeakerOptions.FirstOrDefault(o => o.Id == comp.VialPrototype).MaxVolume.Float();

        for (var i = 0; i < program.Count; i++)
        {
            var entry = program[i];
            RMCTuringDispenserProgramRow row;
            if (i < container.ChildCount)
            {
                row = (RMCTuringDispenserProgramRow) container.GetChild(i);
            }
            else
            {
                row = new RMCTuringDispenserProgramRow();
                container.AddChild(row);
            }

            var name = entry.Reagent.Id;
            var fillColor = Color.White;
            if (_rmcReagent.TryIndex(entry.Reagent, out var reagent))
            {
                name = reagent.LocalizedName;
                fillColor = reagent.SubstanceColor;
            }

            row.ReagentLabel.Text = name;
            row.AmountLabel.Text = $"{entry.Amount}u";
            row.VialFillIcon.ModulateSelfOverride = fillColor;
            row.SetFillFraction(vialVolume > 0 ? entry.Amount.Float() / vialVolume : 1f);
        }

        container.RemoveChildrenAfter(program.Count);
    }
}
