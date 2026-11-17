using System.Globalization;
using System.Linq;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Chemistry.ChemMaster;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.CMU14.Chemistry;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Chemistry;

public sealed partial class CMUChemicalRecipeMachineSystem : EntitySystem
{
    private const string RecipeHeader = "CMU14-CHEM-RECIPE/1";
    private const string PillSolutionId = "food";
    private const int MaxRecipeEntries = 64;
    private const int MaxPaperContentSize = 10000;
    private static readonly FixedPoint2 MaxRecipeAmount = FixedPoint2.New(1000);
    private static readonly EntProtoId PaperPrototype = "CMUChemRecipePaper";

    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly RMCReagentSystem _reagents = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCChemMasterComponent, RMCChemMasterPillScanEvent>(OnPillScan);
        SubscribeLocalEvent<CMUChemicalRecipeMachineComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<CMUChemicalRecipeMachineComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<CMUChemicalRecipeMachineComponent, BoundUIOpenedEvent>(OnUiOpened);

        Subs.BuiEvents<CMUChemicalRecipeMachineComponent>(CMUChemicalRecipeMachineUiKey.Key, subs =>
        {
            subs.Event<CMUChemicalRecipeMachineLoadMessage>(OnLoadIngredients);
            subs.Event<CMUChemicalRecipeMachineSynthesizeMessage>(OnSynthesize);
            subs.Event<CMUChemicalRecipeMachineEjectMessage>(OnEject);
        });
    }

    private void OnPillScan(Entity<RMCChemMasterComponent> ent, ref RMCChemMasterPillScanEvent args)
    {
        if (args.Handled || !HasComp<PillComponent>(args.Pill))
            return;

        args.Handled = true;
        if (!_solutions.TryGetSolution(args.Pill, PillSolutionId, out _, out var solution) ||
            solution.Contents.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-chem-recipe-paper-invalid-pill"), ent, args.User);
            return;
        }

        var text = CreateRecipePaper(Name(args.Pill), solution);
        if (text.Length > MaxPaperContentSize)
        {
            _popup.PopupEntity(Loc.GetString("cmu-chem-recipe-paper-too-long"), ent, args.User);
            return;
        }

        var paper = Spawn(PaperPrototype, Transform(ent).Coordinates);
        if (!TryComp(paper, out PaperComponent? paperComponent))
        {
            QueueDel(paper);
            _popup.PopupEntity(Loc.GetString("cmu-chem-recipe-paper-failed"), ent, args.User);
            return;
        }

        _paper.SetContent((paper, paperComponent), text);
        _hands.TryPickup(args.User, paper);
    }

    private string CreateRecipePaper(string pillName, Solution solution)
    {
        var outputs = new Dictionary<string, FixedPoint2>();
        var consumed = new Dictionary<string, FixedPoint2>();
        var catalysts = new Dictionary<string, FixedPoint2>();
        var steps = new List<string>();
        var exceededLimits = false;

        foreach (var reagent in solution.Contents.OrderBy(content => content.Reagent.Prototype.ToString(), StringComparer.Ordinal))
        {
            AddSynthesisRequirements(
                reagent.Reagent.Prototype,
                reagent.Quantity,
                new HashSet<string>(StringComparer.Ordinal),
                consumed,
                catalysts,
                steps,
                ref exceededLimits);
            outputs[reagent.Reagent.Prototype] = reagent.Quantity;
        }

        if (exceededLimits ||
            consumed.Count + catalysts.Count + outputs.Count > MaxRecipeEntries ||
            InputTotalExceedsLimit(consumed, catalysts))
        {
            consumed.Clear();
            catalysts.Clear();
            steps.Clear();
            foreach (var (id, amount) in outputs)
            {
                TryAddAmount(consumed, id, amount);
                AddRawMaterialStep(id, steps);
            }
        }

        var text = new StringBuilder();
        text.AppendLine(RecipeHeader);
        AppendCodeSection(text, "OUTPUT", outputs);
        AppendCodeSection(text, "CONSUME", consumed);
        AppendCodeSection(text, "CATALYST", catalysts);
        text.AppendLine("---");
        text.AppendLine(Loc.GetString("cmu-chem-recipe-paper-title", ("pill", pillName)));
        text.AppendLine(Loc.GetString("cmu-chem-recipe-paper-dose"));

        foreach (var output in outputs.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!TryGetReagent(output.Key, out var reagent))
                continue;

            text.AppendLine(Loc.GetString("cmu-chem-recipe-paper-reagent",
                ("amount", FormatAmount(output.Value)),
                ("name", reagent.LocalizedName)));
        }

        text.AppendLine();
        text.AppendLine(Loc.GetString("cmu-chem-recipe-paper-inputs"));
        foreach (var input in consumed.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!TryGetReagent(input.Key, out var reagent))
                continue;

            text.AppendLine(Loc.GetString("cmu-chem-recipe-paper-reagent",
                ("amount", FormatAmount(input.Value)),
                ("name", reagent.LocalizedName)));
        }

        foreach (var catalyst in catalysts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!TryGetReagent(catalyst.Key, out var reagent))
                continue;

            text.AppendLine(Loc.GetString("cmu-chem-recipe-paper-catalyst",
                ("amount", FormatAmount(catalyst.Value)),
                ("name", reagent.LocalizedName)));
        }

        text.AppendLine();
        text.AppendLine(Loc.GetString("cmu-chem-recipe-paper-steps"));
        for (var i = 0; i < steps.Count; i++)
        {
            text.AppendLine(Loc.GetString("cmu-chem-recipe-paper-step",
                ("number", i + 1),
                ("step", steps[i])));
        }

        text.AppendLine();
        text.AppendLine(Loc.GetString("cmu-chem-recipe-paper-packaging"));
        return text.ToString();
    }

    private void AppendCodeSection(StringBuilder text, string type, Dictionary<string, FixedPoint2> amounts)
    {
        foreach (var (id, amount) in amounts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            text.Append(type);
            text.Append('|');
            text.Append(id);
            text.Append('|');
            text.AppendLine(FormatAmount(amount));
        }
    }

    private void AddSynthesisRequirements(
        string reagentId,
        FixedPoint2 amount,
        HashSet<string> path,
        Dictionary<string, FixedPoint2> consumed,
        Dictionary<string, FixedPoint2> catalysts,
        List<string> steps,
        ref bool exceededLimits)
    {
        if (amount <= FixedPoint2.Zero)
            return;

        if (amount > MaxRecipeAmount)
        {
            exceededLimits = true;
            amount = MaxRecipeAmount;
        }

        if (path.Count >= 16 || !path.Add(reagentId))
        {
            if (!TryAddAmount(consumed, reagentId, amount))
                exceededLimits = true;
            AddRawMaterialStep(reagentId, steps);
            return;
        }

        var reaction = _prototypes.EnumeratePrototypes<ReactionPrototype>()
            .Where(candidate => !candidate.Source &&
                                candidate.Effects.Length == 0 &&
                                candidate.Reactants.Count > 0 &&
                                candidate.Products.Any(product => product.Key.ToString() == reagentId && product.Value > FixedPoint2.Zero))
            .OrderBy(candidate => candidate.ID, StringComparer.Ordinal)
            .FirstOrDefault();

        if (reaction == null)
        {
            if (!TryAddAmount(consumed, reagentId, amount))
                exceededLimits = true;
            AddRawMaterialStep(reagentId, steps);
            path.Remove(reagentId);
            return;
        }

        var productAmount = reaction.Products.First(product => product.Key.ToString() == reagentId).Value;
        var reactionUnits = amount.Double() / productAmount.Double();
        if (!double.IsFinite(reactionUnits) || reactionUnits > MaxRecipeAmount.Double())
        {
            exceededLimits = true;
            if (!TryAddAmount(consumed, reagentId, amount))
                exceededLimits = true;
            AddRawMaterialStep(reagentId, steps);
            path.Remove(reagentId);
            return;
        }

        if (reaction.Quantized)
            reactionUnits = Math.Max(1, Math.Ceiling(reactionUnits));
        else
            reactionUnits = Math.Max(FixedPoint2.Epsilon.Double(), reactionUnits);

        var reactionsNeeded = FixedPoint2.NewCeiling((float) reactionUnits);

        foreach (var reactant in reaction.Reactants)
        {
            var reactantId = reactant.Key.ToString();
            if (reactant.Value.Catalyst)
            {
                AddMaximum(catalysts, reactantId, reactant.Value.Amount, ref exceededLimits);
                continue;
            }

            var requiredAmount = reactant.Value.Amount.Double() * reactionsNeeded.Double();
            if (!double.IsFinite(requiredAmount) || requiredAmount > MaxRecipeAmount.Double())
            {
                exceededLimits = true;
                requiredAmount = MaxRecipeAmount.Double();
            }

            AddSynthesisRequirements(
                reactantId,
                FixedPoint2.New(requiredAmount),
                path,
                consumed,
                catalysts,
                steps,
                ref exceededLimits);
        }

        var reactionName = string.IsNullOrWhiteSpace(reaction.Name)
            ? reaction.ID
            : Loc.GetString(reaction.Name);
        var productName = TryGetReagent(reagentId, out var productReagent)
            ? productReagent.LocalizedName
            : reagentId;
        var conditions = new List<string>();
        if (reaction.MinimumTemperature > 0)
            conditions.Add(Loc.GetString("cmu-chem-recipe-paper-min-temperature", ("temperature", reaction.MinimumTemperature)));
        if (float.IsFinite(reaction.MaximumTemperature))
            conditions.Add(Loc.GetString("cmu-chem-recipe-paper-max-temperature", ("temperature", reaction.MaximumTemperature)));
        if (reaction.MixingCategories is { Count: > 0 })
            conditions.Add(Loc.GetString("cmu-chem-recipe-paper-mixing",
                ("categories", string.Join(", ", reaction.MixingCategories))));

        steps.Add(conditions.Count == 0
            ? Loc.GetString("cmu-chem-recipe-paper-mix", ("reaction", reactionName), ("amount", FormatAmount(amount)), ("name", productName))
            : Loc.GetString("cmu-chem-recipe-paper-mix-conditions",
                ("reaction", reactionName),
                ("amount", FormatAmount(amount)),
                ("name", productName),
                ("conditions", string.Join("; ", conditions))));
        path.Remove(reagentId);
    }

    private void AddRawMaterialStep(string reagentId, List<string> steps)
    {
        var name = TryGetReagent(reagentId, out var reagent)
            ? reagent.LocalizedName
            : reagentId;
        steps.Add(Loc.GetString("cmu-chem-recipe-paper-no-safe-reaction", ("name", name)));
    }

    private static bool TryAddAmount(Dictionary<string, FixedPoint2> amounts, string id, FixedPoint2 amount)
    {
        var total = (long) amounts.GetValueOrDefault(id).Value + amount.Value;
        if (total > MaxRecipeAmount.Value)
            return false;

        amounts[id] = FixedPoint2.FromCents((int) total);
        return true;
    }

    private static void AddMaximum(Dictionary<string, FixedPoint2> amounts, string id, FixedPoint2 amount, ref bool exceededLimits)
    {
        if (amount > MaxRecipeAmount)
        {
            exceededLimits = true;
            amount = MaxRecipeAmount;
        }

        if (!amounts.TryGetValue(id, out var current) || amount > current)
            amounts[id] = amount;
    }

    private void OnContainerChanged(Entity<CMUChemicalRecipeMachineComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateUi(ent);
    }

    private void OnContainerChanged(Entity<CMUChemicalRecipeMachineComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateUi(ent);
    }

    private void OnUiOpened(Entity<CMUChemicalRecipeMachineComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnLoadIngredients(Entity<CMUChemicalRecipeMachineComponent> ent, ref CMUChemicalRecipeMachineLoadMessage args)
    {
        if (!TryGetRecipe(ent, out var recipe) ||
            _itemSlots.GetItemOrNull(ent.Owner, CMUChemicalRecipeMachineComponent.VesselSlotId) is not { } vessel ||
            !_solutions.TryGetFitsInDispenser(vessel, out var sourceSolution, out var source))
        {
            _popup.PopupEntity(Loc.GetString("cmu-chem-recipe-machine-load-invalid"), ent, args.Actor);
            UpdateUi(ent);
            return;
        }

        if (!_solutions.TryGetSolution(ent.Owner, CMUChemicalRecipeMachineComponent.StockSolutionId, out var stock))
        {
            _popup.PopupEntity(Loc.GetString("cmu-chem-recipe-machine-storage-unavailable"), ent, args.Actor);
            UpdateUi(ent);
            return;
        }

        var required = GetInputs(recipe);
        var transfers = new Dictionary<string, FixedPoint2>();
        var totalTransfer = FixedPoint2.Zero;
        foreach (var (id, amount) in required)
        {
            var stored = stock.Value.Comp.Solution.GetTotalPrototypeQuantity(id);
            var remaining = amount - stored;
            if (remaining <= FixedPoint2.Zero)
                continue;

            var available = source.GetTotalPrototypeQuantity(id);
            var transfer = FixedPoint2.Min(available, remaining);
            if (transfer <= FixedPoint2.Zero)
                continue;

            transfers[id] = transfer;
            totalTransfer += transfer;
        }

        if (totalTransfer <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("cmu-chem-recipe-machine-no-matching-ingredients"), ent, args.Actor);
            UpdateUi(ent);
            return;
        }

        if (totalTransfer > stock.Value.Comp.Solution.AvailableVolume)
        {
            _popup.PopupEntity(Loc.GetString("cmu-chem-recipe-machine-storage-full"), ent, args.Actor);
            UpdateUi(ent);
            return;
        }

        foreach (var (id, amount) in transfers)
        {
            var removed = _solutions.RemoveReagent(sourceSolution.Value, id, amount);
            if (removed != amount ||
                !_solutions.TryAddReagent(stock.Value, id, removed, out var accepted, source.Temperature) ||
                accepted != removed)
            {
                _popup.PopupEntity(Loc.GetString("cmu-chem-recipe-machine-transfer-failed"), ent, args.Actor);
                UpdateUi(ent);
                return;
            }
        }

        UpdateUi(ent);
    }

    private void OnSynthesize(Entity<CMUChemicalRecipeMachineComponent> ent, ref CMUChemicalRecipeMachineSynthesizeMessage args)
    {
        if (!TryGetRecipe(ent, out var recipe) ||
            _itemSlots.GetItemOrNull(ent.Owner, CMUChemicalRecipeMachineComponent.VesselSlotId) is not { } vessel ||
            !_solutions.TryGetFitsInDispenser(vessel, out var outputSolution, out var output))
        {
            _popup.PopupEntity(Loc.GetString("cmu-chem-recipe-machine-synthesis-invalid"), ent, args.Actor);
            UpdateUi(ent);
            return;
        }

        if (output.Volume > FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("cmu-chem-recipe-machine-output-not-empty"), ent, args.Actor);
            UpdateUi(ent);
            return;
        }

        var outputVolume = Sum(recipe.Outputs.Values);
        if (outputVolume <= FixedPoint2.Zero || outputVolume > output.AvailableVolume)
        {
            _popup.PopupEntity(Loc.GetString("cmu-chem-recipe-machine-output-too-small"), ent, args.Actor);
            UpdateUi(ent);
            return;
        }

        if (!_solutions.TryGetSolution(ent.Owner, CMUChemicalRecipeMachineComponent.StockSolutionId, out var stock))
        {
            _popup.PopupEntity(Loc.GetString("cmu-chem-recipe-machine-storage-unavailable"), ent, args.Actor);
            UpdateUi(ent);
            return;
        }

        foreach (var (id, amount) in GetInputs(recipe))
        {
            if (stock.Value.Comp.Solution.GetTotalPrototypeQuantity(id) < amount)
            {
                _popup.PopupEntity(Loc.GetString("cmu-chem-recipe-machine-ingredients-missing"), ent, args.Actor);
                UpdateUi(ent);
                return;
            }
        }

        if (_net.IsClient)
            return;

        foreach (var (id, amount) in recipe.Outputs)
        {
            if (!_solutions.TryAddReagent(outputSolution.Value, id, amount))
            {
                _popup.PopupEntity(Loc.GetString("cmu-chem-recipe-machine-output-failed"), ent, args.Actor);
                UpdateUi(ent);
                return;
            }
        }

        foreach (var (id, amount) in recipe.ConsumedInputs)
            _solutions.RemoveReagent(stock.Value, id, amount);

        UpdateUi(ent);
    }

    private void OnEject(Entity<CMUChemicalRecipeMachineComponent> ent, ref CMUChemicalRecipeMachineEjectMessage args)
    {
        var slot = args.Recipe
            ? CMUChemicalRecipeMachineComponent.RecipeSlotId
            : CMUChemicalRecipeMachineComponent.VesselSlotId;
        if (_itemSlots.TryGetSlot(ent.Owner, slot, out var itemSlot))
            _itemSlots.TryEjectToHands(ent.Owner, itemSlot, args.Actor);
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<CMUChemicalRecipeMachineComponent> ent)
    {
        var recipeItem = _itemSlots.GetItemOrNull(ent.Owner, CMUChemicalRecipeMachineComponent.RecipeSlotId);
        var vessel = _itemSlots.GetItemOrNull(ent.Owner, CMUChemicalRecipeMachineComponent.VesselSlotId);
        RecipeData? recipe = null;
        string status;

        if (recipeItem == null)
        {
            status = Loc.GetString("cmu-chem-recipe-machine-no-paper");
        }
        else if (TryGetRecipe(ent, out var loadedRecipe))
        {
            recipe = loadedRecipe;
            status = Loc.GetString("cmu-chem-recipe-machine-ready");
        }
        else
        {
            status = Loc.GetString("cmu-chem-recipe-machine-invalid-paper");
        }

        var inputs = new List<string>();
        var outputs = new List<string>();
        var canLoad = false;
        var canSynthesize = false;

        if (recipe != null)
        {
            _solutions.TryGetSolution(ent.Owner, CMUChemicalRecipeMachineComponent.StockSolutionId, out var stock);
            foreach (var (id, amount) in GetInputs(recipe).OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var stored = stock?.Comp.Solution.GetTotalPrototypeQuantity(id) ?? FixedPoint2.Zero;
                var displayName = TryGetReagent(id, out var reagent)
                    ? reagent.LocalizedName
                    : id;
                inputs.Add(Loc.GetString("cmu-chem-recipe-machine-ingredient",
                    ("name", displayName),
                    ("required", FormatAmount(amount)),
                    ("stored", FormatAmount(stored))));
                if (stored < amount)
                    canLoad = true;
            }

            foreach (var (id, amount) in recipe.Outputs.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var displayName = TryGetReagent(id, out var reagent)
                    ? reagent.LocalizedName
                    : id;
                outputs.Add(Loc.GetString("cmu-chem-recipe-machine-product",
                    ("name", displayName),
                    ("amount", FormatAmount(amount))));
            }

            if (vessel is { } outputItem &&
                _solutions.TryGetFitsInDispenser(outputItem, out _, out var output) &&
                output.Volume == FixedPoint2.Zero &&
                Sum(recipe.Outputs.Values) <= output.AvailableVolume &&
                _solutions.TryGetSolution(ent.Owner, CMUChemicalRecipeMachineComponent.StockSolutionId, out var stockSolution))
            {
                canSynthesize = GetInputs(recipe).All(input =>
                    stockSolution.Value.Comp.Solution.GetTotalPrototypeQuantity(input.Key) >= input.Value);
            }
        }

        if (recipe != null && vessel == null)
            status = Loc.GetString("cmu-chem-recipe-machine-insert-vessel");
        else if (recipe != null && !canSynthesize)
            status = canLoad
                ? Loc.GetString("cmu-chem-recipe-machine-load-prompt")
                : Loc.GetString("cmu-chem-recipe-machine-not-ready");

        _ui.SetUiState(ent.Owner, CMUChemicalRecipeMachineUiKey.Key,
            new CMUChemicalRecipeMachineUiState(
                status,
                recipe == null ? null : Loc.GetString("cmu-chem-recipe-machine-loaded"),
                inputs,
                outputs,
                canLoad && vessel != null,
                canSynthesize,
                recipeItem != null,
                vessel != null));
    }

    private bool TryGetRecipe(Entity<CMUChemicalRecipeMachineComponent> ent, out RecipeData recipe)
    {
        recipe = new RecipeData();
        var paper = _itemSlots.GetItemOrNull(ent.Owner, CMUChemicalRecipeMachineComponent.RecipeSlotId);
        if (paper == null ||
            !TryComp(paper.Value, out PaperComponent? paperComponent) ||
            !TryParseRecipe(paperComponent.Content, recipe))
        {
            recipe = new RecipeData();
            return false;
        }

        return true;
    }

    private bool TryParseRecipe(string content, RecipeData recipe)
    {
        var lines = content.Split('\n', StringSplitOptions.TrimEntries);
        if (lines.Length == 0 || lines[0] != RecipeHeader)
            return false;

        var entries = 0;
        var hasOutput = false;
        foreach (var line in lines.Skip(1))
        {
            if (line == "---")
                break;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != 3 ||
                parts[0] is not ("OUTPUT" or "CONSUME" or "CATALYST") ||
                !double.TryParse(parts[2], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) ||
                !double.IsFinite(amount) ||
                amount <= 0 ||
                amount > MaxRecipeAmount.Double() ||
                !TryGetReagent(parts[1], out _))
            {
                return false;
            }

            var fixedAmount = FixedPoint2.New(amount);
            if (fixedAmount <= FixedPoint2.Zero)
                return false;

            var amounts = parts[0] switch
            {
                "OUTPUT" => recipe.Outputs,
                "CONSUME" => recipe.ConsumedInputs,
                _ => recipe.Catalysts,
            };
            if (parts[0] == "CATALYST")
            {
                if (!amounts.TryGetValue(parts[1], out var current) || fixedAmount > current)
                    amounts[parts[1]] = fixedAmount;
            }
            else
            {
                if (!TryAddAmount(amounts, parts[1], fixedAmount))
                    return false;
            }

            if (parts[0] == "OUTPUT")
                hasOutput = true;

            if (++entries > MaxRecipeEntries)
                return false;
        }

        return hasOutput &&
               recipe.Outputs.Count > 0 &&
               Sum(recipe.Outputs.Values) <= MaxRecipeAmount &&
               !InputTotalExceedsLimit(recipe.ConsumedInputs, recipe.Catalysts);
    }

    private static Dictionary<string, FixedPoint2> GetInputs(RecipeData recipe)
    {
        return GetInputs(recipe.ConsumedInputs, recipe.Catalysts);
    }

    private static Dictionary<string, FixedPoint2> GetInputs(
        Dictionary<string, FixedPoint2> consumed,
        Dictionary<string, FixedPoint2> catalysts)
    {
        var inputs = new Dictionary<string, FixedPoint2>(consumed, StringComparer.Ordinal);
        foreach (var (id, amount) in catalysts)
        {
            var total = (long) inputs.GetValueOrDefault(id).Value + amount.Value;
            if (total > MaxRecipeAmount.Value)
                total = MaxRecipeAmount.Value;
            inputs[id] = FixedPoint2.FromCents((int) total);
        }
        return inputs;
    }

    private static FixedPoint2 Sum(IEnumerable<FixedPoint2> amounts)
    {
        var total = FixedPoint2.Zero;
        foreach (var amount in amounts)
            total += amount;
        return total;
    }

    private static bool InputTotalExceedsLimit(
        Dictionary<string, FixedPoint2> consumed,
        Dictionary<string, FixedPoint2> catalysts)
    {
        long total = 0;
        foreach (var amount in consumed.Values)
            total += amount.Value;
        foreach (var amount in catalysts.Values)
            total += amount.Value;
        return total > MaxRecipeAmount.Value;
    }

    private static string FormatAmount(FixedPoint2 amount)
    {
        return amount.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private bool TryGetReagent(string id, [NotNullWhen(true)] out Reagent? reagent)
    {
        return _reagents.TryIndex(new ReagentId(id, null), out reagent);
    }

    private sealed class RecipeData
    {
        public Dictionary<string, FixedPoint2> Outputs { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, FixedPoint2> ConsumedInputs { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, FixedPoint2> Catalysts { get; } = new(StringComparer.Ordinal);
    }
}
