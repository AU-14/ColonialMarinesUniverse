using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Administration.Logs;
using Content.Shared._RMC14.Campfire;
using Content.Shared._RMC14.Drink;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Yautja.BadBlood;

// The Lodge spit: whole prey corpses and loose prey meat are cooked on it into the only food a Bad Blood eats,
// the clan is paid for prey that was a player, and flasks are filled at it.
// Dependency fields taken by this file: every field starting with _spit.
public sealed partial class BadBloodCampSystem
{
    private static readonly ProtoId<TagPrototype> SpitXenoLarvaTag = "RMCXenoLarva";

    [Dependency] private SharedCampfireSystem _spitCampfire = default!;
    [Dependency] private InventorySystem _spitInventory = default!;
    [Dependency] private MobStateSystem _spitMobState = default!;
    [Dependency] private BadBloodProvenanceSystem _spitProvenance = default!;
    [Dependency] private IRobustRandom _spitRandom = default!;
    [Dependency] private SharedSolutionContainerSystem _spitSolutions = default!;
    [Dependency] private TagSystem _spitTags = default!;
    [Dependency] private YautjaTrophySystem _spitTrophies = default!;

    partial void InitializeSpit()
    {
        // CanDropTargetEvent is answered by the shared BadBloodCampCookSystem, the client needs it too
        SubscribeLocalEvent<BadBloodCampCookComponent, DragDropTargetEvent>(OnSpitDragDrop);
        SubscribeLocalEvent<BadBloodCampCookComponent, InteractUsingEvent>(OnSpitInteractUsing);
        SubscribeLocalEvent<BadBloodCampCookComponent, ActivateInWorldEvent>(OnSpitActivate);
        SubscribeLocalEvent<BadBloodCampCookComponent, BadBloodSpitCookCorpseDoAfterEvent>(OnSpitCookCorpseDoAfter);
        SubscribeLocalEvent<BadBloodCampCookComponent, BadBloodSpitCookMeatDoAfterEvent>(OnSpitCookMeatDoAfter);
    }

    // the spit goes with the Lodge. a corpse still turning on it is put back on the ground first
    partial void LodgeDestroyedSpit(Entity<BadBloodCampComponent> lodge, Entity<BadBloodCampRuleComponent> rule)
    {
        var query = EntityQueryEnumerator<BadBloodCampCookComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var cook, out var xform))
        {
            // nowhere to put it when the map itself is going
            if (TerminatingOrDeleted(uid) ||
                xform.MapUid is not { } map ||
                TerminatingOrDeleted(map) ||
                xform.GridUid is { } grid && TerminatingOrDeleted(grid))
            {
                continue;
            }

            EjectSpitBody((uid, cook));
        }
    }

    #region Whole corpses

    /// <summary>
    /// Whether <paramref name="body"/> can go on <paramref name="spit"/>: a dead human or xeno that is not a
    /// Yautja, synth, larva or parasite, over a burning fire. <paramref name="reason"/> is the locale id of the
    /// refusal. Prey that never had a player still cooks, it just pays nothing.
    /// </summary>
    public bool CanCookCorpse(Entity<BadBloodCampCookComponent> spit, EntityUid body, [NotNullWhen(false)] out string? reason)
    {
        reason = null;
        if (TerminatingOrDeleted(body) || !_spitMobState.IsDead(body))
        {
            reason = "cmu-bad-blood-spit-not-dead";
            return false;
        }

        if (HasComp<YautjaComponent>(body))
        {
            reason = "cmu-bad-blood-spit-refuse-yautja";
            return false;
        }

        if (HasComp<SynthComponent>(body))
        {
            reason = "cmu-bad-blood-spit-refuse-synth";
            return false;
        }

        if (HasComp<XenoParasiteComponent>(body) || _spitTags.HasTag(body, SpitXenoLarvaTag))
        {
            reason = "cmu-bad-blood-spit-refuse-larva";
            return false;
        }

        if (!_spitTrophies.IsXenoTrophyTarget(body) && !HasComp<HumanoidProfileComponent>(body))
        {
            reason = "cmu-bad-blood-spit-not-prey";
            return false;
        }

        if (!IsSpitLit(spit.Owner))
        {
            reason = "cmu-bad-blood-spit-unlit";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Puts <paramref name="body"/> on the spit and starts cooking it. Tells <paramref name="user"/> why when it is
    /// refused.
    /// </summary>
    public bool TryStartCookCorpse(Entity<BadBloodCampCookComponent> spit, EntityUid user, EntityUid body)
    {
        if (!_badBlood.IsBadBlood(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-spit-denied"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!CanCookCorpse(spit, body, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason, ("target", body)), user, user, PopupType.SmallCaution);
            return false;
        }

        var container = _offeringContainers.EnsureContainer<ContainerSlot>(spit.Owner, spit.Comp.BodyContainerId);
        if (container.ContainedEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-spit-busy"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!_offeringContainers.Insert(body, container))
            return false;

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            spit.Comp.CorpseCookTime,
            new BadBloodSpitCookCorpseDoAfterEvent(),
            spit.Owner,
            target: spit.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (!_offeringDoAfter.TryStartDoAfter(doAfter))
        {
            EjectSpitBody(spit);
            return false;
        }

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-spit-corpse-start", ("target", body)), user, user);
        return true;
    }

    /// <summary>
    /// Finishes cooking the corpse on <paramref name="spit"/>: the body becomes cooked cuts and the clan is paid
    /// once for it, if it was worthy prey. A corpse that can no longer be cooked is put back on the ground.
    /// </summary>
    public bool TryFinishCookCorpse(Entity<BadBloodCampCookComponent> spit, EntityUid user, EntityUid body)
    {
        if (!_offeringContainers.TryGetContainer(spit.Owner, spit.Comp.BodyContainerId, out var container) ||
            !container.Contains(body))
        {
            return false;
        }

        var xeno = _spitTrophies.IsXenoTrophyTarget(body);
        if (!CanCookCorpse(spit, body, out var reason) ||
            !_armoryProto.TryIndex(xeno ? spit.Comp.XenoCorpseRecipe : spit.Comp.HumanCorpseRecipe, out var recipe))
        {
            EjectSpitBody(spit);
            if (reason != null && !TerminatingOrDeleted(user))
                _popup.PopupEntity(Loc.GetString(reason, ("target", body)), user, user, PopupType.SmallCaution);

            return false;
        }

        var coords = Transform(spit.Owner).Coordinates;
        _offeringContainers.Remove(body, container, force: true, destination: coords);

        var min = xeno ? spit.Comp.XenoCorpseMinCuts : spit.Comp.HumanCorpseMinCuts;
        var max = xeno ? spit.Comp.XenoCorpseMaxCuts : spit.Comp.HumanCorpseMaxCuts;
        var cuts = _spitRandom.Next(min, Math.Max(min, max) + 1);
        for (var i = 0; i < cuts; i++)
        {
            Spawn(recipe.Cooked, coords);
        }

        // read before the body is gone. the ledger keeps the body's id after it is deleted
        var paid = FixedPoint4.Zero;
        if (_spitProvenance.IsWorthyPrey(body) && _rule.TryGetRule(out var rule))
            paid = _rule.TryCreditCook(body, rule.Comp.CorpseCookSp);

        _offeringAdminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} cooked the corpse {ToPrettyString(body):target} on the Bad Blood Lodge spit {ToPrettyString(spit.Owner):spit} into {cuts} cuts for {paid} SP");

        DropSpitBodyGear(body);
        QueueDel(body);

        if (!TerminatingOrDeleted(user))
            PopupSpitCooked(user, paid, "cmu-bad-blood-spit-corpse-finished", "cmu-bad-blood-spit-corpse-finished-unpaid");

        return true;
    }

    private void OnSpitDragDrop(Entity<BadBloodCampCookComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled || args.Dragged == args.User || !_spitMobState.IsDead(args.Dragged))
            return;

        args.Handled = true;
        TryStartCookCorpse(ent, args.User, args.Dragged);
    }

    private void OnSpitCookCorpseDoAfter(Entity<BadBloodCampCookComponent> ent, ref BadBloodSpitCookCorpseDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        // the do-after targets the spit, the corpse is whatever is turning on it
        if (args.Cancelled ||
            !_offeringContainers.TryGetContainer(ent.Owner, ent.Comp.BodyContainerId, out var container) ||
            container.ContainedEntities.Count == 0)
        {
            EjectSpitBody(ent);
            return;
        }

        TryFinishCookCorpse(ent, args.User, container.ContainedEntities[0]);
    }

    private void EjectSpitBody(Entity<BadBloodCampCookComponent> spit)
    {
        if (_offeringContainers.TryGetContainer(spit.Owner, spit.Comp.BodyContainerId, out var container))
            _offeringContainers.EmptyContainer(container, true, Transform(spit.Owner).Coordinates);
    }

    // what the prey wore and carried stays on the ground, the fire only takes the body
    private void DropSpitBodyGear(EntityUid body)
    {
        if (_spitInventory.TryGetContainerSlotEnumerator(body, out var slots))
        {
            while (slots.NextItem(out _, out var slot))
            {
                _spitInventory.TryUnequip(body, slot.Name, silent: true, force: true);
            }
        }

        foreach (var held in _hands.EnumerateHeld(body).ToArray())
        {
            _hands.TryDrop(body, held, checkActionBlocker: false);
        }
    }

    #endregion

    #region Loose meat

    /// <summary>
    /// The spit recipe that cooks <paramref name="meat"/>, matched on its prototype.
    /// </summary>
    public bool TryGetSpitRecipe(EntityUid meat, [NotNullWhen(true)] out CMUYautjaPreyCookRecipePrototype? recipe)
    {
        recipe = null;
        if (MetaData(meat).EntityPrototype?.ID is not { } id)
            return false;

        foreach (var candidate in _armoryProto.EnumeratePrototypes<CMUYautjaPreyCookRecipePrototype>())
        {
            if (candidate.Input.Id != id)
                continue;

            recipe = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether <paramref name="meat"/> can be cooked on <paramref name="spit"/>: a cut with a recipe that was not
    /// taken from a Yautja, synth, larva or parasite, over a burning fire. <paramref name="reason"/> is the locale
    /// id of the refusal.
    /// </summary>
    public bool CanCookMeat(Entity<BadBloodCampCookComponent> spit, EntityUid meat, [NotNullWhen(false)] out string? reason)
    {
        reason = null;
        if (TerminatingOrDeleted(meat) || !TryGetSpitRecipe(meat, out _))
        {
            reason = "cmu-bad-blood-spit-not-prey";
            return false;
        }

        if (TryComp(meat, out BadBloodMeatOriginComponent? origin))
        {
            if (origin.IsYautja)
            {
                reason = "cmu-bad-blood-spit-refuse-yautja";
                return false;
            }

            if (origin.IsSynth)
            {
                reason = "cmu-bad-blood-spit-refuse-synth";
                return false;
            }

            if (origin.IsLarva)
            {
                reason = "cmu-bad-blood-spit-refuse-larva";
                return false;
            }
        }

        if (!IsSpitLit(spit.Owner))
        {
            reason = "cmu-bad-blood-spit-unlit";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Starts cooking a loose cut of prey meat. Tells <paramref name="user"/> why when it is refused.
    /// </summary>
    public bool TryStartCookMeat(Entity<BadBloodCampCookComponent> spit, EntityUid user, EntityUid meat)
    {
        if (!_badBlood.IsBadBlood(user))
            return false;

        if (!CanCookMeat(spit, meat, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason, ("target", meat)), user, user, PopupType.SmallCaution);
            return false;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            spit.Comp.MeatCookTime,
            new BadBloodSpitCookMeatDoAfterEvent(),
            spit.Owner,
            target: spit.Owner,
            used: meat)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };

        if (!_offeringDoAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-spit-meat-start", ("target", meat)), user, user);
        return true;
    }

    /// <summary>
    /// Cooks <paramref name="meat"/> at once: it becomes the recipe's cooked cut, and the clan is paid for a cut
    /// taken from killed prey (not a Yautja, synth, larva or parasite), as far as that body's cook cap still allows.
    /// </summary>
    public bool TryCookMeat(Entity<BadBloodCampCookComponent> spit, EntityUid user, EntityUid meat)
    {
        if (!_badBlood.IsBadBlood(user) ||
            !CanCookMeat(spit, meat, out _) ||
            !TryGetSpitRecipe(meat, out var recipe))
        {
            return false;
        }

        var paid = FixedPoint4.Zero;
        if (TryComp(meat, out BadBloodMeatOriginComponent? origin) &&
            origin.WasKill &&
            !origin.IsYautja &&
            !origin.IsSynth &&
            !origin.IsLarva &&
            _rule.TryGetRule(out var rule))
        {
            paid = _rule.TryCreditCook(origin.Body, rule.Comp.MeatCookSp);
        }

        _offeringAdminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):player} cooked {ToPrettyString(meat):meat} on the Bad Blood Lodge spit {ToPrettyString(spit.Owner):spit} for {paid} SP");

        var coords = Transform(user).Coordinates;
        QueueDel(meat);
        for (var i = 0; i < recipe.Count; i++)
        {
            _hands.PickupOrDrop(user, Spawn(recipe.Cooked, coords));
        }

        PopupSpitCooked(user, paid, "cmu-bad-blood-spit-meat-finished", "cmu-bad-blood-spit-meat-finished-unpaid");
        return true;
    }

    private void OnSpitCookMeatDoAfter(Entity<BadBloodCampCookComponent> ent, ref BadBloodSpitCookMeatDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used is not { } meat)
            return;

        args.Handled = true;

        if (!CanCookMeat(ent, meat, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason, ("target", meat)), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        TryCookMeat(ent, args.User, meat);
    }

    #endregion

    #region The fire and the flask

    /// <summary>
    /// The spit's fire burns. A spit with no fire component always counts as lit.
    /// </summary>
    public bool IsSpitLit(EntityUid spit)
    {
        return !TryComp(spit, out CampfireComponent? fire) || fire.Lit;
    }

    /// <summary>
    /// Fills <paramref name="flask"/> with water at the spit.
    /// </summary>
    public bool TryRefillFlask(Entity<BadBloodCampCookComponent> spit, EntityUid user, EntityUid flask)
    {
        if (!_spitSolutions.TryGetRefillableSolution(flask, out var soln, out var solution))
            return false;

        if (solution.AvailableVolume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-spit-flask-full", ("flask", flask)), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!_spitSolutions.TryAddReagent(soln.Value, spit.Comp.RefillReagent, solution.AvailableVolume, out var added) ||
            added <= FixedPoint2.Zero)
        {
            return false;
        }

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-spit-flask-filled", ("flask", flask)), user, user);
        return true;
    }

    private void OnSpitInteractUsing(Entity<BadBloodCampCookComponent> ent, ref InteractUsingEvent args)
    {
        // anyone else, and anything the spit has no use for, is left to whatever the click would have done
        if (args.Handled || !_badBlood.IsBadBlood(args.User))
            return;

        if (TryGetSpitRecipe(args.Used, out _))
        {
            args.Handled = true;
            TryStartCookMeat(ent, args.User, args.Used);
            return;
        }

        if (HasComp<RMCFlaskComponent>(args.Used))
        {
            args.Handled = true;
            TryRefillFlask(ent, args.User, args.Used);
        }
    }

    // anyone can put the fire out, the campfire does that itself. a Bad Blood rekindles it with bare hands
    private void OnSpitActivate(Entity<BadBloodCampCookComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled ||
            !_badBlood.IsBadBlood(args.User) ||
            !TryComp(ent.Owner, out CampfireComponent? fire) ||
            fire.Lit)
        {
            return;
        }

        args.Handled = true;
        _spitCampfire.SetLit((ent.Owner, fire), true);
        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-spit-rekindled"), args.User, args.User);
    }

    private void PopupSpitCooked(EntityUid user, FixedPoint4 paid, string paidLoc, string unpaidLoc)
    {
        var message = paid > FixedPoint4.Zero
            ? Loc.GetString(paidLoc, ("sp", paid.ToString()))
            : Loc.GetString(unpaidLoc);

        _popup.PopupEntity(message, user, user);
    }

    #endregion
}
