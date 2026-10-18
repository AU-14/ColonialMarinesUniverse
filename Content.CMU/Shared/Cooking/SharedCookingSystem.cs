// SPDX-License-Identifier: AGPL-3.0-only
// Ported from the cooking of crystall-punk-14 (MIT) and adapted to this project.
using System.Linq;
using Content.Shared.Audio;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Fluids;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Cooking;

// Cooking, shared by both sides: works out which recipe fits the contents of a vessel, and cooks, burns and serves the dish.
public abstract partial class SharedCookingSystem : EntitySystem
{
    [Dependency] protected readonly SharedContainerSystem Container = default!;
    [Dependency] protected readonly SharedSolutionContainerSystem Solution = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPuddleSystem _puddle = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    // What a dish is made of when it burns: half of its solution turns into this.
    private readonly ProtoId<ReagentPrototype> _burntFoodReagent = "RUMCBurntFood";

    // The recipes by their complexity, the hardest first: the hardest are looked at first, and the plainest, which suit anything, are the last.
    protected List<CookingRecipePrototype> OrderedRecipes = [];

    // The tags that the things of the game get for the recipes, by the prototype of the thing (a thing counts as the prototypes it was made of too).
    private readonly Dictionary<string, HashSet<ProtoId<TagPrototype>>> _mappedTags = new();
    private readonly Dictionary<string, HashSet<ProtoId<TagPrototype>>> _tagsOfProto = new();

    // The tags that the things which have a tag of the game get.
    private readonly Dictionary<ProtoId<TagPrototype>, HashSet<ProtoId<TagPrototype>>> _tagsFromTags = new();

    public override void Initialize()
    {
        base.Initialize();

        InitTransfer();
        InitDoAfter();
        CacheRecipes();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<FoodHolderComponent, ExaminedEvent>(OnExaminedEvent);
    }

    public override void Update(float frameTime)
    {
        UpdateDoAfter();
    }

    private void CacheRecipes()
    {
        OrderedRecipes = _proto.EnumeratePrototypes<CookingRecipePrototype>()
            .Where(recipe => recipe.Requirements.Count > 0)
            .OrderByDescending(recipe => recipe.Requirements.Sum(condition => condition.GetComplexity()))
            .ToList();

        _mappedTags.Clear();
        _tagsOfProto.Clear();
        _tagsFromTags.Clear();
        foreach (var map in _proto.EnumeratePrototypes<CookingTagMapPrototype>())
        {
            foreach (var from in map.FromTags)
            {
                if (!_tagsFromTags.TryGetValue(from, out var given))
                    _tagsFromTags[from] = given = new HashSet<ProtoId<TagPrototype>>();

                foreach (var tag in map.Tags)
                    given.Add(tag);
            }

            foreach (var entity in map.Entities)
            {
                if (!_mappedTags.TryGetValue(entity.Id, out var set))
                    _mappedTags[entity.Id] = set = new HashSet<ProtoId<TagPrototype>>();

                foreach (var tag in map.Tags)
                    set.Add(tag);
            }
        }
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs ev)
    {
        if (ev.WasModified<EntityPrototype>() || ev.WasModified<CookingRecipePrototype>() || ev.WasModified<CookingTagMapPrototype>())
            CacheRecipes();
    }

    private void OnExaminedEvent(Entity<FoodHolderComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.FoodData?.Name is null)
            return;

        if (!Solution.TryGetSolution(ent.Owner, ent.Comp.SolutionId ?? "food", out _, out var solution) || solution.Volume == 0)
            return;

        args.PushMarkup(Loc.GetString("au14-cooking-examine", ("name", Loc.GetString(ent.Comp.FoodData.Name)), ("count", solution.Volume)));
    }

    // ---------------------------------------------------------------- the tags of the things
    // The tags of a thing: its own, and the ones that the map of the cooking gives to the prototype it was made of (or to the prototypes that one was made of).
    public IEnumerable<ProtoId<TagPrototype>> GetTags(EntityUid uid)
    {
        if (TryComp<TagComponent>(uid, out var tags))
        {
            foreach (var tag in tags.Tags)
            {
                yield return tag;

                if (_tagsFromTags.TryGetValue(tag, out var given))
                {
                    foreach (var extra in given)
                        yield return extra;
                }
            }
        }

        if (MetaData(uid).EntityPrototype is { } proto)
        {
            foreach (var tag in TagsOfProto(proto.ID))
                yield return tag;
        }
    }

    private HashSet<ProtoId<TagPrototype>> TagsOfProto(string id)
    {
        if (_tagsOfProto.TryGetValue(id, out var cached))
            return cached;

        var result = new HashSet<ProtoId<TagPrototype>>();
        _tagsOfProto[id] = result;

        if (_mappedTags.TryGetValue(id, out var own))
            result.UnionWith(own);

        // The abstract prototypes cannot be indexed, so the parents are read from the data of the prototype, abstract or not.
        if (_proto.TryGetMapping(typeof(EntityPrototype), id, out var data) && data.TryGet("parent", out var parentNode))
        {
            if (parentNode is ValueDataNode single)
            {
                result.UnionWith(TagsOfProto(single.Value));
            }
            else if (parentNode is SequenceDataNode sequence)
            {
                foreach (var node in sequence)
                {
                    if (node is ValueDataNode value)
                        result.UnionWith(TagsOfProto(value.Value));
                }
            }
        }

        return result;
    }

    // ---------------------------------------------------------------- the recipes
    public CookingRecipePrototype? GetRecipe(Entity<FoodCookerComponent> ent)
    {
        if (!Container.TryGetContainer(ent, ent.Comp.ContainerId, out var container))
            return null;

        Solution.TryGetSolution(ent.Owner, ent.Comp.SolutionId ?? "cooker", out _, out var solution);

        var allTags = new List<ProtoId<TagPrototype>>();
        foreach (var contained in container.ContainedEntities)
            allTags.AddRange(GetTags(contained));

        return GetRecipe(ent.Comp.FoodType, solution, allTags);
    }

    public CookingRecipePrototype? GetRecipe(ProtoId<CookingFoodTypePrototype> foodType, Solution? solution, List<ProtoId<TagPrototype>> allTags)
    {
        foreach (var recipe in OrderedRecipes)
        {
            if (recipe.FoodType != foodType)
                continue;

            var met = true;
            foreach (var condition in recipe.Requirements)
            {
                if (!condition.CheckRequirement(EntityManager, _proto, allTags, solution))
                {
                    met = false;
                    break;
                }
            }

            if (met)
                return recipe;
        }

        return null;
    }

    // Turns what lies in the vessel into the solution of the dish and puts the data of the dish in the holder.
    protected void CreateFoodData(Entity<FoodCookerComponent> ent, CookingRecipePrototype recipe)
    {
        if (!Solution.TryGetSolution(ent.Owner, ent.Comp.SolutionId ?? "cooker", out var soln, out var solution))
            return;

        if (!Container.TryGetContainer(ent, ent.Comp.ContainerId, out var container))
            return;

        var newData = new CookingFoodData(recipe.FoodData) { CurrentRecipe = recipe.ID };

        foreach (var contained in container.ContainedEntities.ToList())
        {
            if (TryComp<EdibleComponent>(contained, out var food))
            {
                if (Solution.TryGetSolution(contained, food.Solution, out _, out var foodSolution))
                {
                    Solution.TryMixAndOverflow(soln.Value, foodSolution, solution.MaxVolume, out var overflowed);
                    if (overflowed is not null)
                        _puddle.TrySplashSpillAt(ent, Transform(ent).Coordinates, overflowed, out _);
                }
            }

            if (TryComp<FlavorProfileComponent>(contained, out var flavorComp))
            {
                foreach (var flavor in flavorComp.Flavors)
                    newData.Flavors.Add(flavor);
            }

            QueueDel(contained);
        }

        // The inside of the dish that it has of its own (of the soup of the game, for the dishes that came from the soups of the game).
        foreach (var (reagent, amount) in recipe.ExtraReagents)
            Solution.TryAddReagent(soln.Value, reagent, amount, out _);

        if (solution.Volume <= 0)
            return;

        if (TryComp<FoodHolderComponent>(ent.Owner, out var holder))
        {
            holder.FoodData = newData;
            Dirty(ent.Owner, holder);
        }

        Dirty(ent);
    }

    protected void BurntFood(Entity<FoodCookerComponent> ent)
    {
        if (!TryComp<FoodHolderComponent>(ent, out var holder) || holder.FoodData is null)
            return;

        if (!Solution.TryGetSolution(ent.Owner, ent.Comp.SolutionId ?? "cooker", out var soln, out var solution))
            return;

        foreach (var visuals in holder.FoodData.Visuals)
            visuals.Color = Color.FromHex("#212121");

        holder.FoodData.Name = Loc.GetString("au14-cooking-burned-name");
        holder.FoodData.Desc = Loc.GetString("au14-cooking-burned-desc");

        var replaced = solution.Volume / 2;
        solution.SplitSolution(replaced);
        solution.AddReagent(_burntFoodReagent, replaced / 2);
        Solution.UpdateChemicals(soln.Value);

        DirtyField(ent.Owner, holder, nameof(FoodHolderComponent.FoodData));
    }

    // ---------------------------------------------------------------- the pouring from one holder to another
    private void InitTransfer()
    {
        SubscribeLocalEvent<FoodHolderComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<FoodHolderComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<FoodCookerComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
    }

    private void OnInteractUsing(Entity<FoodHolderComponent> target, ref InteractUsingEvent args)
    {
        if (!TryComp<FoodHolderComponent>(args.Used, out var used))
            return;

        TryTransferFood(target, (args.Used, used));
    }

    private void OnAfterInteract(Entity<FoodHolderComponent> ent, ref AfterInteractEvent args)
    {
        if (!TryComp<FoodHolderComponent>(args.Target, out var target))
            return;

        TryTransferFood(ent, (args.Target.Value, target));
    }

    // A dish that is done is not to be filled up with more ingredients.
    private void OnInsertAttempt(Entity<FoodCookerComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled || !TryComp<FoodHolderComponent>(ent, out var holder) || holder.FoodData is null)
            return;

        _popup.PopupEntity(Loc.GetString("au14-cooking-popup-not-empty", ("name", MetaData(ent).EntityName)), ent);
        args.Cancel();
    }

    protected virtual bool TryTransferFood(Entity<FoodHolderComponent> target, Entity<FoodHolderComponent> source)
    {
        if (!source.Comp.CanGiveFood || !target.Comp.CanAcceptFood || target.Comp.FoodType != source.Comp.FoodType || source.Comp.FoodData is null)
            return false;

        if (!TryComp<EdibleComponent>(target, out var holderFood))
            return false;

        if (!Solution.TryGetSolution(source.Owner, source.Comp.SolutionId ?? "cooker", out var cookerSoln, out var cookerSolution))
            return false;

        if (Solution.TryGetSolution(target.Owner, holderFood.Solution, out var holderSoln, out var solution))
        {
            if (solution.Volume > 0)
            {
                _popup.PopupEntity(Loc.GetString("au14-cooking-popup-not-empty", ("name", MetaData(target).EntityName)), target);
                return false;
            }

            Solution.TryTransferSolution(holderSoln.Value, cookerSolution, solution.MaxVolume);
        }

        UpdateFoodDataVisuals(target, source.Comp.FoodData);

        Dirty(target);
        Dirty(source);
        Solution.UpdateChemicals(cookerSoln.Value);

        return true;
    }

    protected virtual void UpdateFoodDataVisuals(Entity<FoodHolderComponent> ent, CookingFoodData data, bool rename = true)
    {
        if (rename)
        {
            if (data.Name is not null)
                _metaData.SetEntityName(ent, Loc.GetString(data.Name));
            if (data.Desc is not null)
                _metaData.SetEntityDescription(ent, Loc.GetString(data.Desc));
        }

        EnsureComp<FlavorProfileComponent>(ent, out var flavorComp);
        foreach (var flavor in data.Flavors)
            flavorComp.Flavors.Add(flavor);

        ent.Comp.FoodData = new CookingFoodData(data);
        foreach (var layer in ent.Comp.FoodData.Visuals)
        {
            if (_random.Prob(0.5f))
                layer.Scale = new System.Numerics.Vector2(-1, 1);
        }

        DirtyField(ent, ent.Comp, nameof(FoodHolderComponent.FoodData));
    }

    // ---------------------------------------------------------------- the heating and the cooking itself
    private void InitDoAfter()
    {
        SubscribeLocalEvent<FoodCookerComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<FoodCookerComponent, CookingDoAfter>(OnCookFinished);
        SubscribeLocalEvent<FoodCookerComponent, BurningDoAfter>(OnCookBurned);
    }

    // The cooking is dropped when the vessel is not heated often enough.
    private void UpdateDoAfter()
    {
        var query = EntityQueryEnumerator<FoodCookerComponent>();
        while (query.MoveNext(out _, out var cooker))
        {
            if (_timing.CurTime > cooker.LastHeatingTime + cooker.HeatingFrequencyRequired && _doAfter.IsRunning(cooker.DoAfterId))
                _doAfter.Cancel(cooker.DoAfterId);
        }
    }

    // A heater calls this on the vessel every second or so while it burns: the vessel cooks what is in it, and a dish that is done on the fire burns in the end.
    public void Heat(Entity<FoodCookerComponent> ent)
    {
        if (!Container.TryGetContainer(ent, ent.Comp.ContainerId, out var container) || !TryComp<FoodHolderComponent>(ent, out var holder))
            return;

        if (container.ContainedEntities.Count <= 0 && holder.FoodData is null)
        {
            StopCooking(ent);
            return;
        }

        ent.Comp.LastHeatingTime = _timing.CurTime;
        DirtyField(ent.Owner, ent.Comp, nameof(FoodCookerComponent.LastHeatingTime));

        if (!_doAfter.IsRunning(ent.Comp.DoAfterId) && holder.FoodData is null)
        {
            if (GetRecipe(ent) is { } recipe)
                StartCooking(ent, recipe);
        }
        else if (holder.FoodData is not null)
        {
            StartBurning(ent);
        }
    }

    protected virtual void OnCookBurned(Entity<FoodCookerComponent> ent, ref BurningDoAfter args)
    {
        StopCooking(ent);

        if (args.Cancelled || args.Handled)
            return;

        BurntFood(ent);
        args.Handled = true;
    }

    protected virtual void OnCookFinished(Entity<FoodCookerComponent> ent, ref CookingDoAfter args)
    {
        StopCooking(ent);

        if (args.Cancelled || args.Handled || !TryComp<FoodHolderComponent>(ent, out var holder))
            return;

        if (!_proto.TryIndex(args.Recipe, out var recipe))
            return;

        CreateFoodData(ent, recipe);
        UpdateFoodDataVisuals((ent, holder), holder.FoodData ?? recipe.FoodData, ent.Comp.RenameCooker);

        args.Handled = true;
    }

    private void StartCooking(Entity<FoodCookerComponent> ent, CookingRecipePrototype recipe)
    {
        if (_doAfter.IsRunning(ent.Comp.DoAfterId))
            return;

        _appearance.SetData(ent, CookingVisuals.Cooking, true);

        var args = new DoAfterArgs(EntityManager, ent, recipe.CookingTime, new CookingDoAfter(recipe.ID), ent)
        {
            NeedHand = false,
            BreakOnWeightlessMove = false,
            RequireCanInteract = false,
        };

        _doAfter.TryStartDoAfter(args, out var id);
        ent.Comp.DoAfterId = id;
        _ambientSound.SetAmbience(ent, true);
    }

    private void StartBurning(Entity<FoodCookerComponent> ent)
    {
        if (_doAfter.IsRunning(ent.Comp.DoAfterId))
            return;

        _appearance.SetData(ent, CookingVisuals.Burning, true);

        var args = new DoAfterArgs(EntityManager, ent, ent.Comp.BurnAfter, new BurningDoAfter(), ent)
        {
            NeedHand = false,
            BreakOnWeightlessMove = false,
            RequireCanInteract = false,
        };

        _doAfter.TryStartDoAfter(args, out var id);
        ent.Comp.DoAfterId = id;
        _ambientSound.SetAmbience(ent, true);
    }

    public void StopCooking(Entity<FoodCookerComponent> ent)
    {
        if (_doAfter.IsRunning(ent.Comp.DoAfterId))
            _doAfter.Cancel(ent.Comp.DoAfterId);

        _appearance.SetData(ent, CookingVisuals.Cooking, false);
        _appearance.SetData(ent, CookingVisuals.Burning, false);
        _ambientSound.SetAmbience(ent, false);
    }

    private void OnParentChanged(Entity<FoodCookerComponent> ent, ref EntParentChangedMessage args)
    {
        StopCooking(ent);
    }
}
