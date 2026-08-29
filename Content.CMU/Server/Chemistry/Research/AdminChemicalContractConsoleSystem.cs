using Content.Server.CMU14.Chemistry.Reagents;
using Content.Server.Administration.Managers;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.CMU14.Chemistry.Research;
using Content.Shared.CMU14.Chemistry.Reagent;
using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server.CMU14.Chemistry.Research;

/// <summary>
/// Provides an admin test workflow for creating a generated reagent contract from an exact property
/// profile, then manufacturing that reagent by using the printed contract on the console.
/// </summary>
public sealed class AdminChemicalContractConsoleSystem : EntitySystem
{
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private ServerReagentGeneratorSystem _generator = default!;
    [Dependency] private ServerResearchDataTerminalSystem _research = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdminChemicalContractConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<AdminChemicalContractConsoleComponent, InteractUsingEvent>(OnInteractUsing);

        Subs.BuiEvents<AdminChemicalContractConsoleComponent>(AdminChemicalContractConsoleUi.Key, subs =>
        {
            subs.Event<AdminChemicalContractSetPropertyBuiMsg>(OnSetProperty);
            subs.Event<AdminChemicalContractSetAllBuiMsg>(OnSetAll);
            subs.Event<AdminChemicalContractIssueBuiMsg>(OnIssueContract);
        });
    }

    private void OnUiOpened(Entity<AdminChemicalContractConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!IsAuthorized(args.Actor, ent.Owner))
        {
            _ui.CloseUi(ent.Owner, AdminChemicalContractConsoleUi.Key, args.Actor);
            return;
        }

        UpdateUi(ent);
    }

    private void OnSetProperty(
        Entity<AdminChemicalContractConsoleComponent> ent,
        ref AdminChemicalContractSetPropertyBuiMsg args)
    {
        if (!IsAuthorized(args.Actor, ent.Owner) ||
            !ent.Comp.AvailableProperties.Contains(args.Property) ||
            !_prototypes.TryIndex<ReagentPropertyPrototype>(args.Property, out var property))
        {
            return;
        }

        var level = Math.Clamp(args.Level, 0, property.MaxLevel);
        if (level == 0)
            ent.Comp.SelectedProperties.Remove(args.Property);
        else
            ent.Comp.SelectedProperties[args.Property] = level;

        ent.Comp.Status = string.Empty;
        UpdateUi(ent);
    }

    private void OnSetAll(
        Entity<AdminChemicalContractConsoleComponent> ent,
        ref AdminChemicalContractSetAllBuiMsg args)
    {
        if (!IsAuthorized(args.Actor, ent.Owner))
            return;

        ent.Comp.SelectedProperties.Clear();
        if (args.Level > 0)
        {
            foreach (var id in ent.Comp.AvailableProperties)
            {
                if (_prototypes.TryIndex(id, out var property))
                    ent.Comp.SelectedProperties[id] = Math.Clamp(args.Level, 1, property.MaxLevel);
            }
        }

        ent.Comp.Status = string.Empty;
        UpdateUi(ent);
    }

    private void OnIssueContract(
        Entity<AdminChemicalContractConsoleComponent> ent,
        ref AdminChemicalContractIssueBuiMsg args)
    {
        if (!IsAuthorized(args.Actor, ent.Owner))
            return;

        if (ent.Comp.SelectedProperties.Count == 0)
        {
            ent.Comp.Status = Loc.GetString("admin-chemical-contract-status-no-properties");
            _popup.PopupEntity(ent.Comp.Status, ent.Owner, args.Actor);
            UpdateUi(ent);
            return;
        }

        var data = new GeneratedReagentData
        {
            Class = ReagentClass.Ultra,
            GenTier = 3,
            ScanPointYield = 7,
        };

        _generator.GenerateStats(ref data, true);
        _generator.GenerateName(ref data);
        data.Effects = new Dictionary<string, int>(ent.Comp.SelectedProperties);
        data.PropertyHint = data.Effects.Keys.First();

        if (_generator.ChemicalGenClassesList.TryGetValue("C2", out var recipeCandidates) &&
            recipeCandidates.Count > 0)
        {
            data.RecipeHint = _random.Pick(recipeCandidates);
        }

        var contract = _research.IssueAdminContract(ent.Owner, data);
        if (contract is null)
        {
            ent.Comp.Status = Loc.GetString("admin-chemical-contract-status-failed");
            _popup.PopupEntity(ent.Comp.Status, ent.Owner, args.Actor);
            UpdateUi(ent);
            return;
        }

        ent.Comp.Status = Loc.GetString("admin-chemical-contract-status-issued", ("name", data.Name));
        _popup.PopupEntity(ent.Comp.Status, ent.Owner, args.Actor);
        _adminLog.Add(
            LogType.Action,
            LogImpact.High,
            $"{ToPrettyString(args.Actor):admin} issued admin chemical contract {data.ID} " +
            $"with properties {string.Join(", ", data.Effects.Select(property => $"{property.Key} {property.Value}"))}");
        UpdateUi(ent);
    }

    private void OnInteractUsing(Entity<AdminChemicalContractConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (!HasComp<AdminChemicalContractPaperComponent>(args.Used))
            return;

        args.Handled = true;
        if (!IsAuthorized(args.User, ent.Owner))
            return;

        if (!TryMaterializeContract(ent, args.Used, out _, out var registeredData))
        {
            _popup.PopupEntity(Loc.GetString("admin-chemical-contract-invalid"), ent.Owner, args.User);
            return;
        }

        ent.Comp.Status = Loc.GetString(
            "admin-chemical-contract-status-materialized",
            ("amount", ent.Comp.OutputAmount),
            ("name", registeredData.Name));
        _popup.PopupEntity(ent.Comp.Status, ent.Owner, args.User);
        _adminLog.Add(
            LogType.Action,
            LogImpact.High,
            $"{ToPrettyString(args.User):admin} materialized {ent.Comp.OutputAmount}u of admin chemical " +
            $"{registeredData.ID} using {ToPrettyString(args.Used):contract}");
        UpdateUi(ent);
    }

    /// <summary>
    /// Materializes a valid contract. The public entry point exists so the generated-reagent and solution
    /// integration can be tested without weakening the admin authorization on the interaction handler.
    /// </summary>
    public bool TryMaterializeContract(
        Entity<AdminChemicalContractConsoleComponent> ent,
        EntityUid contract,
        out EntityUid? vial,
        out GeneratedReagentData registeredData)
    {
        vial = null;
        registeredData = default;
        if (!HasComp<AdminChemicalContractPaperComponent>(contract) ||
            !TryComp<ResearchReportComponent>(contract, out var report) ||
            report.Data is not { } reportData ||
            !report.Valid ||
            !report.Completed ||
            !_generator.ProceduralReagentData.TryGetValue(reportData.ID, out registeredData) ||
            !_prototypes.HasIndex<ReagentPrototype>(reportData.ID))
        {
            return false;
        }

        var spawnedVial = SpawnNextToOrDrop("RMCVial", ent.Owner);
        if (!_solutions.TryGetSolution(spawnedVial, "beaker", out var solution) ||
            !_solutions.TryAddReagent(solution.Value, registeredData.ID, ent.Comp.OutputAmount))
        {
            QueueDel(spawnedVial);
            return false;
        }

        _metaData.SetEntityName(
            spawnedVial,
            Loc.GetString("admin-chemical-contract-vial-name", ("name", registeredData.Name)));
        DirtyEntity(spawnedVial);
        vial = spawnedVial;
        return true;
    }

    private bool IsAuthorized(EntityUid user, EntityUid console)
    {
        if (_admins.IsAdmin(user))
            return true;

        _popup.PopupEntity(Loc.GetString("admin-chemical-contract-admin-only"), console, user);
        return false;
    }

    private void UpdateUi(Entity<AdminChemicalContractConsoleComponent> ent)
    {
        var properties = new List<AdminChemicalContractPropertyState>();
        foreach (var id in ent.Comp.AvailableProperties)
        {
            if (!_prototypes.TryIndex(id, out var property))
                continue;

            ent.Comp.SelectedProperties.TryGetValue(id, out var selectedLevel);
            properties.Add(new AdminChemicalContractPropertyState(
                property.ID,
                property.LocalizedName,
                property.LocalizedDescription,
                property.MaxLevel,
                selectedLevel));
        }

        _ui.SetUiState(
            ent.Owner,
            AdminChemicalContractConsoleUi.Key,
            new AdminChemicalContractConsoleBuiState(properties, ent.Comp.Status));
    }
}
