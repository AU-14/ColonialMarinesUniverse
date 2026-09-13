using System.Linq;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Treatment.FirstAid;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Treatment.Surgery.Traits;
using Content.Server.CMU14.Medical.Treatment.Surgery;
using Content.Shared.Administration.Logs;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Database;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Robust.Shared.Audio.Systems;

namespace Content.Server.CMU14.Yautja;

public sealed partial class YautjaHealingGunSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private SharedBoneSystem _bone = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedFractureSystem _fracture = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedOrganHealthSystem _organHealth = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private CMUSurgerySystem _surgery = default!;
    [Dependency] private SharedCMUSurgicalTraitSystem _surgicalTraits = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private SharedCMUWoundsSystem _wounds = default!;
    [Dependency] private CMUWoundLedgerSystem _woundLedger = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaHealingGunComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<YautjaHealingGunComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<YautjaHealingGunComponent, YautjaDeepRepairDoAfterEvent>(OnDeepRepairComplete);
    }

    private void OnUseInHand(Entity<YautjaHealingGunComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryHeal(ent, args.User, args.User))
            args.Handled = true;
    }

    private void OnAfterInteract(Entity<YautjaHealingGunComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (TryHeal(ent, target, args.User))
            args.Handled = true;
    }

    private bool TryHeal(Entity<YautjaHealingGunComponent> gun, EntityUid target, EntityUid user)
    {
        if (!TryComp(target, out DamageableComponent? damageable)
            || !TryComp(target, out InjurableComponent? injurable))
            return false;

        if (gun.Comp.DamageContainers is not null
            && injurable.DamageContainer is { } container
            && !gun.Comp.DamageContainers.Contains(container))
        {
            return false;
        }

        if (user != target && !_interaction.InRangeUnobstructed(user, target, popup: true))
            return false;

        var deepRepair = user == target
            && HasComp<YautjaComponent>(target)
            && HasDeepRepairDamage(target, gun.Comp);
        if (!HasDamage(gun, (target, damageable)) && !deepRepair)
        {
            _popup.PopupClient(Loc.GetString("medical-item-cant-use", ("item", gun.Owner)), gun.Owner, user);
            return false;
        }

        var hasUseDelay = TryComp(gun.Owner, out UseDelayComponent? delay);
        if (hasUseDelay && _useDelay.IsDelayed((gun.Owner, delay)))
            return false;

        if (deepRepair)
        {
            if (!TryStartDeepRepair(gun, user))
                return false;

            if (hasUseDelay)
                _useDelay.TryResetDelay((gun.Owner, delay!));
            return true;
        }

        if (hasUseDelay && !_useDelay.TryResetDelay((gun.Owner, delay!), true))
            return false;

        ApplyQuickTreatment(gun, target, user, damageable);
        return true;
    }

    private void ApplyQuickTreatment(
        Entity<YautjaHealingGunComponent> gun,
        EntityUid target,
        EntityUid user,
        DamageableComponent damageable)
    {
        if (TryComp(target, out BloodstreamComponent? bloodstream))
        {
            if (gun.Comp.BloodlossModifier != 0)
            {
                var wasBleeding = bloodstream.BleedAmount > 0;
                _bloodstream.TryModifyBleedAmount((target, bloodstream), gun.Comp.BloodlossModifier);
                if (wasBleeding && bloodstream.BleedAmount <= 0)
                {
                    var popup = user == target
                        ? Loc.GetString("medical-item-stop-bleeding-self")
                        : Loc.GetString("medical-item-stop-bleeding", ("target", Identity.Entity(target, EntityManager)));
                    _popup.PopupClient(popup, target, user);
                }
            }

            if (gun.Comp.ModifyBloodLevel != 0)
                _bloodstream.TryModifyBloodLevel((target, bloodstream), gun.Comp.ModifyBloodLevel);
        }
        if (gun.Comp.TreatsWounds)
            TreatWounds(target);

        if (gun.Comp.RepairsFractures)
            RepairFractures(target);
        var healed = _damageable.TryChangeDamage(target, gun.Comp.Damage * _damageable.UniversalTopicalsHealModifier, true, origin: user);
        var total = healed?.GetTotal() ?? FixedPoint2.Zero;

        _audio.PlayPredicted(gun.Comp.HealSound, gun.Owner, user);

        if (user != target)
        {
            _popup.PopupEntity(
                Loc.GetString("medical-item-popup-target", ("user", Identity.Entity(user, EntityManager)), ("item", gun.Owner)),
                target,
                target,
                PopupType.Medium);
            _adminLogger.Add(LogType.Healed, $"{ToPrettyString(user):user} healed {ToPrettyString(target):target} for {total:damage} damage with {ToPrettyString(gun.Owner):item}");
        }
        else
        {
            _adminLogger.Add(LogType.Healed, $"{ToPrettyString(user):user} healed themselves for {total:damage} damage with {ToPrettyString(gun.Owner):item}");
        }

    }

    private bool TryStartDeepRepair(Entity<YautjaHealingGunComponent> gun, EntityUid user)
    {
        var args = new DoAfterArgs(EntityManager,
            user,
            gun.Comp.DeepRepairDuration,
            new YautjaDeepRepairDoAfterEvent(),
            gun.Owner,
            user,
            gun.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnHandChange = true,
            NeedHand = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
        };

        return _doAfter.TryStartDoAfter(args);
    }

    private void OnDeepRepairComplete(Entity<YautjaHealingGunComponent> gun, ref YautjaDeepRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !HasComp<YautjaComponent>(target))
            return;

        args.Handled = true;
        foreach (var (part, _) in _medicalIndex.GetBodyParts(target))
        {
            RepairPartDeepDamage(part);
        }

        if (gun.Comp.RepairsOrgans && TryComp(target, out BodyComponent? body) && body.Organs is { } organs)
        {
            foreach (var organ in organs.ContainedEntities)
            {
                if (!TryComp(organ, out OrganHealthComponent? health) || health.Current >= health.Max)
                    continue;

                _organHealth.HealOrgan((organ, (OrganHealthComponent?) health), target, health.Max - health.Current);
            }
        }

        if (TryComp(target, out DamageableComponent? damageable))
            ApplyQuickTreatment(gun, target, target, damageable);
    }

    private void RepairPartDeepDamage(EntityUid part)
    {
        if (HasComp<CMUEscharComponent>(part))
            RemComp<CMUEscharComponent>(part);

        foreach (var trait in _surgicalTraits.EnumerateOrderedTraits(part).ToArray())
        {
            _surgery.TryResolveSurgicalTrait(part, trait);
        }

        _wounds.SuppressInternalBleed(part);
    }

    private bool HasDeepRepairDamage(EntityUid target, YautjaHealingGunComponent gun)
    {
        foreach (var (part, _) in _medicalIndex.GetBodyParts(target))
        {
            if (HasComp<CMUEscharComponent>(part)
                || HasComp<InternalBleedingComponent>(part)
                || HasComp<CMUSurgicalInternalBleedingComponent>(part)
                || _surgicalTraits.CountTraits(part) > 0)
                return true;
        }

        if (!gun.RepairsOrgans || !TryComp(target, out BodyComponent? body) || body.Organs is not { } organs)
            return false;

        foreach (var organ in organs.ContainedEntities)
        {
            if (TryComp(organ, out OrganHealthComponent? health) && health.Current < health.Max)
                return true;
        }

        return false;
    }

    private bool HasDamage(Entity<YautjaHealingGunComponent> gun, Entity<DamageableComponent> target)
    {
        if (gun.Comp.TreatsWounds && HasUntreatedWounds(target.Owner))
            return true;

        if (gun.Comp.RepairsFractures && HasFractures(target.Owner))
            return true;

        var damage = _damageable.GetAllDamage((target.Owner, (DamageableComponent?) target.Comp));
        foreach (var (type, amount) in gun.Comp.Damage.DamageDict)
        {
            if (amount < 0
                && damage.DamageDict.TryGetValue(type, out var current)
                && current > 0)
            {
                return true;
            }
        }

        return TryComp(target, out BloodstreamComponent? bloodstream)
            && gun.Comp.BloodlossModifier < 0
            && bloodstream.BleedAmount > 0;
    }

    private bool TreatWounds(EntityUid target)
    {
        var changed = false;
        foreach (var (partUid, _) in _medicalIndex.GetBodyParts(target))
        {
            var guard = 0;
            while (guard++ < 128 && _wounds.TryTreatWound(partUid, out _))
            {
                changed = true;
            }
        }

        return changed;
    }

    private bool RepairFractures(EntityUid target)
    {
        var changed = false;
        foreach (var (partUid, _) in _medicalIndex.GetBodyParts(target))
        {
            if (!TryComp<FractureComponent>(partUid, out var fracture))
                continue;

            if (TryComp<BoneComponent>(partUid, out var bone))
                _bone.RestoreIntegrity((partUid, bone), bone.IntegrityMax);

            _fracture.SetSeverity((partUid, fracture), FractureSeverity.None, forceUpgrade: false);
            RemComp<CMUSplintedComponent>(partUid);
            RemComp<CMUCastComponent>(partUid);
            RemComp<CMUMalunionComponent>(partUid);
            RemComp<CMUPostOpBoneSetComponent>(partUid);
            _wounds.RecomputeInternalBleed(partUid);
            changed = true;
        }

        return changed;
    }

    private bool HasUntreatedWounds(EntityUid target)
    {
        foreach (var (partUid, _) in _medicalIndex.GetBodyParts(target))
        {
            if (!TryComp<BodyPartWoundComponent>(partUid, out var wounds))
                continue;

            if (_woundLedger.CountUntreatedWounds(wounds) > 0)
                return true;
        }

        return false;
    }

    private bool HasFractures(EntityUid target)
    {
        foreach (var (partUid, _) in _medicalIndex.GetBodyParts(target))
        {
            if (TryComp<FractureComponent>(partUid, out var fracture)
                && fracture.Severity != FractureSeverity.None)
            {
                return true;
            }
        }

        return false;
    }
}
