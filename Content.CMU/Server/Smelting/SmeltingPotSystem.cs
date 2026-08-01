// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Linq;
using Content.Shared._AU14.Smelting;
using Content.Shared._RMC14.Campfire;
using Content.Shared._RMC14.Emote;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Construction.Components;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.Doors.Electronics;
using Content.Shared.Examine;
using Content.Shared.Mobs.Components;
using Content.Shared.Throwing;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Smelting;

/// <summary>
/// Runs the smelting pot: loading it, sitting it on a fire, converting a batch at a time while that fire is
/// lit, and handing the sheets back.
///
/// Progress is a plain machine timer, NOT a do-after: a do-after belongs to a user and dies the moment they
/// walk away or take a hit, which is wrong for a pot left on a campfire. Feedback is carried by the sprite,
/// the steam, the boiling loop and the examine text instead.
/// </summary>
public sealed class SmeltingPotSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedRMCEmoteSystem _emote = default!;

    /// <summary>Played once when a batch finishes.</summary>
    private static readonly SoundSpecifier BatchDoneSound = new SoundPathSpecifier("/Audio/Effects/sizzle.ogg");

    /// <summary>Steam puff spawned per unit consumed by a finished batch.</summary>
    private static readonly EntProtoId SteamEffect = "AU14SmeltingSteam";

    /// <summary>Acrid puff released when electronics are rendered down.</summary>
    private static readonly EntProtoId FumeEffect = "AU14SmeltingFumes";

    private static readonly ProtoId<EmotePrototype> CoughEmote = "Cough";

    // 🔧 TUNABLE: reach of the fume cloud in tiles, how many puffs draw it, and how fast they drift out.
    private const float FumeRadius = 1.5f;
    private const int FumePuffCount = 8;
    private const float FumeSpeed = 1.5f;

    /// <summary>🔧 TUNABLE: poison dealt to everyone caught in the cloud.</summary>
    private static readonly DamageSpecifier FumeDamage = new()
    {
        DamageDict = { ["Poison"] = 1 },
    };

    private readonly HashSet<EntityUid> _fumeTargets = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<SmeltingPotComponent, InteractUsingEvent>(OnPotInteractUsing);
        SubscribeLocalEvent<SmeltingPotComponent, InteractHandEvent>(OnPotInteractHand);
        SubscribeLocalEvent<SmeltingPotComponent, ExaminedEvent>(OnPotExamined);
        SubscribeLocalEvent<SmeltingPotComponent, GetVerbsEvent<AlternativeVerb>>(OnPotGetVerbs);
        SubscribeLocalEvent<SmeltingPotComponent, DestructionEventArgs>(OnPotDestroyed);

        // Sitting the pot on a fire is done by clicking the fire while holding it. This hooks the POT's
        // AfterInteract rather than the campfire's InteractUsing: SharedCampfireSystem already owns that
        // subscription (fuel and ignition) and Robust permits only one per component/event pair. The campfire
        // leaves anything that is not fuel or a lighter unhandled, so the interaction reaches us here.
        SubscribeLocalEvent<SmeltingPotComponent, AfterInteractEvent>(OnPotAfterInteract);
    }

    // ---- loading and unloading -------------------------------------------------------------------

    private void OnPotInteractUsing(Entity<SmeltingPotComponent> pot, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Scrap electronics are single entities with no stack type, so they take their own path.
        if (IsElectronics(args.Used) && pot.Comp.Recipes.Any(r => r.InputAnyElectronics))
        {
            args.Handled = true;
            InsertElectronics(pot, args.Used, args.User);
            return;
        }

        if (!TryComp<StackComponent>(args.Used, out var stack))
            return;

        args.Handled = true;

        if (FindRecipe(pot, stack.StackTypeId) == null)
        {
            _popup.PopupEntity(Loc.GetString("au14-smelting-pot-wrong-material"), pot, args.User);
            return;
        }

        // One material at a time: this is the rule that makes a recipe picker unnecessary, so it has to be
        // enforced rather than silently mixing.
        if (pot.Comp.Electronics)
        {
            _popup.PopupEntity(
                Loc.GetString("au14-smelting-pot-busy-with", ("material", Loc.GetString("au14-smelting-pot-material-electronics"))),
                pot,
                args.User);
            return;
        }

        if (pot.Comp.Material is { } loaded && loaded != stack.StackTypeId)
        {
            _popup.PopupEntity(
                Loc.GetString("au14-smelting-pot-busy-with", ("material", GetStackName(loaded))),
                pot,
                args.User);
            return;
        }

        var space = pot.Comp.Capacity - pot.Comp.Amount;
        if (space <= 0)
        {
            _popup.PopupEntity(Loc.GetString("au14-smelting-pot-full"), pot, args.User);
            return;
        }

        var taken = Math.Min(space, _stack.GetCount(args.Used, stack));
        if (taken <= 0)
            return;

        _stack.SetCount(args.Used, _stack.GetCount(args.Used, stack) - taken, stack);
        pot.Comp.Material = stack.StackTypeId;
        pot.Comp.Amount += taken;
        Dirty(pot);

        _popup.PopupEntity(
            Loc.GetString("au14-smelting-pot-inserted", ("count", taken), ("material", GetStackName(stack.StackTypeId))),
            pot,
            args.User);

        TryStartBatch(pot);
    }

    /// <summary>Same rule the sapper workbench uses, so "electronics" means one thing across the fork.</summary>
    private bool IsElectronics(EntityUid uid) =>
        HasComp<MachineBoardComponent>(uid) ||
        HasComp<ComputerBoardComponent>(uid) ||
        HasComp<DoorElectronicsComponent>(uid);

    private void InsertElectronics(Entity<SmeltingPotComponent> pot, EntityUid used, EntityUid user)
    {
        if (pot.Comp.Material != null)
        {
            _popup.PopupEntity(
                Loc.GetString("au14-smelting-pot-busy-with", ("material", GetStackName(pot.Comp.Material.Value))),
                pot,
                user);
            return;
        }

        if (pot.Comp.Amount >= pot.Comp.Capacity)
        {
            _popup.PopupEntity(Loc.GetString("au14-smelting-pot-full"), pot, user);
            return;
        }

        // Boards are consumed whole - there is nothing to split, so the entity goes.
        QueueDel(used);
        pot.Comp.Electronics = true;
        pot.Comp.Amount++;
        Dirty(pot);

        _popup.PopupEntity(
            Loc.GetString("au14-smelting-pot-inserted", ("count", 1), ("material", Loc.GetString("au14-smelting-pot-material-electronics"))),
            pot,
            user);

        TryStartBatch(pot);
    }

    /// <summary>Empty hand collects finished sheets. Taking the pot itself is the alt-verb, so an accidental
    /// click never walks off with someone's ore.</summary>
    private void OnPotInteractHand(Entity<SmeltingPotComponent> pot, ref InteractHandEvent args)
    {
        if (args.Handled || pot.Comp.Fire == null)
            return;

        args.Handled = true;

        if (!TryCollectOutput(pot, args.User))
            _popup.PopupEntity(Loc.GetString("au14-smelting-pot-nothing-ready"), pot, args.User);
    }

    private bool TryCollectOutput(Entity<SmeltingPotComponent> pot, EntityUid user)
    {
        if (pot.Comp.OutputMaterial is not { } output || pot.Comp.OutputAmount <= 0)
            return false;

        SpawnStacks(output, pot.Comp.OutputAmount, Transform(pot).Coordinates, user);

        _popup.PopupEntity(
            Loc.GetString("au14-smelting-pot-collected", ("count", pot.Comp.OutputAmount), ("material", GetStackName(output))),
            pot,
            user);

        pot.Comp.OutputMaterial = null;
        pot.Comp.OutputAmount = 0;
        Dirty(pot);

        // Collecting may have unblocked a stalled pot.
        TryStartBatch(pot);
        return true;
    }

    /// <summary>Sheets can exceed a stack's MaxCount, so a big run comes out as several stacks rather than one
    /// silently truncated one.</summary>
    private void SpawnStacks(ProtoId<StackPrototype> stackType, int amount, EntityCoordinates coords, EntityUid? user)
    {
        if (!_prototype.TryIndex(stackType, out var proto) || string.IsNullOrEmpty(proto.Spawn.Id))
            return;

        var spawnProto = proto.Spawn;

        var max = proto.MaxCount ?? int.MaxValue;
        var first = true;

        while (amount > 0)
        {
            var chunk = Math.Min(max, amount);
            amount -= chunk;

            var ent = Spawn(spawnProto, coords);
            if (TryComp<StackComponent>(ent, out var stack))
                _stack.SetCount(ent, chunk, stack);

            // Put the first stack straight into the collector's hands; the rest land at the pot's feet.
            if (first && user != null)
                _hands.TryPickupAnyHand(user.Value, ent);

            first = false;
        }
    }

    // ---- attaching to a fire ---------------------------------------------------------------------

    private void OnPotAfterInteract(Entity<SmeltingPotComponent> pot, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!HasComp<CampfireComponent>(target))
            return;

        args.Handled = true;

        // Already on a fire (somehow) - nothing to do.
        if (pot.Comp.Fire != null)
            return;

        // One pot per fire, or two pots would fight over the same tile and the same fire.
        if (GetPotOn(target) != null)
        {
            _popup.PopupEntity(Loc.GetString("au14-smelting-pot-fire-occupied"), target, args.User);
            return;
        }

        var fire = target;
        _hands.TryDrop(args.User, pot.Owner);

        var coords = Transform(fire).Coordinates;
        _transform.SetCoordinates(pot, coords);
        _transform.AnchorEntity(pot);

        pot.Comp.Fire = fire;
        Dirty(pot);

        _popup.PopupEntity(Loc.GetString("au14-smelting-pot-attached"), fire, args.User);
        TryStartBatch(pot);
    }

    private void OnPotGetVerbs(Entity<SmeltingPotComponent> pot, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || pot.Comp.Fire == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("au14-smelting-pot-verb-detach"),
            Act = () => Detach(pot, user),
        });

        if (pot.Comp.Amount > 0)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("au14-smelting-pot-verb-empty"),
                Act = () => EmptyInput(pot, user),
            });
        }
    }

    /// <summary>Lifts the pot off the fire. Contents ride along - the pot is the container, not the fire.</summary>
    private void Detach(Entity<SmeltingPotComponent> pot, EntityUid user)
    {
        PauseBatch(pot);
        pot.Comp.Fire = null;
        Dirty(pot);

        _transform.Unanchor(pot);
        _hands.TryPickupAnyHand(user, pot);
        _popup.PopupEntity(Loc.GetString("au14-smelting-pot-detached"), pot, user);
    }

    /// <summary>Tips the unsmelted input back out, so a wrong load is recoverable without waiting it out.</summary>
    private void EmptyInput(Entity<SmeltingPotComponent> pot, EntityUid user)
    {
        if (pot.Comp.Amount <= 0)
            return;

        // Boards were consumed on insertion (there is no half a circuit board), so only stack materials can
        // be poured back out. Electronics are simply discarded, which the popup says plainly.
        if (pot.Comp.Material is { } material)
        {
            SpawnStacks(material, pot.Comp.Amount, Transform(pot).Coordinates, user);
            _popup.PopupEntity(
                Loc.GetString("au14-smelting-pot-emptied", ("count", pot.Comp.Amount), ("material", GetStackName(material))),
                pot,
                user);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("au14-smelting-pot-emptied-scrap"), pot, user);
        }

        pot.Comp.Material = null;
        pot.Comp.Electronics = false;
        pot.Comp.Amount = 0;
        PauseBatch(pot);
        SetActive(pot, false);
        Dirty(pot);
    }

    private void OnPotDestroyed(Entity<SmeltingPotComponent> pot, ref DestructionEventArgs args)
    {
        var coords = Transform(pot).Coordinates;

        // Electronics have already been consumed into the melt, so only stack input survives the wreck.
        if (pot.Comp.Material is { } material && pot.Comp.Amount > 0)
            SpawnStacks(material, pot.Comp.Amount, coords, null);

        if (pot.Comp.OutputMaterial is { } output && pot.Comp.OutputAmount > 0)
            SpawnStacks(output, pot.Comp.OutputAmount, coords, null);
    }

    // ---- smelting --------------------------------------------------------------------------------

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SmeltingPotComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var pot = new Entity<SmeltingPotComponent>(uid, comp);

            if (!IsOnLitFire(pot))
            {
                // Fire died or the pot came off: hold the remaining time so relighting resumes rather than restarts.
                if (comp.Active)
                {
                    PauseBatch(pot);
                    SetActive(pot, false);
                }

                continue;
            }

            if (comp.BatchEndsAt is not { } endsAt)
            {
                TryStartBatch(pot);
                continue;
            }

            if (now < endsAt)
                continue;

            CompleteBatch(pot);
        }
    }

    private void TryStartBatch(Entity<SmeltingPotComponent> pot)
    {
        if (pot.Comp.BatchEndsAt != null || !IsOnLitFire(pot))
            return;

        if (FindReadyRecipe(pot) is not { } recipe)
        {
            SetActive(pot, false);
            return;
        }

        // Resume a batch the fire interrupted, otherwise start a fresh one.
        var duration = pot.Comp.BatchRemaining ?? recipe.Duration;
        pot.Comp.BatchRemaining = null;
        pot.Comp.BatchEndsAt = _timing.CurTime + duration;
        SetActive(pot, true);
        Dirty(pot);
    }

    private void PauseBatch(Entity<SmeltingPotComponent> pot)
    {
        if (pot.Comp.BatchEndsAt is not { } endsAt)
            return;

        var left = endsAt - _timing.CurTime;
        pot.Comp.BatchRemaining = left > TimeSpan.Zero ? left : TimeSpan.Zero;
        pot.Comp.BatchEndsAt = null;
        Dirty(pot);
    }

    private void CompleteBatch(Entity<SmeltingPotComponent> pot)
    {
        pot.Comp.BatchEndsAt = null;

        if (FindReadyRecipe(pot) is not { } recipe)
        {
            SetActive(pot, false);
            Dirty(pot);
            return;
        }

        pot.Comp.Amount -= recipe.InputAmount;
        if (pot.Comp.Amount <= 0)
        {
            pot.Comp.Amount = 0;
            pot.Comp.Material = null;
            pot.Comp.Electronics = false;
        }

        pot.Comp.OutputMaterial = recipe.Output;
        pot.Comp.OutputAmount += recipe.OutputAmount;
        Dirty(pot);

        // One steam puff per unit consumed, so the pips that just vanished visibly hiss away.
        var coords = Transform(pot).Coordinates;
        for (var i = 0; i < recipe.InputAmount; i++)
            Spawn(SteamEffect, coords);

        // Rendering circuit boards down cooks off their polymers: a ring of acrid yellow fumes rolls out and
        // burns anyone standing too close.
        if (recipe.InputAnyElectronics)
            ReleaseToxicFumes(pot);

        _audio.PlayPvs(BatchDoneSound, pot);

        TryStartBatch(pot);
    }

    /// <summary>
    /// Puffs acrid fumes out of the pot in every direction and poisons whoever they wash over.
    ///
    /// The damage is applied as ONE radius check rather than by letting the drifting puff entities collide
    /// with people: a handful of tiny fast-moving effects colliding reliably is exactly the sort of thing
    /// physics does not guarantee, and a toxic cloud that sometimes does nothing is worse than no cloud. The
    /// spawned puffs are therefore purely the visual, and the radius check is the mechanic.
    /// </summary>
    private void ReleaseToxicFumes(Entity<SmeltingPotComponent> pot)
    {
        var coords = Transform(pot).Coordinates;

        // Visual: puffs pushed outward evenly so the cloud reads as rolling off the pot on all sides.
        for (var i = 0; i < FumePuffCount; i++)
        {
            var angle = Angle.FromDegrees(360.0 / FumePuffCount * i);
            var puff = Spawn(FumeEffect, coords);
            _throwing.TryThrow(puff, angle.ToVec() * FumeRadius, FumeSpeed);
        }

        // Mechanic: anyone inside the cloud takes a lungful.
        _fumeTargets.Clear();
        _lookup.GetEntitiesInRange(coords, FumeRadius, _fumeTargets);

        foreach (var target in _fumeTargets)
        {
            // Only things that breathe: no point poisoning crates, and the pot must not gas itself.
            if (target == pot.Owner || !HasComp<MobStateComponent>(target))
                continue;

            _damageable.TryChangeDamage(target, FumeDamage, ignoreResistances: false, origin: pot.Owner);
            _emote.TryEmoteWithChat(target, CoughEmote, forceEmote: true, cooldown: TimeSpan.Zero);
        }
    }

    /// <summary>The recipe this pot can run right now: correct material, enough of it, and room for the result.</summary>
    private SmeltingRecipe? FindReadyRecipe(Entity<SmeltingPotComponent> pot)
    {
        SmeltingRecipe? recipe;
        if (pot.Comp.Electronics)
            recipe = pot.Comp.Recipes.FirstOrDefault(r => r.InputAnyElectronics);
        else if (pot.Comp.Material is { } material)
            recipe = FindRecipe(pot, material);
        else
            return null;

        if (recipe == null)
            return null;

        if (pot.Comp.Amount < recipe.InputAmount)
            return null;

        // Mixing outputs would lose the first kind, so a pot holding steel will not start a plasteel batch
        // until the steel is collected.
        if (pot.Comp.OutputMaterial is { } output && output != recipe.Output)
            return null;

        if (pot.Comp.OutputAmount + recipe.OutputAmount > pot.Comp.OutputCapacity)
            return null;

        return recipe;
    }

    private SmeltingRecipe? FindRecipe(Entity<SmeltingPotComponent> pot, ProtoId<StackPrototype> input) =>
        pot.Comp.Recipes.FirstOrDefault(r => !r.InputAnyElectronics && r.Input is { } recipeInput && recipeInput == input);

    private bool IsOnLitFire(Entity<SmeltingPotComponent> pot) =>
        pot.Comp.Fire is { } fire &&
        !TerminatingOrDeleted(fire) &&
        TryComp<CampfireComponent>(fire, out var campfire) &&
        campfire.Lit;

    private void SetActive(Entity<SmeltingPotComponent> pot, bool active)
    {
        if (pot.Comp.Active == active)
            return;

        pot.Comp.Active = active;
        _appearance.SetData(pot, SmeltingPotVisuals.Active, active);
        Dirty(pot);
    }

    private EntityUid? GetPotOn(EntityUid fire)
    {
        var query = EntityQueryEnumerator<SmeltingPotComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Fire == fire)
                return uid;
        }

        return null;
    }

    private string GetStackName(ProtoId<StackPrototype> stackType) =>
        _prototype.TryIndex(stackType, out var proto) ? Loc.GetString(proto.Name) : stackType.Id;

    // ---- examine ---------------------------------------------------------------------------------

    /// <summary>Examine carries the whole state, since there is no window and no progress bar.</summary>
    private void OnPotExamined(Entity<SmeltingPotComponent> pot, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using var block = args.PushGroup(nameof(SmeltingPotComponent));

        if (pot.Comp.Amount > 0)
        {
            var loadedName = pot.Comp.Material is { } material
                ? GetStackName(material)
                : Loc.GetString("au14-smelting-pot-material-electronics");

            args.PushMarkup(Loc.GetString("au14-smelting-pot-examine-contents",
                ("count", pot.Comp.Amount),
                ("max", pot.Comp.Capacity),
                ("material", loadedName)));
        }
        else
        {
            args.PushMarkup(Loc.GetString("au14-smelting-pot-examine-empty"));
        }

        if (pot.Comp.OutputMaterial is { } output && pot.Comp.OutputAmount > 0)
        {
            args.PushMarkup(Loc.GetString("au14-smelting-pot-examine-output",
                ("count", pot.Comp.OutputAmount),
                ("material", GetStackName(output))));
        }

        if (pot.Comp.Active && pot.Comp.BatchEndsAt is { } endsAt)
        {
            var recipe = FindReadyRecipe(pot);
            var total = recipe?.Duration ?? TimeSpan.FromSeconds(1);
            var left = endsAt - _timing.CurTime;
            var percent = (int) Math.Clamp((1 - left.TotalSeconds / total.TotalSeconds) * 100, 0, 100);
            args.PushMarkup(Loc.GetString("au14-smelting-pot-examine-progress", ("percent", percent)));
        }
        else if (pot.Comp.Fire == null)
        {
            args.PushMarkup(Loc.GetString("au14-smelting-pot-examine-not-on-fire"));
        }
        else if (pot.Comp.Amount > 0)
        {
            args.PushMarkup(Loc.GetString("au14-smelting-pot-examine-fire-out"));
        }
    }
}
