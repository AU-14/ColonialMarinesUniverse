using System.Linq;
using Content.Server.CMU14.Medical.Treatment.Surgery;
using Content.Shared.CMU14.Medical.Treatment.Surgery.Traits;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Slow;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared._RMC14.Medical.Surgery.Conditions;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja;

/// <summary>
///     Implements the CMSS13 mcomp_wounds effects while leaving scheduling,
///     tool validation, self-surgery and session locking to CMU surgery.
/// </summary>
public sealed partial class YautjaMedicompSurgerySystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedOrganHealthSystem _organHealth = default!;
    [Dependency] private CMUSurgerySystem _surgery = default!;
    [Dependency] private SharedCMUSurgicalTraitSystem _surgicalTraits = default!;
    [Dependency] private CMUWoundLedgerSystem _woundLedger = default!;
    [Dependency] private SharedCMUWoundsSystem _wounds = default!;
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    private static readonly TimeSpan MedicompSlowdownDuration = TimeSpan.FromMinutes(10);

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUYautjaMedicompSurgeryConditionComponent, CMSurgeryValidEvent>(OnSurgeryValid);
        // surface-depth field surgery: hunters never strip armor to self-treat, as in the movies.
        SubscribeLocalEvent<CMUYautjaMedicompStabilizeStepComponent, CMSurgeryCanPerformStepEvent>(OnMedicompStepCanPerform, before: [typeof(SharedCMSurgerySystem)]);
        SubscribeLocalEvent<CMUYautjaMedicompClampStepComponent, CMSurgeryCanPerformStepEvent>(OnMedicompStepCanPerform, before: [typeof(SharedCMSurgerySystem)]);
        SubscribeLocalEvent<CMUYautjaMedicompHealingGunStepComponent, CMSurgeryCanPerformStepEvent>(OnMedicompStepCanPerform, before: [typeof(SharedCMSurgerySystem)]);
        SubscribeLocalEvent<CMUYautjaMedicompStabilizeStepComponent, CMSurgeryStepCompleteCheckEvent>(OnStabilizeCheck);
        SubscribeLocalEvent<CMUYautjaMedicompHealingGunStepComponent, CMSurgeryStepCompleteCheckEvent>(OnTreatedCheck);
        SubscribeLocalEvent<CMUYautjaMedicompClampStepComponent, CMSurgeryStepCompleteCheckEvent>(OnTreatedCheck);
        SubscribeLocalEvent<CMUYautjaMedicompStabilizeStepComponent, CMSurgeryStepEvent>(OnStabilize);
        SubscribeLocalEvent<CMUYautjaMedicompHealingGunStepComponent, CMSurgeryStepEvent>(OnHealingGun);
        SubscribeLocalEvent<CMUYautjaMedicompClampStepComponent, CMSurgeryStepEvent>(OnClamp);
    }

    private void OnSurgeryValid(
        Entity<CMUYautjaMedicompSurgeryConditionComponent> ent,
        ref CMSurgeryValidEvent args)
    {
        if (!TryComp<DamageableComponent>(args.Body, out var damageable))
        {
            args.Cancelled = true;
            return;
        }

        var damage = _damageable.GetAllDamage((args.Body, damageable));
        // A completed treatment can remove the last injury before the closing
        // step. Keep that in-progress procedure available until its markers clear.
        var hasDamage = HasComp<CMUYautjaMedicompStabilizedComponent>(args.Part)
            || HasComp<CMUYautjaMedicompTreatedComponent>(args.Part)
            || damage.TryGetDamageInGroup(_prototypes.Index(BruteGroup), out var brute)
            && brute > 0
            || damage.TryGetDamageInGroup(_prototypes.Index(BurnGroup), out var burn)
            && burn > 0;

        if (!hasDamage)
        {
            foreach (var organ in _medicalIndex.GetOrgans(args.Body))
            {
                if (!TryComp<OrganHealthComponent>(organ.Owner, out var health) || health.Current >= health.Max)
                    continue;

                hasDamage = true;
                break;
            }
        }

        if (!hasDamage)
        {
            foreach (var (part, _) in _medicalIndex.GetBodyParts(args.Body))
            {
                if (!HasComp<CMUEscharComponent>(part)
                    && !HasComp<InternalBleedingComponent>(part)
                    && !HasComp<CMUSurgicalInternalBleedingComponent>(part)
                    && _surgicalTraits.CountTraits(part) == 0)
                    continue;

                hasDamage = true;
                break;
            }
        }

        args.Cancelled = !hasDamage;
    }

    private void OnMedicompStepCanPerform<T>(Entity<T> ent, ref CMSurgeryCanPerformStepEvent args)
        where T : IComponent
    {
        args.IgnoreArmor = true;

        if (ent is Entity<CMUYautjaMedicompHealingGunStepComponent>)
        {
            foreach (var tool in args.Tools)
            {
                if (!TryComp<YautjaHealingGunComponent>(tool, out var gun))
                    continue;

                if (gun.Loaded)
                    return;

                args.Invalid = StepInvalidReason.MissingTool;
                args.Popup = Loc.GetString("cmu-yautja-medicomp-empty-gun");
                return;
            }
        }
    }

    private void OnStabilizeCheck(Entity<CMUYautjaMedicompStabilizeStepComponent> ent, ref CMSurgeryStepCompleteCheckEvent args)
    {
        if (!HasComp<CMUYautjaMedicompStabilizedComponent>(args.Part))
            args.Cancelled = true;
    }

    private void OnTreatedCheck<T>(Entity<T> ent, ref CMSurgeryStepCompleteCheckEvent args) where T : IComponent
    {
        if (!HasComp<CMUYautjaMedicompTreatedComponent>(args.Part))
            args.Cancelled = true;
    }

    private void OnStabilize(Entity<CMUYautjaMedicompStabilizeStepComponent> ent, ref CMSurgeryStepEvent args)
    {
        ApplyGroupHeal(args.Body, 40);
        ApplyMedicompSlowdown(args.Body);
        EnsureComp<CMUYautjaMedicompStabilizedComponent>(args.Part);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-medicomp-stabilized"), args.Body, args.User);
    }

    private void OnHealingGun(Entity<CMUYautjaMedicompHealingGunStepComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!TryFindHealingGun(args.Tools, out var gun) || !gun.Comp.Loaded)
            return;

        ApplyGroupHeal(args.Body, 65);
        foreach (var organ in _medicalIndex.GetOrgans(args.Body))
        {
            if (!gun.Comp.RepairsOrgans || !TryComp<OrganHealthComponent>(organ.Owner, out var health))
                continue;

            _organHealth.HealOrgan((organ.Owner, health), args.Body, health.Max);
        }

        _slow.TrySlowdown(args.Body, MedicompSlowdownDuration);
        _slow.TrySuperSlowdown(args.Body, MedicompSlowdownDuration);

        // Keep master's deep repairs in the authoritative surgery completion,
        // so the healing capsule and session checks also apply to these wounds.
        foreach (var (part, _) in _medicalIndex.GetBodyParts(args.Body))
        {
            RemComp<CMUEscharComponent>(part);
            foreach (var trait in _surgicalTraits.EnumerateOrderedTraits(part).ToArray())
                _surgery.TryResolveSurgicalTrait(part, trait);
            _wounds.SuppressInternalBleed(part);
        }

        RemComp<CMUYautjaMedicompStabilizedComponent>(args.Part);
        EnsureComp<CMUYautjaMedicompTreatedComponent>(args.Part);
        gun.Comp.Loaded = false;
        Dirty(gun);
    }

    private void OnClamp(Entity<CMUYautjaMedicompClampStepComponent> ent, ref CMSurgeryStepEvent args)
    {
        // CMSS13's clamp completes the treatment: finishing the surgery restores
        // full health no matter how much damage is left. The speed debuffs from
        // stabilize and tend only clear here, so an incomplete treatment stays slow.
        HealToFull(args.Body);
        RemComp<RMCSlowdownComponent>(args.Body);
        RemComp<RMCSuperSlowdownComponent>(args.Body);

        foreach (var (part, _) in _medicalIndex.GetBodyParts(args.Body))
        {
            _wounds.StopSurfaceBleedingOnPart(part);
            _woundLedger.TryUpdateExternalBleeding(part, ExternalBleedTier.None);
            // CMSS13's clamp returns the selected defense zone to surface
            // depth. In CMU that is represented by removing the open-incision
            // markers and suppressing only the surgical bleed source.
            _wounds.ClearInternalBleed(part);
            RemComp<CMIncisionOpenComponent>(part);
            RemComp<CMBleedersClampedComponent>(part);
            RemComp<CMSkinRetractedComponent>(part);
            RemComp<CMUEscharComponent>(part);
        }

        RemComp<CMUYautjaMedicompTreatedComponent>(args.Part);
    }

    private void ApplyGroupHeal(EntityUid body, int amount)
    {
        if (!TryComp<DamageableComponent>(body, out var damageable))
            return;

        // An even per-type spread clamps at zero on undamaged types, so heal
        // through HealEvenly, which moves the excess to damaged types.
        _damageable.HealEvenly((body, damageable), -amount, BruteGroup);
        _damageable.HealEvenly((body, damageable), -amount, BurnGroup);
    }

    /// <summary>
    ///     CMU14: closing the wound restores the patient to full health and repairs every organ, instead
    ///     of only a fixed amount of brute/burn.
    /// </summary>
    private void HealToFull(EntityUid body)
    {
        if (TryComp<DamageableComponent>(body, out var damageable))
        {
            var current = _damageable.GetAllDamage((body, damageable));
            var heal = new DamageSpecifier();
            foreach (var (type, amount) in current.DamageDict)
            {
                if (amount > FixedPoint2.Zero)
                    heal.DamageDict[type] = -amount;
            }

            if (!heal.Empty)
                _damageable.TryChangeDamage(body, heal, ignoreResistances: true);
        }

        foreach (var organ in _medicalIndex.GetOrgans(body))
        {
            if (TryComp<OrganHealthComponent>(organ.Owner, out var health))
                _organHealth.HealOrgan((organ.Owner, health), body, health.Max);
        }
    }

    /// <summary>
    ///     CMU14: the patient is heavily slowed from the stabilize step until the wound is clamped shut.
    /// </summary>
    private void ApplyMedicompSlowdown(EntityUid body)
    {
        // CMU14: RMCSlowSystem refuses to slow a regular Yautja, and the medicomp patient is normally a
        // Yautja hunter, so the procedure has to drive the slow components itself. The clamp step clears them.
        var expire = _timing.CurTime + MedicompSlowdownDuration;

        var slow = EnsureComp<RMCSlowdownComponent>(body);
        if (expire > slow.ExpiresAt)
        {
            slow.ExpiresAt = expire;
            Dirty(body, slow);
        }

        var super = EnsureComp<RMCSuperSlowdownComponent>(body);
        if (expire > super.ExpiresAt)
        {
            super.ExpiresAt = expire;
            Dirty(body, super);
        }
    }

    private bool TryFindHealingGun(List<EntityUid> tools, out Entity<YautjaHealingGunComponent> gun)
    {
        foreach (var tool in tools)
        {
            if (TryComp(tool, out YautjaHealingGunComponent? component))
            {
                gun = (tool, component);
                return true;
            }
        }

        gun = default;
        return false;
    }
}
