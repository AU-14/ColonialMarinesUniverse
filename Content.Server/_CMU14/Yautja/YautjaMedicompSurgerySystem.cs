using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Medical.Treatment.FirstAid;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Slow;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared._RMC14.Medical.Surgery.Conditions;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Yautja;

/// <summary>
///     Implements the CMSS13 mcomp_wounds effects while leaving scheduling,
///     tool validation, self-surgery and session locking to CMU surgery.
/// </summary>
public sealed class YautjaMedicompSurgerySystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedOrganHealthSystem _organHealth = default!;
    [Dependency] private CMUWoundLedgerSystem _woundLedger = default!;
    [Dependency] private SharedCMUWoundsSystem _wounds = default!;
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUYautjaMedicompSurgeryConditionComponent, CMSurgeryValidEvent>(OnSurgeryValid);
        SubscribeLocalEvent<CMUYautjaMedicompStabilizeStepComponent, CMSurgeryStepCompleteCheckEvent>(OnStabilizeCheck);
        SubscribeLocalEvent<CMUYautjaMedicompHealingGunStepComponent, CMSurgeryStepCompleteCheckEvent>(OnHealingGunCheck);
        SubscribeLocalEvent<CMUYautjaMedicompClampStepComponent, CMSurgeryStepCompleteCheckEvent>(OnClampCheck);
        SubscribeLocalEvent<CMUYautjaMedicompHealingGunStepComponent, CMSurgeryCanPerformStepEvent>(OnHealingGunCanPerform);
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

        var hasDamage = damageable.Damage.TryGetDamageInGroup(_prototypes.Index(BruteGroup), out var brute)
            && brute > 0
            || damageable.Damage.TryGetDamageInGroup(_prototypes.Index(BurnGroup), out var burn)
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

        args.Cancelled = !hasDamage;
    }

    private void OnStabilizeCheck(Entity<CMUYautjaMedicompStabilizeStepComponent> ent, ref CMSurgeryStepCompleteCheckEvent args)
    {
        if (!HasComp<CMUYautjaMedicompStabilizedComponent>(args.Part))
            args.Cancelled = true;
    }

    private void OnHealingGunCheck(Entity<CMUYautjaMedicompHealingGunStepComponent> ent, ref CMSurgeryStepCompleteCheckEvent args)
    {
        if (!HasComp<CMUYautjaMedicompTreatedComponent>(args.Part))
            args.Cancelled = true;
    }

    private void OnClampCheck(Entity<CMUYautjaMedicompClampStepComponent> ent, ref CMSurgeryStepCompleteCheckEvent args)
    {
        if (!HasComp<CMUYautjaMedicompTreatedComponent>(args.Part))
            args.Cancelled = true;
    }

    private void OnHealingGunCanPerform(
        Entity<CMUYautjaMedicompHealingGunStepComponent> ent,
        ref CMSurgeryCanPerformStepEvent args)
    {
        foreach (var tool in args.Tools)
        {
            if (!TryComp<YautjaHealingGunComponent>(tool, out var gun))
                continue;

            if (gun.Loaded)
                return;

            args.Invalid = StepInvalidReason.MissingTool;
            args.Popup = "The healing gun is empty.";
            return;
        }
    }

    private void OnStabilize(Entity<CMUYautjaMedicompStabilizeStepComponent> ent, ref CMSurgeryStepEvent args)
    {
        ApplyGroupHeal(args.Body, 40);
        _slow.TrySlowdown(args.Body, TimeSpan.FromSeconds(30));
        _slow.TrySuperSlowdown(args.Body, TimeSpan.FromSeconds(15));
        EnsureComp<CMUYautjaMedicompStabilizedComponent>(args.Part);
        _popup.PopupEntity("You stabilize the wounds.", args.Body, args.User);
    }

    private void OnHealingGun(Entity<CMUYautjaMedicompHealingGunStepComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!TryFindHealingGun(args.Tools, out var gun) || !gun.Comp.Loaded)
            return;

        ApplyGroupHeal(args.Body, 65);
        foreach (var organ in _medicalIndex.GetOrgans(args.Body))
        {
            if (!TryComp<OrganHealthComponent>(organ.Owner, out var health))
                continue;

            _organHealth.HealOrgan((organ.Owner, health), args.Body, health.Max);
        }

        _slow.TrySlowdown(args.Body, TimeSpan.FromSeconds(30));
        _slow.TrySuperSlowdown(args.Body, TimeSpan.FromSeconds(15));
        RemComp<CMUYautjaMedicompStabilizedComponent>(args.Part);
        EnsureComp<CMUYautjaMedicompTreatedComponent>(args.Part);
        gun.Comp.Loaded = false;
        Dirty(gun);
    }

    private void OnClamp(Entity<CMUYautjaMedicompClampStepComponent> ent, ref CMSurgeryStepEvent args)
    {
        ApplyGroupHeal(args.Body, 125);
        RemComp<RMCSlowdownComponent>(args.Body);
        RemComp<RMCSuperSlowdownComponent>(args.Body);

        foreach (var (part, _) in _medicalIndex.GetBodyParts(args.Body))
        {
            _wounds.StopSurfaceBleedingOnPart(part);
            _woundLedger.TryUpdateExternalBleeding(part, ExternalBleedTier.None);
            RemComp<CMUEscharComponent>(part);
        }

        RemComp<CMUYautjaMedicompTreatedComponent>(args.Part);
    }

    private void ApplyGroupHeal(EntityUid body, int amount)
    {
        var heal = new DamageSpecifier(_prototypes.Index(BruteGroup), -amount)
                   + new DamageSpecifier(_prototypes.Index(BurnGroup), -amount);
        _damageable.TryChangeDamage(body, heal, ignoreResistances: true);
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
