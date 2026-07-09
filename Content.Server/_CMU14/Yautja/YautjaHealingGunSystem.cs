using Content.Shared._CMU14.Yautja;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Treatment.FirstAid;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Database;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Robust.Shared.Audio.Systems;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaHealingGunSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private SharedBoneSystem _bone = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedFractureSystem _fracture = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private SharedCMUWoundsSystem _wounds = default!;
    [Dependency] private CMUWoundLedgerSystem _woundLedger = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaHealingGunComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<YautjaHealingGunComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnUseInHand(Entity<YautjaHealingGunComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryHeal(ent, args.User, args.User, false))
            args.Handled = true;
    }

    private void OnAfterInteract(Entity<YautjaHealingGunComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (TryHeal(ent, target, args.User, true))
            args.Handled = true;
    }

    private bool TryHeal(Entity<YautjaHealingGunComponent> gun, EntityUid target, EntityUid user, bool resetDelay)
    {
        if (!TryComp(target, out DamageableComponent? damageable))
            return false;

        if (gun.Comp.DamageContainers is not null &&
            damageable.DamageContainerID is { } container &&
            !gun.Comp.DamageContainers.Contains(container))
        {
            return false;
        }

        if (user != target && !_interaction.InRangeUnobstructed(user, target, popup: true))
            return false;

        if (!HasDamage(gun, (target, damageable)))
        {
            _popup.PopupClient(Loc.GetString("medical-item-cant-use", ("item", gun.Owner)), gun.Owner, user);
            return false;
        }

        if (resetDelay &&
            TryComp(gun.Owner, out UseDelayComponent? delay) &&
            !_useDelay.TryResetDelay((gun.Owner, delay), true))
        {
            return false;
        }

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

        return true;
    }

    private bool HasDamage(Entity<YautjaHealingGunComponent> gun, Entity<DamageableComponent> target)
    {
        if (gun.Comp.TreatsWounds && HasUntreatedWounds(target.Owner))
            return true;

        if (gun.Comp.RepairsFractures && HasFractures(target.Owner))
            return true;

        foreach (var (type, amount) in gun.Comp.Damage.DamageDict)
        {
            if (amount < 0 &&
                target.Comp.Damage.DamageDict.TryGetValue(type, out var current) &&
                current > 0)
            {
                return true;
            }
        }

        return TryComp(target, out BloodstreamComponent? bloodstream) &&
               gun.Comp.BloodlossModifier < 0 &&
               bloodstream.BleedAmount > 0;
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
            if (TryComp<FractureComponent>(partUid, out var fracture) &&
                fracture.Severity != FractureSeverity.None)
            {
                return true;
            }
        }

        return false;
    }
}
