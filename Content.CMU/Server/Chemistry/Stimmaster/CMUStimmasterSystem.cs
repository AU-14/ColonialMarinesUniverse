using Content.Server.Materials;
using Content.Shared.CMU14.Chemistry.Stimmaster;
using Content.Shared._RMC14.Chemistry.ChemMaster;
using Content.Shared._RMC14.Chemistry.SmartFridge;
using Content.Shared._RMC14.IconLabel;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Coordinates;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Chemistry.Stimmaster;

/// <summary>
/// Fabricates empty autoinjector shells and fills selected shells from an RMC ChemMaster buffer.
/// </summary>
public sealed partial class CMUStimmasterSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private LabelSystem _label = default!;
    [Dependency] private MaterialStorageSystem _materials = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedRMCIconLabelSystem _rmcIconLabel = default!;
    [Dependency] private SharedRMCSmartFridgeSystem _smartFridge = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    private const string BufferSolution = "buffer";
    private const string InjectorSolution = "pen";
    private const string Steel = "CMSteel";
    private const string Glass = "CMGlass";

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUStimmasterComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<CMUStimmasterComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
        SubscribeLocalEvent<CMUStimmasterComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);

        Subs.BuiEvents<CMUStimmasterComponent>(RMCChemMasterUI.Key, subs =>
        {
            subs.Event<CMUStimmasterCreateMsg>(OnCreate);
            subs.Event<CMUStimmasterSelectInjectorMsg>(OnSelectInjector);
            subs.Event<CMUStimmasterSelectAllMsg>(OnSelectAll);
            subs.Event<CMUStimmasterFillMsg>(OnFill);
            subs.Event<CMUStimmasterLabelMsg>(OnLabel);
            subs.Event<CMUStimmasterTransferMsg>(OnTransfer);
            subs.Event<CMUStimmasterEjectMsg>(OnEject);
        });
    }

    private void OnEntInserted(Entity<CMUStimmasterComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.InjectorContainer)
            Dirty(ent);
    }

    private void OnEntRemoved(Entity<CMUStimmasterComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.InjectorContainer)
            return;

        ent.Comp.SelectedInjectors.Remove(args.Entity);
        Dirty(ent);
    }

    private void OnGetAlternativeVerbs(Entity<CMUStimmasterComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess ||
            !args.CanInteract ||
            !TryComp(ent, out MaterialStorageComponent? storage) ||
            !storage.CanEjectStoredMaterials)
        {
            return;
        }

        foreach (var (materialId, amount) in _materials.GetStoredMaterials((ent.Owner, storage)))
        {
            if (!_prototype.TryIndex<MaterialPrototype>(materialId, out var material))
                continue;

            var sheetVolume = _materials.GetSheetVolume(material);
            var sheets = amount / sheetVolume;
            if (sheets <= 0)
                continue;

            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cmu-stimmaster-eject-material",
                    ("material", Loc.GetString(material.Name)),
                    ("sheets", sheets)),
                Category = VerbCategory.Eject,
                Icon = material.Icon,
                Act = () => EjectMaterial(ent, material.ID),
            });
        }
    }

    private void EjectMaterial(Entity<CMUStimmasterComponent> ent, string material)
    {
        _materials.EjectMaterial(ent, material);
        Dirty(ent);
        DirtyChemMaster(ent.Owner);
    }

    private void OnCreate(Entity<CMUStimmasterComponent> ent, ref CMUStimmasterCreateMsg args)
    {
        if (args.Amount < 1 ||
            args.Amount > ent.Comp.MaxFabricationAmount ||
            !ent.Comp.InjectorPrototypes.Contains(args.Prototype) ||
            !_prototype.HasIndex<EntityPrototype>(args.Prototype.Id))
        {
            return;
        }

        var container = _container.EnsureContainer<Container>(ent, ent.Comp.InjectorContainer);
        if (container.Count + args.Amount > ent.Comp.MaxStoredInjectors)
        {
            _popup.PopupClient(Loc.GetString("cmu-stimmaster-storage-full"), ent, args.Actor, PopupType.SmallCaution);
            return;
        }

        if (!TryComp(ent, out MaterialStorageComponent? storage))
            return;

        var materials = new Dictionary<string, int>
        {
            [Steel] = -ent.Comp.MetalCost * args.Amount,
            [Glass] = -ent.Comp.GlassCost * args.Amount,
        };
        if (!_materials.CanChangeMaterialAmount((ent.Owner, storage), materials) ||
            !_materials.TryChangeMaterialAmount((ent.Owner, storage), materials))
        {
            _popup.PopupClient(Loc.GetString("cmu-stimmaster-not-enough-materials"), ent, args.Actor, PopupType.SmallCaution);
            return;
        }

        var coordinates = Transform(ent).Coordinates;
        for (var i = 0; i < args.Amount; i++)
        {
            var injector = Spawn(args.Prototype, coordinates);
            if (!_container.Insert(injector, container))
                QueueDel(injector);
        }

        Dirty(ent);
        DirtyChemMaster(ent.Owner);
    }

    private void OnSelectInjector(Entity<CMUStimmasterComponent> ent, ref CMUStimmasterSelectInjectorMsg args)
    {
        if (!TryGetContainedInjector(ent, args.Injector, out var injector))
            return;

        if (args.Fill)
            ent.Comp.SelectedInjectors.Add(injector);
        else
            ent.Comp.SelectedInjectors.Remove(injector);

        Dirty(ent);
        DirtyChemMaster(ent.Owner);
    }

    private void OnSelectAll(Entity<CMUStimmasterComponent> ent, ref CMUStimmasterSelectAllMsg args)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.InjectorContainer, out var container))
            return;

        if (args.SelectAll)
            ent.Comp.SelectedInjectors.UnionWith(container.ContainedEntities);
        else
            ent.Comp.SelectedInjectors.Clear();

        Dirty(ent);
        DirtyChemMaster(ent.Owner);
    }

    private void OnFill(Entity<CMUStimmasterComponent> ent, ref CMUStimmasterFillMsg args)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.InjectorContainer, out var container) ||
            !_solution.TryGetSolution(ent.Owner, BufferSolution, out var buffer) ||
            buffer.Value.Comp.Solution.Volume <= FixedPoint2.Zero)
        {
            _popup.PopupClient(Loc.GetString("cmu-stimmaster-no-selection-or-chemicals"), ent, args.Actor, PopupType.SmallCaution);
            return;
        }

        var injectors = new List<Entity<SolutionComponent>>();
        foreach (var injector in container.ContainedEntities)
        {
            if (!ent.Comp.SelectedInjectors.Contains(injector) ||
                !_solution.TryGetSolution(injector, InjectorSolution, out var injectorSolution) ||
                injectorSolution.Value.Comp.Solution.AvailableVolume <= FixedPoint2.Zero)
            {
                continue;
            }

            injectors.Add(injectorSolution.Value);
        }

        if (injectors.Count == 0)
        {
            _popup.PopupClient(Loc.GetString("cmu-stimmaster-no-selection-or-chemicals"), ent, args.Actor, PopupType.SmallCaution);
            return;
        }

        var insufficientChemicals = false;
        foreach (var injectorSolution in injectors)
        {
            var required = injectorSolution.Comp.Solution.AvailableVolume;
            if (buffer.Value.Comp.Solution.Volume < required)
            {
                insufficientChemicals = true;
                break;
            }

            if (!_solution.TryTransferSolution(injectorSolution, buffer.Value.Comp.Solution, required))
                break;
        }

        if (insufficientChemicals)
        {
            _popup.PopupClient(
                Loc.GetString("cmu-stimmaster-not-enough-chemicals-for-injector"),
                ent,
                args.Actor,
                PopupType.SmallCaution);
        }

        _solution.UpdateChemicals(buffer.Value);
        Dirty(ent);
        DirtyChemMaster(ent.Owner);
    }

    private void OnLabel(Entity<CMUStimmasterComponent> ent, ref CMUStimmasterLabelMsg args)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.InjectorContainer, out var container))
            return;

        var maxLabelLength = TryComp(ent, out RMCChemMasterComponent? chemMaster)
            ? chemMaster.MaxLabelLength
            : 64;
        var label = args.Label.Length > maxLabelLength
            ? args.Label[..maxLabelLength]
            : args.Label;
        foreach (var injector in ent.Comp.SelectedInjectors)
        {
            if (!container.Contains(injector))
                continue;

            _label.Label(injector, label);
            SetInjectorIconLabel(injector, label);
        }

        Dirty(ent);
        DirtyChemMaster(ent.Owner);
    }

    private void SetInjectorIconLabel(EntityUid injector, string label)
    {
        var iconLabel = EnsureComp<IconLabelComponent>(injector);
        var iconText = label.Trim();
        if (iconText.Length > iconLabel.LabelMaxSize)
            iconText = iconText[..iconLabel.LabelMaxSize];

        if (string.IsNullOrWhiteSpace(iconText))
        {
            iconLabel.LabelTextLocId = null;
            iconLabel.LabelTextParams.Clear();
            Dirty(injector, iconLabel);
            return;
        }

        _rmcIconLabel.Label(
            injector,
            "rmc-custom-container-label-text",
            ("customLabel", iconText));
    }

    private void OnTransfer(Entity<CMUStimmasterComponent> ent, ref CMUStimmasterTransferMsg args)
    {
        if (!TryGetContainedInjector(ent, args.Injector, out var injector))
            return;

        var range = TryComp(ent, out RMCChemMasterComponent? chemMaster)
            ? chemMaster.LinkRange
            : 5f;
        _smartFridge.TransferToNearby(ent.Owner.ToCoordinates(), range, injector);
        Dirty(ent);
        DirtyChemMaster(ent.Owner);
    }

    private void OnEject(Entity<CMUStimmasterComponent> ent, ref CMUStimmasterEjectMsg args)
    {
        if (!TryGetContainedInjector(ent, args.Injector, out var injector) ||
            !_container.TryGetContainingContainer((injector, null), out var container) ||
            !_container.Remove(injector, container))
        {
            return;
        }

        _hands.TryPickupAnyHand(args.Actor, injector);
        if (TryComp(ent, out RMCChemMasterComponent? chemMaster))
            _audio.PlayPredicted(chemMaster.PillBottleEjectSound, ent, args.Actor);

        Dirty(ent);
        DirtyChemMaster(ent.Owner);
    }

    private bool TryGetContainedInjector(
        Entity<CMUStimmasterComponent> ent,
        NetEntity netInjector,
        out EntityUid injector)
    {
        injector = default;
        if (!TryGetEntity(netInjector, out var candidate) ||
            !_container.TryGetContainer(ent, ent.Comp.InjectorContainer, out var container) ||
            !container.Contains(candidate.Value))
        {
            return false;
        }

        injector = candidate.Value;
        return true;
    }

    private void DirtyChemMaster(EntityUid owner)
    {
        if (TryComp(owner, out RMCChemMasterComponent? chemMaster))
            Dirty(owner, chemMaster);
    }
}
