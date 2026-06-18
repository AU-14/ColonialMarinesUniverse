using Content.Shared._CMU14.Yautja;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Care;
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
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedHumanMedicalSystem _medical = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private HumanTreatmentSystem _treatment = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;

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

        if (TryComp(target, out HumanMedicalComponent? medical))
        {
            if (gun.Comp.TreatsWounds)
                TreatWounds(target, medical);

            if (gun.Comp.RepairsFractures)
                RepairFractures(target, medical);
        }

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
        if (TryComp(target.Owner, out HumanMedicalComponent? medical))
        {
            if (gun.Comp.TreatsWounds && HasUntreatedWounds(medical))
                return true;

            if (gun.Comp.RepairsFractures && HasSkeletalDamage(medical))
                return true;

            if (gun.Comp.BloodlossModifier < 0 && HasActiveBleeding(medical))
                return true;
        }

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

    private bool TreatWounds(EntityUid target, HumanMedicalComponent medical)
    {
        var changed = false;
        for (var i = 0; i < medical.BleedSources.Count; i++)
        {
            var source = medical.BleedSources[i];
            if (!source.Active)
                continue;

            changed |= source.Kind switch
            {
                BleedKind.Internal => _treatment.TryApplyTreatment(
                    target,
                    new TreatmentAttempt(
                        TreatmentKind.ClampBleed,
                        source.Region,
                        BleedSourceId: source.Id),
                    medical).Applied,

                BleedKind.Stump => _treatment.TryApplyTreatment(
                    target,
                    new TreatmentAttempt(
                        TreatmentKind.Suture,
                        source.Region,
                        InjuryId: source.SourceInjuryId,
                        BleedSourceId: source.Id),
                    medical).Applied,

                _ => _treatment.TryApplyTreatment(
                    target,
                    new TreatmentAttempt(
                        TreatmentKind.Gauze,
                        source.Region,
                        BleedSourceId: source.Id),
                    medical).Applied,
            };
        }

        for (var i = 0; i < medical.Injuries.Count; i++)
        {
            var injury = medical.Injuries[i];
            if (injury.Flags.HasFlag(InjuryFlags.Closed) ||
                injury.Flags.HasFlag(InjuryFlags.Sutured))
            {
                continue;
            }

            if (injury.Kind == InjuryKind.Burn)
            {
                if (injury.Flags.HasFlag(InjuryFlags.Salved))
                    continue;

                changed |= _treatment.TryApplyTreatment(
                    target,
                    new TreatmentAttempt(
                        TreatmentKind.Salve,
                        injury.Region,
                        InjuryId: injury.Id),
                    medical).Applied;
                continue;
            }

            if (!IsSuturable(injury.Kind))
                continue;

            changed |= _treatment.TryApplyTreatment(
                target,
                new TreatmentAttempt(
                    TreatmentKind.Suture,
                    injury.Region,
                    InjuryId: injury.Id),
                medical).Applied;
        }

        return changed;
    }

    private bool RepairFractures(EntityUid target, HumanMedicalComponent medical)
    {
        var result = HumanMedicalLedger.RepairAllSkeletalDamage(medical);
        if (!result.Applied)
            return false;

        Dirty(target, medical);
        _medical.NotifyLedgerChanged((target, medical), result);

        return true;
    }

    private static bool HasUntreatedWounds(HumanMedicalComponent medical)
    {
        for (var i = 0; i < medical.BleedSources.Count; i++)
        {
            if (medical.BleedSources[i].Active)
                return true;
        }

        for (var i = 0; i < medical.Injuries.Count; i++)
        {
            var injury = medical.Injuries[i];
            if (injury.Kind == InjuryKind.Burn)
            {
                if (!injury.Flags.HasFlag(InjuryFlags.Salved))
                    return true;

                continue;
            }

            if (IsSuturable(injury.Kind) &&
                !injury.Flags.HasFlag(InjuryFlags.Closed) &&
                !injury.Flags.HasFlag(InjuryFlags.Sutured))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSkeletalDamage(HumanMedicalComponent medical)
    {
        for (var i = 1; i < medical.Regions.Length; i++)
        {
            var skeletal = medical.Regions[i].Skeletal;
            if (skeletal.Flags != SkeletalStateFlags.None ||
                skeletal.KnittingSecondsRemaining > FixedPoint2.Zero)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasActiveBleeding(HumanMedicalComponent medical)
    {
        for (var i = 0; i < medical.BleedSources.Count; i++)
        {
            if (medical.BleedSources[i].Active)
                return true;
        }

        return false;
    }

    private static bool IsSuturable(InjuryKind kind)
    {
        return kind is InjuryKind.Cut or InjuryKind.Stump or InjuryKind.SurgicalIncision;
    }
}
