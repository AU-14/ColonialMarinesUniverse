// SPDX-License-Identifier: AGPL-3.0-only
// Ported from the cooking of crystall-punk-14 (MIT) and adapted to this project.
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition;
using Content.Shared.Tag;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Cooking;

// ---------------------------------------------------------------- the prototypes
// The kind of the dish: what a vessel can cook and what a dish can be poured into (a soup into a bowl, a meal onto a plate, a pie stays in its mould).
[Prototype("cookingFoodType")]
public sealed partial class CookingFoodTypePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
}

// A recipe of the dish: what must lie in the vessel (tags of the ingredients, reagents) and what comes out (the look, the name, the trash that is left).
[Prototype("cookingRecipe")]
public sealed partial class CookingRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    // The conditions that the ingredients must meet. The more and the harder they are, the later the recipe is looked at: the plainest recipes are for anything.
    [DataField]
    public List<CookingCraftRequirement> Requirements = new();

    // The reagents cannot hold what the dish looks like, so the look, the name and the description go here.
    [DataField]
    public CookingFoodData FoodData = new();

    [DataField(required: true)]
    public ProtoId<CookingFoodTypePrototype> FoodType;

    [DataField]
    public TimeSpan CookingTime = TimeSpan.FromSeconds(20f);

    // What the dish adds to the solution of the vessel when it is done: for the dishes that came from the soups of the game it is the inside of the soup of the game.
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> ExtraReagents = new();
}

// Gives tags to the things of the game that the recipes ask for, without touching those things: every thing of the listed prototypes (or made of them) counts as having the tags.
[Prototype("cookingTagMap")]
public sealed partial class CookingTagMapPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<ProtoId<TagPrototype>> Tags = new();

    [DataField]
    public List<EntProtoId> Entities = new();

    // And every thing that has one of these tags of the game.
    [DataField]
    public List<ProtoId<TagPrototype>> FromTags = new();
}

// ---------------------------------------------------------------- the requirements
[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class CookingCraftRequirement
{
    public abstract bool CheckRequirement(IEntityManager entManager, IPrototypeManager protoManager, List<ProtoId<TagPrototype>> placedTags, Solution? solution = null);

    public abstract float GetComplexity();
}

// A cooking requirement that is always met.
public sealed partial class CookingAlwaysMet : CookingCraftRequirement
{
    public override bool CheckRequirement(IEntityManager entManager, IPrototypeManager protoManager, List<ProtoId<TagPrototype>> placedTags, Solution? solution = null) => true;

    public override float GetComplexity() => 0;
}

// A cooking requirement: the ingredients must have these tags (optionally nothing else).
public sealed partial class CookingTagRequired : CookingCraftRequirement
{
    // Any of these tags is enough.
    [DataField(required: true)]
    public List<ProtoId<TagPrototype>> Tags = default!;

    [DataField]
    public bool AllowOtherTags = true;

    public override bool CheckRequirement(IEntityManager entManager, IPrototypeManager protoManager, List<ProtoId<TagPrototype>> placedTags, Solution? solution = null)
    {
        foreach (var placed in placedTags)
        {
            if (Tags.Contains(placed))
                return true;
        }

        return false;
    }

    public override float GetComplexity() => AllowOtherTags ? 5 : 1;
}

// A cooking requirement: the ingredients must not have these tags.
public sealed partial class CookingTagBlocked : CookingCraftRequirement
{
    [DataField(required: true)]
    public HashSet<ProtoId<TagPrototype>> Tags = default!;

    public override bool CheckRequirement(IEntityManager entManager, IPrototypeManager protoManager, List<ProtoId<TagPrototype>> placedTags, Solution? solution = null)
    {
        foreach (var placed in placedTags)
        {
            if (Tags.Contains(placed))
                return false;
        }

        return true;
    }

    public override float GetComplexity() => Tags.Count * -1;
}

// A cooking requirement: the pot must hold at least this amount of these reagents.
public sealed partial class CookingReagentRequired : CookingCraftRequirement
{
    [DataField(required: true)]
    public HashSet<ProtoId<ReagentPrototype>> Reagents = default!;

    [DataField]
    public FixedPoint2 Amount = 10f;

    public override bool CheckRequirement(IEntityManager entManager, IPrototypeManager protoManager, List<ProtoId<TagPrototype>> placedTags, Solution? solution = null)
    {
        if (solution is null)
            return false;

        foreach (var (reagent, quantity) in solution.Contents)
        {
            if (Reagents.Contains(reagent.Prototype) && quantity >= Amount)
                return true;
        }

        return false;
    }

    public override float GetComplexity() => 1;
}

// ---------------------------------------------------------------- the components
// Prepares the dish of the things in the vessel and keeps it in the same vessel (its FoodHolder), while it is heated.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), Access(typeof(SharedCookingSystem))]
public sealed partial class FoodCookerComponent : Component
{
    [DataField(required: true)]
    public ProtoId<CookingFoodTypePrototype> FoodType;

    // The container of the ingredients.
    [DataField]
    public string ContainerId = "storagebase";

    [DataField]
    public string? SolutionId;

    public DoAfterId? DoAfterId = null;

    // When the vessel was heated last: the cooking is dropped if it is not heated for long.
    [DataField, AutoNetworkedField]
    public TimeSpan LastHeatingTime = TimeSpan.Zero;

    // How often it must be heated for the cooking not to be dropped.
    [DataField]
    public TimeSpan HeatingFrequencyRequired = TimeSpan.FromSeconds(2f);

    // How long a finished dish may stay on the fire before it burns.
    [DataField]
    public TimeSpan BurnAfter = TimeSpan.FromSeconds(20f);

    [DataField]
    public bool RenameCooker = false;
}

// A vessel or a dish that holds the cooked food: it is the look of the food (layers over the vessel), its name and its solution. Food of the same kind can be poured from one to another.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true, raiseAfterAutoHandleState: true), Access(typeof(SharedCookingSystem))]
public sealed partial class FoodHolderComponent : Component
{
    [DataField, AutoNetworkedField]
    public CookingFoodData? FoodData;

    [DataField]
    public bool CanAcceptFood = false;

    [DataField]
    public bool CanGiveFood = false;

    [DataField(required: true)]
    public ProtoId<CookingFoodTypePrototype> FoodType;

    [DataField]
    public string? SolutionId;

    // The layer of the sprite after which the layers of the food are put.
    [DataField]
    public string TargetLayerMap = "au14_foodLayers";

    public HashSet<string> RevealedLayers = new();
}

// The cooked dish: its recipe, name, description, look and flavors.
[Serializable]
[DataDefinition]
public sealed partial class CookingFoodData
{
    public CookingFoodData()
    {
    }

    public CookingFoodData(CookingFoodData data)
    {
        CurrentRecipe = data.CurrentRecipe;
        Name = data.Name;
        Desc = data.Desc;
        Visuals = new List<PrototypeLayerData>(data.Visuals);
        Flavors = new HashSet<ProtoId<FlavorPrototype>>(data.Flavors);
    }

    [DataField]
    public ProtoId<CookingRecipePrototype>? CurrentRecipe;

    [DataField]
    public LocId? Name;

    [DataField]
    public LocId? Desc;

    [DataField]
    public List<PrototypeLayerData> Visuals = new();

    [DataField]
    public HashSet<ProtoId<FlavorPrototype>> Flavors = new();
}

// What the appearance of a cooking vessel shows.
[Serializable, NetSerializable]
public enum CookingVisuals : byte
{
    Cooking,
    Burning,
}

// The do-after of cooking a dish; carries the recipe.
[Serializable, NetSerializable]
public sealed partial class CookingDoAfter : DoAfterEvent
{
    [DataField]
    public ProtoId<CookingRecipePrototype> Recipe;

    public CookingDoAfter(ProtoId<CookingRecipePrototype> recipe)
    {
        Recipe = recipe;
    }

    public override DoAfterEvent Clone() => this;
}

// The do-after of a dish that is burning.
[Serializable, NetSerializable]
public sealed partial class BurningDoAfter : SimpleDoAfterEvent;
