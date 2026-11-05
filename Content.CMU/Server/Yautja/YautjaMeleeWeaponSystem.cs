using System.Linq;
using Content.Server.Humanoid;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Synth;
using Content.Shared.Inventory;
using Content.Shared.Stacks;
using Content.Shared.CMU14.Fishing.Components;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.EntityTable;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Yautja;

public sealed partial class YautjaMeleeWeaponSystem : EntitySystem
{
    [Dependency] private EntityTableSystem _entityTable = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodyPartHealthSystem _bodyPartHealth = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private HumanoidOrganAppearanceSystem _organAppearance = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private RMCUnrevivableSystem _unrevivable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StatusEffectQuerySystem _status = default!;
    [Dependency] private YautjaTrophySystem _trophies = default!;
    [Dependency] private YautjaMachineSystem _machines = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaHunterSpearFishingComponent, AfterInteractEvent>(OnHunterSpearAfterInteract);
        SubscribeLocalEvent<YautjaHunterSpearFishingComponent, YautjaHunterSpearFishingDoAfterEvent>(OnHunterSpearFishingDoAfter);
        SubscribeLocalEvent<YautjaCeremonialDaggerComponent, AfterInteractEvent>(OnCeremonialDaggerAfterInteract);
        SubscribeLocalEvent<YautjaCeremonialDaggerComponent, YautjaCeremonialDaggerPrepareFlayDoAfterEvent>(OnCeremonialDaggerPrepareFlayDoAfter);
        SubscribeLocalEvent<YautjaCeremonialDaggerComponent, YautjaCeremonialDaggerFlayDoAfterEvent>(OnCeremonialDaggerFlayDoAfter);
        SubscribeLocalEvent<YautjaCeremonialDaggerComponent, YautjaCeremonialDaggerLimbFlayDoAfterEvent>(OnCeremonialDaggerLimbFlayDoAfter);
    }

    private void OnHunterSpearAfterInteract(Entity<YautjaHunterSpearFishingComponent> spear, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        args.Handled = TryStartHunterSpearFishing(spear, args.User, target, args.CanReach);
    }

    private bool TryStartHunterSpearFishing(
        Entity<YautjaHunterSpearFishingComponent> spear,
        EntityUid user,
        EntityUid target,
        bool canReach)
    {
        if (spear.Comp.BusyFishing ||
            !canReach ||
            !HasComp<FishingSpotComponent>(target) ||
            _hands.GetActiveItem(user) != spear.Owner)
        {
            return false;
        }

        spear.Comp.BusyFishing = true;

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            spear.Comp.DoAfter,
            new YautjaHunterSpearFishingDoAfterEvent(),
            spear.Owner,
            target: target,
            used: spear.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
            DistanceThreshold = 1.5f,
            ForceVisible = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            spear.Comp.BusyFishing = false;
            return false;
        }

        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-hunter-spear-fishing-start-others", ("user", user), ("spear", spear.Owner)),
            user,
            Filter.PvsExcept(user),
            true);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-hunter-spear-fishing-start-self"), user, user);
        return true;
    }

    private void OnHunterSpearFishingDoAfter(
        Entity<YautjaHunterSpearFishingComponent> spear,
        ref YautjaHunterSpearFishingDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        spear.Comp.BusyFishing = false;

        if (args.Cancelled ||
            args.Target is not { } target ||
            !TryComp(target, out FishingSpotComponent? spot))
        {
            return;
        }

        if (_random.Prob(spear.Comp.FailureChance))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hunter-spear-fishing-fail"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        var caughtPrototype = _entityTable.GetSpawns(spot.FishList).First();
        var caught = Spawn(caughtPrototype, Transform(target).Coordinates);
        _hands.PickupOrDrop(args.User, caught, checkActionBlocker: false);

        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-hunter-spear-fishing-caught-others", ("user", args.User), ("spear", spear.Owner), ("catch", caught)),
            args.User,
            Filter.PvsExcept(args.User),
            true);
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-hunter-spear-fishing-caught-self", ("spear", spear.Owner), ("catch", caught)),
            args.User,
            args.User);
    }

    // a plain click on the corpse, same as flaying a severed limb or head. cmss13 does it from attack(),
    // which is just a click there, so it shouldn't need combat mode here
    private void OnCeremonialDaggerAfterInteract(Entity<YautjaCeremonialDaggerComponent> dagger, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        if (TryStartCeremonialDaggerBodyFlay(dagger, args.User, target, args.CanReach))
        {
            args.Handled = true;
            return;
        }

        args.Handled = TryStartCeremonialDaggerLimbFlay(dagger, args.User, target, args.CanReach);
    }

    private bool TryStartCeremonialDaggerBodyFlay(
        Entity<YautjaCeremonialDaggerComponent> dagger,
        EntityUid user,
        EntityUid target,
        bool canReach)
    {
        if (!canReach ||
            !_mobState.IsDead(target) ||
            _hands.GetActiveItem(user) != dagger.Owner)
        {
            return false;
        }

        if (!HasComp<HumanoidProfileComponent>(target) || HasComp<XenoComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-ceremonial-dagger-flay-non-human"), user, user);
            return true;
        }

        if (!CanFlay(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-ceremonial-dagger-flay-not-strong"), user, user);
            return true;
        }

        if (HasComp<YautjaComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-ceremonial-dagger-flay-yautja"), user, user, PopupType.LargeCaution);
            return true;
        }

        if (HasComp<SynthComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-ceremonial-dagger-flay-synth"), user, user);
            return true;
        }

        if (TryComp(target, out YautjaFlayedComponent? flayed))
        {
            // fully flayed, nothing left to take
            if (flayed.NextStage == YautjaFlayingStage.Complete)
                return false;

            TryResumeCeremonialDaggerFlay(dagger, user, (target, flayed));
            return true;
        }

        TryStartCeremonialDaggerPrepareFlay(dagger, user, target);
        return true;
    }

    private bool TryStartCeremonialDaggerLimbFlay(
        Entity<YautjaCeremonialDaggerComponent> dagger,
        EntityUid user,
        EntityUid target,
        bool canReach)
    {
        if (!canReach ||
            !HasComp<YautjaComponent>(user) && !HasComp<YautjaTechAuthorizedComponent>(user) ||
            !_machines.TryResolveSeveredLimb(target, out _) ||
            _hands.GetActiveItem(user) != dagger.Owner)
        {
            return false;
        }

        if (HasComp<YautjaFlayedComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-ceremonial-dagger-limb-already-flayed"), user, user);
            return true;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            TimeSpan.FromSeconds(2),
            new YautjaCeremonialDaggerLimbFlayDoAfterEvent(),
            dagger.Owner,
            target: target,
            used: dagger.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            BreakOnHandChange = false,
            BlockDuplicate = true,
            CancelDuplicate = false,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
            DistanceThreshold = 1.5f,
            ForceVisible = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _audio.PlayPvs(dagger.Comp.StartFlaySound, target);
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-ceremonial-dagger-limb-flay-start", ("limb", target)),
            user,
            user,
            PopupType.MediumCaution);
        return true;
    }

    private bool TryStartCeremonialDaggerPrepareFlay(
        Entity<YautjaCeremonialDaggerComponent> dagger,
        EntityUid user,
        EntityUid target)
    {
        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            dagger.Comp.PrepareDelay,
            new YautjaCeremonialDaggerPrepareFlayDoAfterEvent(),
            dagger.Owner,
            target: target,
            used: dagger.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            BreakOnHandChange = false,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
            DistanceThreshold = 1.5f,
            ForceVisible = true,
        };

        return _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnCeremonialDaggerPrepareFlayDoAfter(
        Entity<YautjaCeremonialDaggerComponent> dagger,
        ref YautjaCeremonialDaggerPrepareFlayDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (args.Cancelled ||
            args.Target is not { } target ||
            Deleted(target) ||
            !_mobState.IsDead(target) ||
            !CanFlay(args.User))
        {
            return;
        }

        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-ceremonial-dagger-flay-start-others", ("user", args.User), ("victim", target), ("dagger", dagger.Owner)),
            args.User,
            Filter.PvsExcept(args.User),
            true,
            PopupType.MediumCaution);
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-ceremonial-dagger-flay-start-self", ("victim", target), ("dagger", dagger.Owner)),
            args.User,
            args.User,
            PopupType.MediumCaution);
        _audio.PlayPvs(dagger.Comp.StartFlaySound, target);
        StartCeremonialDaggerFlay(dagger, args.User, target);
    }

    // cmss13 thralls flay too, they get TRAIT_SUPER_STRONG from the link
    private bool CanFlay(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) || HasComp<YautjaThrallHeroComponent>(user);
    }

    private void TryResumeCeremonialDaggerFlay(
        Entity<YautjaCeremonialDaggerComponent> dagger,
        EntityUid user,
        Entity<YautjaFlayedComponent> target)
    {
        if (target.Comp.CurrentFlayer is { } current && current != user && !TerminatingOrDeleted(current))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-ceremonial-dagger-flay-busy", ("victim", target.Owner), ("flayer", current)),
                user,
                user);
            return;
        }

        if (!StartCeremonialDaggerFlay(dagger, user, target))
            return;

        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-ceremonial-dagger-flay-resume-others", ("user", user), ("victim", target.Owner), ("dagger", dagger.Owner)),
            user,
            Filter.PvsExcept(user),
            true,
            PopupType.MediumCaution);
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-ceremonial-dagger-flay-resume-self", ("victim", target.Owner), ("dagger", dagger.Owner)),
            user,
            user,
            PopupType.MediumCaution);
        _audio.PlayPvs(dagger.Comp.StartFlaySound, target);
    }

    private bool StartCeremonialDaggerFlay(
        Entity<YautjaCeremonialDaggerComponent> dagger,
        EntityUid user,
        EntityUid target)
    {
        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            dagger.Comp.FlayDelay,
            new YautjaCeremonialDaggerFlayDoAfterEvent(),
            dagger.Owner,
            target: target,
            used: dagger.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            BreakOnHandChange = false,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
            DistanceThreshold = 1.5f,
            ForceVisible = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        if (TryComp(target, out YautjaFlayedComponent? flayed))
        {
            flayed.CurrentFlayer = user;
            Dirty(target, flayed);
        }

        return true;
    }

    // one doafter per stage and it repeats until the body is done, the same loop cmss13 runs
    private void OnCeremonialDaggerFlayDoAfter(
        Entity<YautjaCeremonialDaggerComponent> dagger,
        ref YautjaCeremonialDaggerFlayDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (args.Target is not { } target || TerminatingOrDeleted(target))
            return;

        TryComp(target, out YautjaFlayedComponent? flayed);
        if (args.Cancelled || !_mobState.IsDead(target) || !CanFlay(args.User))
        {
            if (flayed != null && flayed.CurrentFlayer == args.User)
            {
                flayed.CurrentFlayer = null;
                Dirty(target, flayed);
            }

            if (args.Cancelled)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-ceremonial-dagger-flay-interrupted"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (flayed == null)
        {
            flayed = FinishCeremonialDaggerFirstFlayPass(dagger, args.User, target);
        }
        else
        {
            switch (flayed.NextStage)
            {
                case YautjaFlayingStage.Scalp:
                    FinishCeremonialDaggerScalpStage(dagger, args.User, (target, flayed));
                    break;
                case YautjaFlayingStage.Strip:
                    FinishCeremonialDaggerStripStage(dagger, args.User, (target, flayed));
                    break;
                case YautjaFlayingStage.Skin:
                    FinishCeremonialDaggerSkinStage(dagger, args.User, (target, flayed));
                    break;
                default:
                    return;
            }
        }

        flayed.CurrentFlayer = flayed.NextStage == YautjaFlayingStage.Complete ? null : args.User;
        Dirty(target, flayed);
        args.Repeat = flayed.NextStage != YautjaFlayingStage.Complete;
    }

    private void OnCeremonialDaggerLimbFlayDoAfter(
        Entity<YautjaCeremonialDaggerComponent> dagger,
        ref YautjaCeremonialDaggerLimbFlayDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (args.Target is not { } target ||
            Deleted(target) ||
            !_machines.TryResolveSeveredLimb(target, out _) ||
            !HasComp<YautjaComponent>(args.User) && !HasComp<YautjaTechAuthorizedComponent>(args.User))
        {
            return;
        }

        if (args.Cancelled)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-ceremonial-dagger-limb-flay-cancel", ("limb", target)),
                args.User,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        EnsureComp<YautjaFlayedComponent>(target);
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-ceremonial-dagger-limb-flay-finish", ("limb", target)),
            args.User,
            args.User,
            PopupType.MediumCaution);
    }

    private YautjaFlayedComponent FinishCeremonialDaggerFirstFlayPass(
        Entity<YautjaCeremonialDaggerComponent> dagger,
        EntityUid user,
        EntityUid target)
    {
        PopupFlayStage(user, target, "first-pass");
        _audio.PlayPvs(dagger.Comp.FirstPassSound, target);

        var flayed = EnsureComp<YautjaFlayedComponent>(target);
        flayed.Stage = 1;
        flayed.NextStage = YautjaFlayingStage.Scalp;

        ApplyFlayDamage(target, dagger.Owner, dagger.Comp.FirstPassDamage);
        SpawnFlayLeftovers(dagger, target, meat: true, hide: 0);
        return flayed;
    }

    private void FinishCeremonialDaggerScalpStage(
        Entity<YautjaCeremonialDaggerComponent> dagger,
        EntityUid user,
        Entity<YautjaFlayedComponent> target)
    {
        target.Comp.NextStage = YautjaFlayingStage.Strip;
        _audio.PlayPvs(dagger.Comp.ScalpSound, target);

        if (!TryGetFlayHead(target, out _))
        {
            ApplyFlayDamage(target, dagger.Owner, dagger.Comp.ScalpDamage, BodyPartType.Torso);
            PopupFlayStage(user, target, "scalp-headless");
            return;
        }

        ApplyFlayDamage(target, dagger.Owner, dagger.Comp.ScalpDamage, BodyPartType.Head);
        SpawnFlayLeftovers(dagger, target, meat: false, hide: dagger.Comp.ScalpHide);

        // you can't scalp someone with no hair
        if (!_organAppearance.TryGetMarkings(target, HumanoidVisualLayers.Hair, out var organ, out _, out var hair) ||
            hair.Count == 0)
        {
            PopupFlayStage(user, target, "scalp-bald");
            return;
        }

        PopupFlayStage(user, target, "scalp");
        var scalp = _trophies.SpawnRuntimeScalp(target, user);
        _hands.PickupOrDrop(user, scalp, checkActionBlocker: false);

        // the hair comes off with the scalp
        _organAppearance.SetMarkings(target, organ, HumanoidVisualLayers.Hair, []);
    }

    private void FinishCeremonialDaggerStripStage(
        Entity<YautjaCeremonialDaggerComponent> dagger,
        EntityUid user,
        Entity<YautjaFlayedComponent> target)
    {
        target.Comp.Stage = 2;
        target.Comp.NextStage = YautjaFlayingStage.Skin;
        PopupFlayStage(user, target, "strip");
        _audio.PlayPvs(dagger.Comp.StripSound, target);

        ApplyFlayDamage(target, dagger.Owner, dagger.Comp.StripDamage);
        SpawnFlayLeftovers(dagger, target, meat: false, hide: dagger.Comp.StripHide);

        // uniform comes off with the skin, so the stripped body actually shows
        _inventory.TryUnequip(target, target, "jumpsuit", silent: true, force: true);
        if (_organAppearance.TryGetMarkings(target, HumanoidVisualLayers.FacialHair, out var organ, out _, out var beard) &&
            beard.Count > 0)
        {
            _organAppearance.SetMarkings(target, organ, HumanoidVisualLayers.FacialHair, []);
        }
    }

    private void FinishCeremonialDaggerSkinStage(
        Entity<YautjaCeremonialDaggerComponent> dagger,
        EntityUid user,
        Entity<YautjaFlayedComponent> target)
    {
        target.Comp.Stage = 3;
        target.Comp.NextStage = YautjaFlayingStage.Complete;
        PopupFlayStage(user, target, "skin");
        _audio.PlayPvs(dagger.Comp.SkinSound, target);

        ApplyFlayDamage(target, dagger.Owner, dagger.Comp.SkinDamage);
        SpawnFlayLeftovers(dagger, target, meat: true, hide: dagger.Comp.SkinHide);

        _hands.DropAll(target.Owner, checkActionBlocker: false, doDropInteraction: false);
        var slots = _inventory.GetSlotEnumerator(target.Owner);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity != null)
                _inventory.TryUnequip(target, target, slot.ID, silent: true, force: true);
        }

        // cmss13 PERMANENTLY_DEAD, nobody's coming back from this
        _unrevivable.MakeUnrevivable(target.Owner);
        _meta.SetEntityName(target, Loc.GetString("cmu-yautja-ceremonial-dagger-flayed-name"));
    }

    private void PopupFlayStage(EntityUid user, EntityUid target, string stage)
    {
        _popup.PopupEntity(
            Loc.GetString($"cmu-yautja-ceremonial-dagger-flay-{stage}-others", ("user", user), ("victim", target)),
            user,
            Filter.PvsExcept(user),
            true,
            PopupType.MediumCaution);
        _popup.PopupEntity(
            Loc.GetString($"cmu-yautja-ceremonial-dagger-flay-{stage}-self", ("victim", target)),
            user,
            user,
            PopupType.MediumCaution);
    }

    private bool TryGetFlayHead(EntityUid target, out EntityUid head)
    {
        foreach (var (partUid, part) in _body.GetBodyChildren(target))
        {
            if (part.PartType != BodyPartType.Head)
                continue;

            head = partUid;
            return true;
        }

        head = default;
        return false;
    }

    // null part = every part, same as the first pass always did
    private void ApplyFlayDamage(EntityUid target, EntityUid dagger, DamageSpecifier damage, BodyPartType? only = null)
    {
        var specifier = new DamageSpecifier(damage);
        if (TryComp<DamageableComponent>(target, out var damageable))
            _damage.AddDamage(target, damageable, specifier);

        foreach (var (partUid, part) in _body.GetBodyChildren(target))
        {
            if (only != null && part.PartType != only)
                continue;

            _bodyPartHealth.TryApplyPartDamage(target, partUid, specifier, tool: dagger, ignoreResistance: true);
        }
    }

    private void SpawnFlayLeftovers(Entity<YautjaCeremonialDaggerComponent> dagger, EntityUid victim, bool meat, int hide)
    {
        var coords = Transform(victim).Coordinates;
        var name = Name(victim);
        if (meat)
        {
            var steak = Spawn(dagger.Comp.MeatPrototype, coords);
            _meta.SetEntityName(steak, Loc.GetString("cmu-yautja-ceremonial-dagger-flay-meat-name", ("name", name)));
        }

        if (hide <= 0)
            return;

        var skin = Spawn(dagger.Comp.HidePrototype, coords);
        _stack.SetCount(skin, hide);
        _meta.SetEntityName(skin, Loc.GetString("cmu-yautja-ceremonial-dagger-flay-hide-name", ("name", name)));
    }
}
