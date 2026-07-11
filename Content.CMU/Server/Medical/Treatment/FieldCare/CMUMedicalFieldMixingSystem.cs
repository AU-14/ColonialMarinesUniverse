using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Treatment.FieldCare;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.AU14;
using Content.Shared.ActionBlocker;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using TribalComponent = Content.Shared._CMU14.Threats.Mobs.Tribal.TribalComponent;

namespace Content.Server._CMU14.Medical.Treatment.FieldCare;

public sealed partial class CMUMedicalFieldMixingSystem : EntitySystem
{
    private const string PackedTraumaDressingStack = "CMUPlainTraumaDressing";

    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private CMUFieldTreatmentItemDiscoverySystem _itemDiscovery = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUMedicalIngredientComponent, AfterInteractEvent>(OnIngredientAfterInteract);
        SubscribeNetworkEvent<CMUMedicalFieldCraftingOpenRequestEvent>(OnOpenCraftingRequest);

        Subs.BuiEvents<CMUHumanMedicalComponent>(CMUMedicalFieldCraftingUI.Key, subs =>
        {
            subs.Event<CMUMedicalFieldCraftingChooseBuiMsg>(OnHumanCraftingChoice);
        });
        Subs.BuiEvents<YautjaComponent>(CMUMedicalFieldCraftingUI.Key, subs =>
        {
            subs.Event<CMUMedicalFieldCraftingChooseBuiMsg>(OnYautjaCraftingChoice);
        });
        Subs.BuiEvents<TribalComponent>(CMUMedicalFieldCraftingUI.Key, subs =>
        {
            subs.Event<CMUMedicalFieldCraftingChooseBuiMsg>(OnTribalCraftingChoice);
        });
    }

    public int ResolveIngredientUnitCost(int medicalSkill)
    {
        return medicalSkill switch
        {
            <= 1 => 5,
            2 => 2,
            _ => 1,
        };
    }

    public IReadOnlyList<CMUMedicalFieldCraftingOption> GetCraftableOptions(EntityUid user)
    {
        var items = _itemDiscovery.GetAccessibleItems(user);
        var options = new List<CMUMedicalFieldCraftingOption>(items.Count);
        var seen = new HashSet<(CMUFieldTreatmentFamily Family, CMUFieldTreatmentBaseKind BaseKind)>(items.Count);
        var ingredients = new List<AccessibleIngredient>(items.Count);
        var bases = new List<AccessibleBase>(items.Count);

        foreach (var item in items)
        {
            if (!TryComp<StackComponent>(item, out var stack))
                continue;

            if (TryComp<CMUMedicalIngredientComponent>(item, out var ingredient))
            {
                var ingredientCost = ResolveIngredientUnitCost(_skills.GetSkill(user, ingredient.Skill));
                if (stack.Count >= ingredientCost)
                    ingredients.Add(new AccessibleIngredient(ingredient, ingredientCost));
            }

            if (stack.Count >= 1 && TryComp<CMUMedicalMixingBaseComponent>(item, out var mixingBase))
                bases.Add(new AccessibleBase(mixingBase, stack));
        }

        foreach (var ingredient in ingredients)
        {
            foreach (var baseItem in bases)
            {
                if (!IsAllowedRecipe(ingredient.Component, baseItem.Component, baseItem.Stack) ||
                    !TryGetProduct(ingredient.Component, baseItem.Component.Kind, out var product))
                {
                    continue;
                }

                var key = (ingredient.Component.Family, baseItem.Component.Kind);
                if (!seen.Add(key))
                    continue;

                options.Add(new CMUMedicalFieldCraftingOption(
                    ingredient.Component.Family,
                    baseItem.Component.Kind,
                    product,
                    ingredient.Cost));
            }
        }

        return options;
    }

    public bool TryCraftAvailableTreatment(
        EntityUid user,
        CMUFieldTreatmentFamily family,
        CMUFieldTreatmentBaseKind baseKind,
        out EntityUid? product)
    {
        product = null;

        if (!Enum.IsDefined(family) || !Enum.IsDefined(baseKind))
            return false;

        if (!CanUseFieldCraftingMenu(user) || !_actionBlocker.CanInteract(user, null))
            return false;

        if (!TryFindCraftingPair(user, family, baseKind, out var ingredient, out var baseItem))
            return false;

        return TryMixTreatment(user, ingredient, baseItem, out product);
    }

    public bool TryOpenCraftingMenu(EntityUid user)
    {
        if (!CanUseFieldCraftingMenu(user) || !_actionBlocker.CanInteract(user, null))
            return false;

        var isOpen = _ui.IsUiOpen(user, CMUMedicalFieldCraftingUI.Key);
        var options = GetCraftableOptions(user);
        if (options.Count == 0)
        {
            if (isOpen)
                _ui.CloseUi(user, CMUMedicalFieldCraftingUI.Key, user);

            _popup.PopupEntity(Loc.GetString("cmu-field-treatment-no-craft-options"), user, user);
            return false;
        }

        _ui.SetUiState(user, CMUMedicalFieldCraftingUI.Key, new CMUMedicalFieldCraftingBuiState(new List<CMUMedicalFieldCraftingOption>(options)));
        if (!isOpen)
            _ui.OpenUi(user, CMUMedicalFieldCraftingUI.Key, user);

        return true;
    }

    public bool TryMixTreatment(
        EntityUid user,
        EntityUid ingredientUid,
        EntityUid baseUid,
        out EntityUid? product)
    {
        product = null;

        if (!TryComp<CMUMedicalIngredientComponent>(ingredientUid, out var ingredient) ||
            !TryComp<CMUMedicalMixingBaseComponent>(baseUid, out var mixingBase) ||
            !TryComp<StackComponent>(ingredientUid, out var ingredientStack) ||
            !TryComp<StackComponent>(baseUid, out var baseStack))
        {
            return false;
        }

        var cost = ResolveIngredientUnitCost(_skills.GetSkill(user, ingredient.Skill));
        if (ingredientStack.Count < cost || baseStack.Count < 1)
            return false;

        if (!IsAllowedRecipe(ingredient, mixingBase, baseStack))
            return false;

        var productId = mixingBase.Kind switch
        {
            CMUFieldTreatmentBaseKind.Gauze => ingredient.GauzeProduct,
            CMUFieldTreatmentBaseKind.TraumaDressing => ingredient.TraumaProduct,
            _ => default,
        };

        if (productId == default)
            return false;

        if (!_stacks.Use(ingredientUid, cost, ingredientStack))
            return false;

        if (!_stacks.Use(baseUid, 1, baseStack))
            return false;

        var spawned = Spawn(productId, Transform(user).Coordinates);
        product = spawned;
        _stacks.TryMergeToHands(spawned, user);
        return true;
    }

    private void OnOpenCraftingRequest(CMUMedicalFieldCraftingOpenRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { Valid: true } user)
            return;

        TryOpenCraftingMenu(user);
    }

    private void OnHumanCraftingChoice(Entity<CMUHumanMedicalComponent> ent, ref CMUMedicalFieldCraftingChooseBuiMsg args)
    {
        HandleCraftingChoice(ent.Owner, ref args);
    }

    private void OnYautjaCraftingChoice(Entity<YautjaComponent> ent, ref CMUMedicalFieldCraftingChooseBuiMsg args)
    {
        HandleCraftingChoice(ent.Owner, ref args);
    }

    private void OnTribalCraftingChoice(Entity<TribalComponent> ent, ref CMUMedicalFieldCraftingChooseBuiMsg args)
    {
        HandleCraftingChoice(ent.Owner, ref args);
    }

    private void HandleCraftingChoice(EntityUid owner, ref CMUMedicalFieldCraftingChooseBuiMsg args)
    {
        var user = args.Actor;
        if (user != owner || !CanUseFieldCraftingMenu(user))
            return;

        if (TryCraftAvailableTreatment(user, args.Family, args.BaseKind, out _))
            _popup.PopupEntity(Loc.GetString("cmu-field-treatment-mixed"), user, user);
        else
            _popup.PopupEntity(Loc.GetString("cmu-field-treatment-cannot-craft"), user, user);

        var options = GetCraftableOptions(user);
        if (options.Count == 0)
        {
            _ui.CloseUi(user, CMUMedicalFieldCraftingUI.Key, user);
            return;
        }

        _ui.SetUiState(user, CMUMedicalFieldCraftingUI.Key, new CMUMedicalFieldCraftingBuiState(new List<CMUMedicalFieldCraftingOption>(options)));
    }

    private void OnIngredientAfterInteract(Entity<CMUMedicalIngredientComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!TryMixTreatment(args.User, ent.Owner, target, out _))
            return;

        _popup.PopupEntity(Loc.GetString("cmu-field-treatment-mixed"), args.User, args.User);
        args.Handled = true;
    }

    private bool CanUseFieldCraftingMenu(EntityUid user)
    {
        return HasComp<CMUHumanMedicalComponent>(user) ||
               HasComp<YautjaComponent>(user) ||
               HasComp<TribalComponent>(user);
    }

    private bool TryFindCraftingPair(
        EntityUid user,
        CMUFieldTreatmentFamily family,
        CMUFieldTreatmentBaseKind baseKind,
        out EntityUid ingredientUid,
        out EntityUid baseUid)
    {
        ingredientUid = default;
        baseUid = default;
        CMUMedicalIngredientComponent? selectedIngredient = null;

        var items = _itemDiscovery.GetAccessibleItems(user);
        foreach (var candidate in items)
        {
            if (!TryComp<CMUMedicalIngredientComponent>(candidate, out var ingredient) ||
                ingredient.Family != family ||
                !TryGetProduct(ingredient, baseKind, out _) ||
                !TryComp<StackComponent>(candidate, out var ingredientStack))
            {
                continue;
            }

            var ingredientCost = ResolveIngredientUnitCost(_skills.GetSkill(user, ingredient.Skill));
            if (ingredientStack.Count < ingredientCost)
                continue;

            ingredientUid = candidate;
            selectedIngredient = ingredient;
            break;
        }

        if (ingredientUid == default || selectedIngredient == null)
            return false;

        foreach (var candidate in items)
        {
            if (!TryComp<CMUMedicalMixingBaseComponent>(candidate, out var mixingBase) ||
                mixingBase.Kind != baseKind ||
                !TryComp<StackComponent>(candidate, out var baseStack) ||
                baseStack.Count < 1 ||
                !IsAllowedRecipe(selectedIngredient, mixingBase, baseStack))
            {
                continue;
            }

            baseUid = candidate;
            return true;
        }

        return false;
    }

    private static bool TryGetProduct(
        CMUMedicalIngredientComponent ingredient,
        CMUFieldTreatmentBaseKind baseKind,
        out EntProtoId product)
    {
        product = baseKind switch
        {
            CMUFieldTreatmentBaseKind.Gauze => ingredient.GauzeProduct,
            CMUFieldTreatmentBaseKind.TraumaDressing => ingredient.TraumaProduct,
            _ => default,
        };

        return product != default;
    }

    private static bool IsAllowedRecipe(
        CMUMedicalIngredientComponent ingredient,
        CMUMedicalMixingBaseComponent mixingBase,
        StackComponent baseStack)
    {
        return ingredient.Family is CMUFieldTreatmentFamily.Hemostatic or CMUFieldTreatmentFamily.BurnGel &&
               mixingBase.Kind == CMUFieldTreatmentBaseKind.TraumaDressing &&
               baseStack.StackTypeId == PackedTraumaDressingStack;
    }

    private readonly record struct AccessibleIngredient(
        CMUMedicalIngredientComponent Component,
        int Cost);

    private readonly record struct AccessibleBase(
        CMUMedicalMixingBaseComponent Component,
        StackComponent Stack);
}
