using System.Linq;
using Content.Shared._RMC14.Synth;
using Content.Shared._CMU14.Fishing.Components;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Yautja;
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

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaMeleeWeaponSystem : EntitySystem
{
    private const string YautjaInterferenceStatus = "YautjaInterference";

    [Dependency] private EntityTableSystem _entityTable = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodyPartHealthSystem _bodyPartHealth = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StatusEffectQuerySystem _status = default!;
    [Dependency] private YautjaTrophySystem _trophies = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaMeleeXenoInterferenceComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<YautjaScytheBonusStrikeComponent, MeleeHitEvent>(OnScytheMeleeHit);
        SubscribeLocalEvent<YautjaHunterSpearFishingComponent, AfterInteractEvent>(OnHunterSpearAfterInteract);
        SubscribeLocalEvent<YautjaHunterSpearFishingComponent, YautjaHunterSpearFishingDoAfterEvent>(OnHunterSpearFishingDoAfter);
        SubscribeLocalEvent<YautjaCeremonialDaggerComponent, MeleeHitEvent>(OnCeremonialDaggerMeleeHit);
        SubscribeLocalEvent<YautjaCeremonialDaggerComponent, AfterInteractEvent>(OnCeremonialDaggerAfterInteract);
        SubscribeLocalEvent<YautjaCeremonialDaggerComponent, YautjaCeremonialDaggerPrepareFlayDoAfterEvent>(OnCeremonialDaggerPrepareFlayDoAfter);
        SubscribeLocalEvent<YautjaCeremonialDaggerComponent, YautjaCeremonialDaggerFlayDoAfterEvent>(OnCeremonialDaggerFlayDoAfter);
        SubscribeLocalEvent<YautjaCeremonialDaggerComponent, YautjaCeremonialDaggerLimbFlayDoAfterEvent>(OnCeremonialDaggerLimbFlayDoAfter);
    }

    private void OnMeleeHit(Entity<YautjaMeleeXenoInterferenceComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit ||
            ent.Comp.Duration <= TimeSpan.Zero ||
            !CanApplyInterference(args.User))
        {
            return;
        }

        foreach (var target in args.HitEntities)
        {
            if (!HasComp<XenoComponent>(target))
                continue;

            _status.TryAddStatusEffect(target, YautjaInterferenceStatus, ent.Comp.Duration, true);
        }
    }

    private bool CanApplyInterference(EntityUid user)
    {
        return HasComp<YautjaComponent>(user);
    }

    private void OnScytheMeleeHit(Entity<YautjaScytheBonusStrikeComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit ||
            args.HitEntities.Count == 0 ||
            ent.Comp.Chance <= 0f ||
            !_random.Prob(ent.Comp.Chance))
        {
            return;
        }

        args.BonusDamage += args.BaseDamage;
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-scythe-bonus-strike-others"),
            args.User,
            Filter.PvsExcept(args.User),
            true,
            PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-scythe-bonus-strike-self"), args.User, args.User, PopupType.MediumCaution);
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

    private void OnCeremonialDaggerMeleeHit(Entity<YautjaCeremonialDaggerComponent> dagger, ref MeleeHitEvent args)
    {
        if (args.Handled ||
            !args.IsHit ||
            args.HitEntities.Count != 1)
        {
            return;
        }

        var target = args.HitEntities[0];
        if (!_mobState.IsDead(target))
            return;

        if (!HasComp<HumanoidAppearanceComponent>(target) || HasComp<XenoComponent>(target))
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("cmu-yautja-ceremonial-dagger-flay-non-human"), args.User, args.User);
            return;
        }

        if (!HasComp<YautjaComponent>(args.User))
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("cmu-yautja-ceremonial-dagger-flay-not-strong"), args.User, args.User);
            return;
        }

        if (HasComp<YautjaComponent>(target))
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("cmu-yautja-ceremonial-dagger-flay-yautja"), args.User, args.User, PopupType.LargeCaution);
            return;
        }

        if (HasComp<SynthComponent>(target))
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("cmu-yautja-ceremonial-dagger-flay-synth"), args.User, args.User);
            return;
        }

        if (TryComp(target, out YautjaFlayedComponent? flayed))
        {
            args.Handled = TryCompleteCeremonialDaggerScalpStage(dagger, args.User, target, flayed);
            return;
        }

        args.Handled = TryStartCeremonialDaggerPrepareFlay(dagger, args.User, target);
    }

    private void OnCeremonialDaggerAfterInteract(Entity<YautjaCeremonialDaggerComponent> dagger, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        args.Handled = TryStartCeremonialDaggerLimbFlay(dagger, args.User, target, args.CanReach);
    }

    private bool TryStartCeremonialDaggerLimbFlay(
        Entity<YautjaCeremonialDaggerComponent> dagger,
        EntityUid user,
        EntityUid target,
        bool canReach)
    {
        if (!canReach ||
            !HasComp<YautjaComponent>(user) && !HasComp<YautjaTechAuthorizedComponent>(user) ||
            !TryComp(target, out BodyPartComponent? bodyPart) ||
            bodyPart.Body != null ||
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
            !HasComp<YautjaComponent>(args.User))
        {
            return;
        }

        StartCeremonialDaggerFlay(dagger, args.User, target);
    }

    private bool StartCeremonialDaggerFlay(
        Entity<YautjaCeremonialDaggerComponent> dagger,
        EntityUid user,
        EntityUid target)
    {
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-ceremonial-dagger-flay-start-others", ("user", user), ("victim", target), ("dagger", dagger.Owner)),
            user,
            Filter.PvsExcept(user),
            true,
            PopupType.MediumCaution);
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-ceremonial-dagger-flay-start-self", ("victim", target), ("dagger", dagger.Owner)),
            user,
            user,
            PopupType.MediumCaution);
        _audio.PlayPvs(dagger.Comp.StartFlaySound, target);

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

        return _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnCeremonialDaggerFlayDoAfter(
        Entity<YautjaCeremonialDaggerComponent> dagger,
        ref YautjaCeremonialDaggerFlayDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (args.Cancelled ||
            args.Target is not { } target ||
            Deleted(target) ||
            !_mobState.IsDead(target) ||
            !HasComp<YautjaComponent>(args.User))
        {
            return;
        }

        FinishCeremonialDaggerFirstFlayPass(dagger, args.User, target);
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
            !TryComp(target, out BodyPartComponent? bodyPart) ||
            bodyPart.Body != null ||
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

    private void FinishCeremonialDaggerFirstFlayPass(
        Entity<YautjaCeremonialDaggerComponent> dagger,
        EntityUid user,
        EntityUid target)
    {
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-ceremonial-dagger-flay-first-pass-others", ("user", user), ("victim", target)),
            user,
            Filter.PvsExcept(user),
            true,
            PopupType.MediumCaution);
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-ceremonial-dagger-flay-first-pass-self", ("victim", target)),
            user,
            user,
            PopupType.MediumCaution);
        _audio.PlayPvs(dagger.Comp.FirstPassSound, target);

        var flayed = EnsureComp<YautjaFlayedComponent>(target);
        flayed.Stage = 1;
        flayed.NextStage = YautjaFlayingStage.Scalp;
        flayed.CurrentFlayer = null;
        Dirty(target, flayed);

        var damage = new DamageSpecifier(dagger.Comp.FirstPassDamage);
        if (TryComp<DamageableComponent>(target, out var damageable))
            _damage.AddDamage(target, damageable, damage);

        foreach (var (partUid, _) in _body.GetBodyChildren(target))
        {
            _bodyPartHealth.TryApplyPartDamage(target, partUid, damage, tool: dagger.Owner, ignoreResistance: true);
        }
    }

    private bool TryCompleteCeremonialDaggerScalpStage(
        Entity<YautjaCeremonialDaggerComponent> dagger,
        EntityUid user,
        EntityUid target,
        YautjaFlayedComponent flayed)
    {
        if (flayed.NextStage != YautjaFlayingStage.Scalp)
            return false;

        flayed.NextStage = YautjaFlayingStage.Strip;
        flayed.CurrentFlayer = null;
        Dirty(target, flayed);

        _audio.PlayPvs(dagger.Comp.FirstPassSound, target);
        _trophies.SpawnRuntimeScalp(target, user);
        return true;
    }
}
