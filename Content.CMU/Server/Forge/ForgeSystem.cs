// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Linq;
using Content.Shared.CMU14.Forge;
using Content.Shared.CMU14.Portable;
using Content.Shared._RMC14.Campfire;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Tools;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Forge;

// The forge: a crucible that melts metal and holds it hot, tongs that carry hot items, and forgeable items that heat and cool.
public sealed partial class ForgeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly RMCReagentSystem _reagent = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ToolSystem _toolSystem = default!;
    [Dependency] private readonly PuddleSystem _puddle = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedBodyPartHealthSystem _bodyPartHealth = default!;
    [Dependency] private readonly CMUMedicalBodyIndexSystem _bodyIndex = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ToolWearSystem _wear = default!;

    private static readonly SoundSpecifier PourSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splash.ogg");
    private static readonly SoundSpecifier CastDoneSound = new SoundPathSpecifier("/Audio/Effects/sizzle.ogg");
    private static readonly SoundSpecifier ScrapSound = new SoundCollectionSpecifier("MetalBreak");
    private static readonly SoundSpecifier BurnSound = new SoundPathSpecifier("/Audio/Effects/lightburn.ogg");

    private const string TongsSlotId = "crucible";

    public override void Initialize()
    {
        SubscribeLocalEvent<ForgeComponent, InteractUsingEvent>(OnCrucibleInteractUsing);
        SubscribeLocalEvent<ForgeComponent, AfterInteractEvent>(OnCrucibleAfterInteract);
        SubscribeLocalEvent<ForgeComponent, ExaminedEvent>(OnCrucibleExamined);
        SubscribeLocalEvent<ForgeableComponent, ExaminedEvent>(OnForgeableExamined);
        SubscribeLocalEvent<ForgeableComponent, GotEquippedHandEvent>(OnForgeableEquippedHand);
        SubscribeLocalEvent<ForgeComponent, DestructionEventArgs>(OnCrucibleDestroyed);
        SubscribeLocalEvent<ForgeComponent, GetVerbsEvent<AlternativeVerb>>(OnCrucibleGetVerbs);

        SubscribeLocalEvent<ForgeTongsComponent, AfterInteractEvent>(OnTongsAfterInteract);
        SubscribeLocalEvent<ForgeTongsComponent, DestructionEventArgs>(OnTongsDestroyed);

        SubscribeLocalEvent<SmelteryComponent, DestructionEventArgs>(OnSmelteryDestroyed);
        SubscribeLocalEvent<SmelteryComponent, ExaminedEvent>(OnSmelteryExamined);
        SubscribeLocalEvent<SmelteryComponent, PortablePickUpAttemptEvent>(OnSmelteryPickUpAttempt);

        SubscribeLocalEvent<AnvilComponent, DestructionEventArgs>(OnAnvilDestroyed);
        SubscribeLocalEvent<AnvilWorkpieceComponent, ExaminedEvent>(OnWorkpieceExamined);

        SubscribeLocalEvent<MoldComponent, InteractUsingEvent>(OnMoldInteractUsing);
        SubscribeLocalEvent<MoldComponent, ExaminedEvent>(OnMoldExamined);
        SubscribeLocalEvent<MoldComponent, GetVerbsEvent<AlternativeVerb>>(OnMoldGetVerbsAlternative);
    }

    private void OnCrucibleInteractUsing(Entity<ForgeComponent> forge, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<StackComponent>(args.Used, out var stack))
            return;

        var ingredient = forge.Comp.Ingredients.FirstOrDefault(i => i.Input == stack.StackTypeId);
        if (ingredient == null)
            return;

        args.Handled = true;

        if (!_solutionContainer.TryGetSolution(forge.Owner, forge.Comp.Solution, out var soln, out var solution))
            return;

        var available = _stack.GetCount((args.Used, stack));
        var maxUnitsFromSpace = (int) MathF.Floor(solution.AvailableVolume.Float() / ingredient.ReagentPerUnit.Float());
        var taken = Math.Min(available, maxUnitsFromSpace);

        if (taken <= 0)
        {
            _popup.PopupEntity(Loc.GetString("au14-forge-full"), forge, args.User);
            return;
        }

        _stack.SetCount((args.Used, stack), available - taken);

        var addAmount = ingredient.ReagentPerUnit * taken;
        _solutionContainer.TryAddReagent(soln.Value, ingredient.Reagent, addAmount, out _);
        forge.Comp.Loaded[ingredient.Input] = forge.Comp.Loaded.GetValueOrDefault(ingredient.Input) + taken;

        _popup.PopupEntity(
            Loc.GetString("au14-forge-inserted", ("count", taken), ("reagent", GetReagentName(ingredient.Reagent))),
            forge,
            args.User);
    }

    private void OnCrucibleGetVerbs(Entity<ForgeComponent> forge, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        if (!_solutionContainer.TryGetSolution(forge.Owner, forge.Comp.Solution, out _, out var solution) || solution.Volume <= FixedPoint2.Zero)
            return;

        var user = args.User;
        var target = forge;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("au14-forge-verb-pour-out"),
            Priority = 2,
            Act = () => PourOut(target, user),
        });

        if (forge.Comp.Loaded.Count > 0)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("au14-forge-verb-take-back"),
                Priority = 1,
                Act = () => TakeBack(target, user),
            });
        }
    }

    // Tips the melt out onto the floor, whatever it is and however hot it is.
    private void PourOut(Entity<ForgeComponent> forge, EntityUid user)
    {
        if (!_solutionContainer.TryGetSolution(forge.Owner, forge.Comp.Solution, out var soln, out var solution) || solution.Volume <= FixedPoint2.Zero)
            return;

        var spill = _solutionContainer.SplitSolution(soln.Value, solution.Volume);
        _puddle.TrySpillAt(forge.Owner, spill, out _);
        forge.Comp.Loaded.Clear();

        _audio.PlayPvs(PourSound, forge);
        _popup.PopupEntity(Loc.GetString("au14-forge-poured-out"), forge, user);
    }

    // Gives back what was put in, as long as none of it has begun to melt.
    private void TakeBack(Entity<ForgeComponent> forge, EntityUid user)
    {
        if (forge.Comp.Loaded.Count == 0 || !TryComp<ForgeableComponent>(forge.Owner, out var forgeable) ||
            !_solutionContainer.TryGetSolution(forge.Owner, forge.Comp.Solution, out var soln, out var solution))
        {
            return;
        }

        var meltingPoint = 0f;
        foreach (var quantity in solution)
        {
            if (_reagent.TryIndex(quantity.Reagent, out var reagentProto) && reagentProto.MeltingPoint is { } point)
                meltingPoint = Math.Max(meltingPoint, point);
        }

        // Once it is molten it is no longer ore.
        if (forgeable.Temperature >= meltingPoint)
        {
            _popup.PopupEntity(Loc.GetString("au14-forge-take-back-molten"), forge, user, PopupType.SmallCaution);
            return;
        }

        var coords = Transform(forge).Coordinates;
        foreach (var (input, count) in forge.Comp.Loaded)
        {
            var ingredient = forge.Comp.Ingredients.FirstOrDefault(i => i.Input == input);
            if (ingredient == null || !_proto.TryIndex(input, out var stackProto) || string.IsNullOrEmpty(stackProto.Spawn.Id))
                continue;

            var amount = ingredient.ReagentPerUnit * count;
            _solutionContainer.RemoveReagent(soln.Value, ingredient.Reagent, amount);

            var returned = Spawn(stackProto.Spawn, coords);
            if (TryComp<StackComponent>(returned, out var returnedStack))
                _stack.SetCount((returned, returnedStack), count);

            _hands.TryPickupAnyHand(user, returned);
        }

        forge.Comp.Loaded.Clear();
        _popup.PopupEntity(Loc.GetString("au14-forge-took-back"), forge, user);
    }

    private void OnCrucibleAfterInteract(Entity<ForgeComponent> forge, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!HasComp<MoldComponent>(target))
            return;

        args.Handled = true;
        TryPour(forge, (target, Comp<MoldComponent>(target)), args.User);
    }

    private void OnTongsAfterInteract(Entity<ForgeTongsComponent> tongs, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        var user = args.User;
        var container = GetTongsContainer(tongs);

        if (args.Target is not { } target)
        {
            if (container.ContainedEntity is { } gripped)
            {
                args.Handled = true;
                if (DropForgeable(tongs, container, gripped, args.ClickLocation, user))
                    _wear.Use(tongs, user);
            }

            return;
        }

        if (TryComp<ForgeableComponent>(target, out var forgeableComp))
        {
            args.Handled = true;
            if (GrabForgeable(tongs, container, (target, forgeableComp), user))
                _wear.Use(tongs, user);

            return;
        }

        if (!HasComp<SmelteryComponent>(target) && !HasComp<AnvilComponent>(target) && !HasComp<MoldComponent>(target))
            return;

        args.Handled = true;

        if (container.ContainedEntity is not { } heldUid || !TryComp<ForgeableComponent>(heldUid, out var heldForgeable))
        {
            _popup.PopupEntity(Loc.GetString("au14-forge-tongs-empty"), target, user, PopupType.SmallCaution);
            return;
        }

        var held = new Entity<ForgeableComponent>(heldUid, heldForgeable);

        if (HasComp<SmelteryComponent>(target))
        {
            if (SeatOnSmeltery(container, held, target, user))
                _wear.Use(tongs, user);
        }
        else if (HasComp<AnvilComponent>(target))
        {
            if (SeatOnAnvil(container, held, target, user))
                _wear.Use(tongs, user);
        }
        else if (TryComp<ForgeComponent>(heldUid, out var heldForgeComp))
        {
            if (TryPour((heldUid, heldForgeComp), (target, Comp<MoldComponent>(target)), user))
                _wear.Use(tongs, user);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("au14-forge-nothing-to-pour"), target, user, PopupType.SmallCaution);
        }
    }

    private ContainerSlot GetTongsContainer(EntityUid tongs) =>
        _container.EnsureContainer<ContainerSlot>(tongs, TongsSlotId);

    private bool GrabForgeable(Entity<ForgeTongsComponent> tongs, ContainerSlot container, Entity<ForgeableComponent> forgeable, EntityUid user)
    {
        if (container.ContainedEntity != null)
            return false;

        // Of a pile of ingots the tongs take a single one: only one goes into a furnace or onto an anvil.
        if (TryComp<StackComponent>(forgeable, out var pile) && pile.Count > 1)
        {
            var coords = Transform(forgeable).Coordinates;
            if (_stack.Split((forgeable.Owner, pile), 1, coords, user) is not { } single || !TryComp<ForgeableComponent>(single, out var singleForgeable))
                return false;

            singleForgeable.Temperature = forgeable.Comp.Temperature;
            Dirty(single, singleForgeable);
            forgeable = (single, singleForgeable);
        }

        if (forgeable.Comp.SeatedOn != null)
        {
            forgeable.Comp.SeatedOn = null;
            Dirty(forgeable);
            _transform.Unanchor(forgeable);
            _appearance.SetData(forgeable.Owner, ForgeableVisuals.Seat, ForgeableSeatState.None);
        }

        _hands.TryDrop(user, forgeable.Owner);

        if (!_container.Insert(forgeable.Owner, container))
            return false;

        _popup.PopupEntity(Loc.GetString("au14-forge-grabbed"), tongs, user);
        return true;
    }

    private bool DropForgeable(Entity<ForgeTongsComponent> tongs, ContainerSlot container, EntityUid item, EntityCoordinates location, EntityUid user)
    {
        if (!_container.Remove(item, container, destination: location))
            return false;

        _popup.PopupEntity(Loc.GetString("au14-forge-dropped"), tongs, user);
        return true;
    }

    private bool SeatOnSmeltery(ContainerSlot container, Entity<ForgeableComponent> forgeable, EntityUid smelteryTarget, EntityUid user)
    {
        if (forgeable.Comp.SeatedOn != null)
            return false;

        if (GetSeatOccupant(smelteryTarget) != null)
        {
            _popup.PopupEntity(Loc.GetString("au14-forge-furnace-occupied"), smelteryTarget, user);
            return false;
        }

        var coords = Transform(smelteryTarget).Coordinates;
        if (!_container.Remove(forgeable.Owner, container, destination: coords))
            return false;

        _transform.AnchorEntity(forgeable.Owner);

        forgeable.Comp.SeatedOn = smelteryTarget;
        Dirty(forgeable);
        _appearance.SetData(forgeable.Owner, ForgeableVisuals.Seat, ForgeableSeatState.Furnace);

        _popup.PopupEntity(Loc.GetString("au14-forge-attached"), smelteryTarget, user);
        return true;
    }

    private bool SeatOnAnvil(ContainerSlot container, Entity<ForgeableComponent> forgeable, EntityUid anvilTarget, EntityUid user)
    {
        if (forgeable.Comp.SeatedOn != null)
            return false;

        if (GetSeatOccupant(anvilTarget) != null)
        {
            _popup.PopupEntity(Loc.GetString("au14-anvil-occupied"), anvilTarget, user);
            return false;
        }

        var coords = Transform(anvilTarget).Coordinates;
        if (!_container.Remove(forgeable.Owner, container, destination: coords))
            return false;

        _transform.AnchorEntity(forgeable.Owner);

        forgeable.Comp.SeatedOn = anvilTarget;
        Dirty(forgeable);
        _appearance.SetData(forgeable.Owner, ForgeableVisuals.Seat, ForgeableSeatState.Anvil);

        _popup.PopupEntity(Loc.GetString("au14-anvil-placed"), anvilTarget, user);
        return true;
    }

    private void OnTongsDestroyed(Entity<ForgeTongsComponent> tongs, ref DestructionEventArgs args)
    {
        var container = GetTongsContainer(tongs);
        if (container.ContainedEntity is { } held)
            _container.Remove(held, container, destination: Transform(tongs).Coordinates);
    }

    private void OnSmelteryDestroyed(Entity<SmelteryComponent> smeltery, ref DestructionEventArgs args)
    {
        FreeSeatOccupant(smeltery.Owner);
    }

    private void OnSmelteryExamined(Entity<SmelteryComponent> smeltery, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("au14-smeltery-examine-max", ("temperature", (int) smeltery.Comp.MaxTemperature)));
    }

    // A furnace with a crucible in it, or with a fire in it, is not carried off.
    private void OnSmelteryPickUpAttempt(Entity<SmelteryComponent> smeltery, ref PortablePickUpAttemptEvent args)
    {
        if (GetSeatOccupant(smeltery.Owner) != null)
        {
            _popup.PopupEntity(Loc.GetString("au14-smeltery-occupied"), smeltery, args.User, PopupType.SmallCaution);
            args.Cancel();
        }
        else if (TryComp<CampfireComponent>(smeltery, out var campfire) && campfire.Lit)
        {
            _popup.PopupEntity(Loc.GetString("au14-smeltery-lit"), smeltery, args.User, PopupType.SmallCaution);
            args.Cancel();
        }
    }

    private void OnAnvilDestroyed(Entity<AnvilComponent> anvil, ref DestructionEventArgs args)
    {
        FreeSeatOccupant(anvil.Owner);
    }

    private void FreeSeatOccupant(EntityUid seat)
    {
        if (GetSeatOccupant(seat) is not { } occupant || !TryComp<ForgeableComponent>(occupant, out var forgeableComp))
            return;

        forgeableComp.SeatedOn = null;
        Dirty(occupant, forgeableComp);
        _transform.Unanchor(occupant);
        _appearance.SetData(occupant, ForgeableVisuals.Seat, ForgeableSeatState.None);
    }

    private void OnWorkpieceExamined(Entity<AnvilWorkpieceComponent> workpiece, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !workpiece.Comp.Forging)
            return;

        if (workpiece.Comp.Recipe is not { } recipeId || !_proto.TryIndex(recipeId, out var recipe))
            return;

        var remaining = recipe.Steps.Count - workpiece.Comp.StepIndex;

        using var block = args.PushGroup(nameof(AnvilWorkpieceComponent));
        args.PushMarkup(Loc.GetString("au14-anvil-examine-forging", ("remaining", remaining)));
    }

    private bool TryPour(Entity<ForgeComponent> forge, Entity<MoldComponent> mold, EntityUid user)
    {
        if (!TryComp<ForgeableComponent>(forge.Owner, out var forgeable))
            return false;

        if (!_solutionContainer.TryGetSolution(forge.Owner, forge.Comp.Solution, out var crucibleSolnEnt, out var crucibleSoln))
            return false;

        if (crucibleSoln.Volume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("au14-forge-pour-empty"), forge, user);
            return false;
        }

        var requiredTemp = 0f;
        foreach (var rq in crucibleSoln)
        {
            if (_reagent.TryIndex(rq.Reagent, out var reagentProto) && reagentProto.MeltingPoint is { } point)
                requiredTemp = Math.Max(requiredTemp, point);
        }

        if (forgeable.Temperature < requiredTemp)
        {
            _popup.PopupEntity(
                Loc.GetString("au14-forge-pour-cold", ("temperature", (int) forgeable.Temperature), ("required", (int) requiredTemp)),
                forge,
                user,
                PopupType.SmallCaution);
            return false;
        }

        if (mold.Comp.Cooling)
        {
            _popup.PopupEntity(Loc.GetString("au14-mold-cooling"), mold, user, PopupType.SmallCaution);
            return false;
        }

        if (!_solutionContainer.TryGetSolution(mold.Owner, mold.Comp.Solution, out var moldSolnEnt, out var moldSoln))
            return false;

        if (moldSoln.AvailableVolume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("au14-mold-full"), mold, user, PopupType.SmallCaution);
            return false;
        }

        var poured = crucibleSoln.Volume;
        if (!_solutionContainer.TryTransferSolution(moldSolnEnt.Value, crucibleSoln, poured))
            return false;

        _solutionContainer.UpdateChemicals(crucibleSolnEnt.Value);
        forge.Comp.Loaded.Clear();

        _audio.PlayPvs(PourSound, mold);
        _popup.PopupEntity(Loc.GetString("au14-forge-poured"), mold, user);

        TryStartCasting(mold, moldSolnEnt.Value, moldSoln, user);
        return true;
    }

    private void TryStartCasting(Entity<MoldComponent> mold, Entity<SolutionComponent> moldSolnEnt, Solution solution, EntityUid user)
    {
        var recipe = FindMoldRecipe(mold, solution);
        if (recipe == null)
        {
            _popup.PopupEntity(Loc.GetString("au14-mold-scrap"), mold, user, PopupType.SmallCaution);
            Dirty(mold);
            return;
        }

        // The melt that is in the mold makes as many castings as it has the ingredients for: ten of melt is an ingot, and a crucible of four ores makes four.
        var batch = int.MaxValue;
        foreach (var (reagent, amount) in recipe.Ingredients)
            batch = Math.Min(batch, (solution.GetTotalPrototypeQuantity(reagent) / amount).Int());

        batch = Math.Max(1, batch);
        foreach (var (reagent, amount) in recipe.Ingredients)
            _solutionContainer.RemoveReagent(moldSolnEnt, reagent, amount * batch);

        mold.Comp.Cooling = true;
        mold.Comp.CoolingBatch = batch;
        mold.Comp.CoolingRecipe = recipe;
        mold.Comp.SolidifiesAt = _timing.CurTime + recipe.CoolTime;
        Dirty(mold);
        _appearance.SetData(mold.Owner, MoldVisuals.State, MoldSpriteState.Cooling);

        _popup.PopupEntity(Loc.GetString("au14-mold-cast-cooling", ("name", recipe.Name)), mold, user);
    }

    private MoldRecipe? FindMoldRecipe(Entity<MoldComponent> mold, Solution solution)
    {
        foreach (var recipe in mold.Comp.Recipes)
        {
            var ok = true;
            foreach (var (reagent, amount) in recipe.Ingredients)
            {
                if (solution.GetTotalPrototypeQuantity(reagent) < amount)
                {
                    ok = false;
                    break;
                }
            }

            if (!ok)
                continue;

            var present = solution.Contents.Count(rq => rq.Quantity > FixedPoint2.Zero);
            if (present > recipe.Ingredients.Count)
                continue;

            return recipe;
        }

        return null;
    }

    private void OnCrucibleDestroyed(Entity<ForgeComponent> forge, ref DestructionEventArgs args)
    {
        if (!_solutionContainer.TryGetSolution(forge.Owner, forge.Comp.Solution, out var soln, out var solution))
            return;

        if (solution.Volume <= FixedPoint2.Zero)
            return;

        var spill = _solutionContainer.SplitSolution(soln.Value, solution.Volume);
        _puddle.TrySpillAt(forge.Owner, spill, out _);
    }

    private void OnMoldInteractUsing(Entity<MoldComponent> mold, ref InteractUsingEvent args)
    {
        if (args.Handled || mold.Comp.Cooling || mold.Comp.Ready)
            return;

        if (!_toolSystem.HasQuality(args.Used, SharedToolSystem.CutQuality))
            return;

        if (!_solutionContainer.TryGetSolution(mold.Owner, mold.Comp.Solution, out var soln, out var solution) ||
            solution.Volume <= FixedPoint2.Zero)
            return;

        args.Handled = true;

        _solutionContainer.RemoveAllSolution(soln.Value);
        Dirty(mold);

        _audio.PlayPvs(ScrapSound, mold);
        _popup.PopupEntity(Loc.GetString("au14-mold-cleared"), mold, args.User);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var forgeableQuery = EntityQueryEnumerator<ForgeableComponent>();
        while (forgeableQuery.MoveNext(out var uid, out var comp))
        {
            var forgeable = new Entity<ForgeableComponent>(uid, comp);
            var lit = IsHeated(forgeable);

            // What the furnace can reach is the limit, however much more the crucible could take.
            var ceiling = comp.MaxTemperature;
            if (lit && comp.SeatedOn is { } furnace && TryComp<SmelteryComponent>(furnace, out var smeltery))
                ceiling = Math.Min(ceiling, smeltery.MaxTemperature);

            var before = comp.Temperature;
            comp.Temperature = lit
                ? (comp.Temperature <= ceiling
                    ? Math.Min(ceiling, comp.Temperature + comp.HeatGainPerSecond * frameTime)
                    : Math.Max(ceiling, comp.Temperature - comp.HeatLossPerSecond * frameTime))
                : Math.Max(comp.AmbientTemperature, comp.Temperature - comp.HeatLossPerSecond * frameTime);

            var glowing = comp.Temperature >= comp.GlowTemperature;
            if (glowing != comp.Active)
            {
                comp.Active = glowing;
                _appearance.SetData(uid, ForgeableVisuals.Active, glowing);
            }

            if (MathF.Abs(before - comp.Temperature) > 0.01f)
                Dirty(forgeable);
        }

        var moldQuery = EntityQueryEnumerator<MoldComponent>();
        while (moldQuery.MoveNext(out var uid, out var comp))
        {
            if (!comp.Cooling || comp.SolidifiesAt is not { } at || now < at)
                continue;

            OnCastingReady((uid, comp));
        }
    }

    private void OnCastingReady(Entity<MoldComponent> mold)
    {
        var recipe = mold.Comp.CoolingRecipe;

        mold.Comp.Cooling = false;
        mold.Comp.Ready = true;
        mold.Comp.SolidifiesAt = null;
        Dirty(mold);
        _appearance.SetData(mold.Owner, MoldVisuals.State, MoldSpriteState.Ready);

        if (recipe == null)
            return;

        _popup.PopupEntity(Loc.GetString("au14-mold-cast-ready", ("name", recipe.Name)), mold, PopupType.Medium);
    }

    private void OnMoldGetVerbsAlternative(Entity<MoldComponent> mold, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !mold.Comp.Ready)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("au14-mold-verb-extract"),
            Act = () => ExtractCasting(mold, user),
        });
    }

    private void ExtractCasting(Entity<MoldComponent> mold, EntityUid user)
    {
        if (!mold.Comp.Ready)
            return;

        var recipe = mold.Comp.CoolingRecipe;
        var batch = Math.Max(1, mold.Comp.CoolingBatch);

        mold.Comp.Ready = false;
        mold.Comp.CoolingRecipe = null;
        mold.Comp.CoolingBatch = 1;
        Dirty(mold);
        _appearance.SetData(mold.Owner, MoldVisuals.State, MoldSpriteState.Empty);

        if (recipe == null)
            return;

        var coords = Transform(mold).Coordinates;
        for (var i = 0; i < recipe.OutputCount * batch; i++)
        {
            var spawned = Spawn(recipe.Output, coords);
            if (i == 0)
                _hands.TryPickupAnyHand(user, spawned);
        }

        _audio.PlayPvs(CastDoneSound, mold);
        _popup.PopupEntity(Loc.GetString("au14-mold-cast-done", ("name", recipe.Name)), mold, PopupType.Medium);
    }

    private void OnCrucibleExamined(Entity<ForgeComponent> forge, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using var block = args.PushGroup(nameof(ForgeComponent));
        var comp = forge.Comp;

        if (_solutionContainer.TryGetSolution(forge.Owner, comp.Solution, out _, out var solution) && solution.Volume > FixedPoint2.Zero)
        {
            args.PushMarkup(Loc.GetString("au14-forge-examine-contents", ("volume", solution.Volume.Float())));
        }
        else
        {
            args.PushMarkup(Loc.GetString("au14-forge-examine-empty"));
        }
    }

    private void OnForgeableExamined(Entity<ForgeableComponent> forgeable, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using var block = args.PushGroup(nameof(ForgeableComponent));
        var comp = forgeable.Comp;

        args.PushMarkup(Loc.GetString("au14-forge-examine-temperature", ("temperature", (int) comp.Temperature)));

        if (comp.SeatedOn is not { } seat)
        {
            args.PushMarkup(Loc.GetString("au14-forge-examine-not-in-furnace"));
        }
        else if (HasComp<AnvilComponent>(seat))
        {
            args.PushMarkup(Loc.GetString("au14-forge-examine-on-anvil"));
        }
        else if (IsHeated(forgeable))
        {
            args.PushMarkup(Loc.GetString("au14-forge-examine-heating"));
        }
        else
        {
            args.PushMarkup(Loc.GetString("au14-forge-examine-cold-furnace"));
        }
    }

    private void OnForgeableEquippedHand(Entity<ForgeableComponent> forgeable, ref GotEquippedHandEvent args)
    {
        if (forgeable.Comp.Temperature < forgeable.Comp.BurnTemperature)
            return;

        var user = args.User;
        var damage = forgeable.Comp.BurnDamage;

        _inventory.TryGetInventoryEntity<DamageOnInteractProtectionComponent>(user, out var protectiveEntity);
        if (protectiveEntity.Comp != null)
            damage = DamageSpecifier.ApplyModifierSet(damage, protectiveEntity.Comp.DamageProtection);

        if (!damage.AnyPositive())
            return;

        var symmetry = args.Hand.Location == HandLocation.Left ? BodyPartSymmetry.Left : BodyPartSymmetry.Right;
        var key = new CMUMedicalBodyPartKey(BodyPartType.Hand, symmetry);

        if (!_bodyIndex.TryGetBodyPart(user, key, out var hand) ||
            !_bodyPartHealth.TryApplyPartDamage(user, hand, damage, origin: forgeable.Owner))
        {
            return;
        }

        _audio.PlayPvs(BurnSound, user);
        _popup.PopupEntity(Loc.GetString("au14-forge-burn-hand"), user, user, PopupType.MediumCaution);
    }

    private void OnMoldExamined(Entity<MoldComponent> mold, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using var block = args.PushGroup(nameof(MoldComponent));
        var comp = mold.Comp;

        if (comp.Cooling)
        {
            args.PushMarkup(Loc.GetString("au14-mold-examine-cooling"));
        }
        else if (comp.Ready)
        {
            args.PushMarkup(Loc.GetString("au14-mold-examine-ready"));
        }
        else if (_solutionContainer.TryGetSolution(mold.Owner, comp.Solution, out _, out var solution) && solution.Volume > FixedPoint2.Zero)
        {
            args.PushMarkup(Loc.GetString("au14-mold-examine-scrap"));
        }
        else
        {
            args.PushMarkup(Loc.GetString("au14-mold-examine-empty"));
        }
    }

    private bool IsHeated(Entity<ForgeableComponent> forgeable) =>
        forgeable.Comp.SeatedOn is { } seat &&
        !TerminatingOrDeleted(seat) &&
        TryComp<CampfireComponent>(seat, out var campfire) &&
        campfire.Lit;

    private EntityUid? GetSeatOccupant(EntityUid seat)
    {
        var query = EntityQueryEnumerator<ForgeableComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.SeatedOn == seat)
                return uid;
        }

        return null;
    }

    private string GetReagentName(ProtoId<ReagentPrototype> reagent) =>
        _reagent.TryIndex(reagent, out var proto) ? proto.LocalizedName : reagent.Id;
}
