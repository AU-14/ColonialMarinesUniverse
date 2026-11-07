using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Popups;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;

// ECS-05 (node N11): Desecrate, the level-2 targeted punch (MeldDesecrateCombo §3.2). The handler only
// checks; the effect is public for the sequence combos. Subscriptions stay in EWJoeCombatSystem.cs.
public sealed partial class EWJoeCombatSystem
{
    private static readonly EWJoeDesecrateComponent DefaultDesecrate = new();

    #region Desecrate

    private void OnDesecrate(Entity<EWJoeComponent> ent, ref EWJoeDesecrateActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        if (target == ent.Owner || !_mobState.IsAlive(target))
            return;

        args.Handled = true;
        // ARCH-05: an Exalted Joe's Desecrate seeds a compound fracture
        var empowered = _liturgy.TryConsumeEmpowered(ent, target, ccBonus: false, "Desecrate");
        Desecrate(ent, target, empowered);
        _liturgy.AbilityLanded(ent, target);
    }

    /// <summary>
    /// Desecrate: lets go of <paramref name="target"/> if held, hits it for heavy Blunt, breaks a bone (the
    /// Joe's selected zone, else the torso, else any limb; Compound when <paramref name="empowered"/>), and
    /// knocks it back with the RMC obstacle slam armed for a wall, table or barricade in its path. A body
    /// with no bone to break (synthetics, xenofauna) takes bonus damage instead, and xenofauna also lose
    /// armor (the Chitin Crack shred). Large xenofauna are not moved. No liveness or range check; callers
    /// check first.
    /// </summary>
    public void Desecrate(Entity<EWJoeComponent> joe, EntityUid target, bool empowered = false)
    {
        var desecrate = CompOrNull<EWJoeDesecrateComponent>(joe) ?? DefaultDesecrate;

        // a finisher out of a grab: let go so the body can fly
        ReleaseIfHeld(joe, target);

        var severity = empowered ? desecrate.EmpoweredSeverity : desecrate.Severity;
        var fractured = TryDesecrateFracture(joe, target, severity, out var part);

        var amount = desecrate.Damage + (fractured ? 0f : desecrate.BonelessBonusDamage);
        _damageable.TryChangeDamage(target, new DamageSpecifier(_proto.Index(Blunt), amount), origin: joe);

        if (fractured)
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-desecrate-victim", ("part", Name(part))), target, target, PopupType.LargeCaution);
        }
        else if (IsXenofauna(target))
        {
            _shred.Shred(target, desecrate.XenoArmorShred, desecrate.XenoArmorShredDuration);
        }

        if (KnockBackFromJoe(joe, target, desecrate.KnockbackDistance, desecrate.KnockbackSpeed))
        {
            // ECS-11: xenofauna get the Joe's stun multiplier before RMC's own reduction
            var paralyze = EWJoeTargetRules.ScaleStun(EntityManager, target, desecrate.SlamParalyze, joe.Comp.XenofaunaStunMultiplier);
            _obstacleSlam.ApplyBonuses(target, paralyze, desecrate.SlamSlow);
        }

        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-desecrate-done", ("target", target)), target, joe, PopupType.MediumCaution);
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"Early Warning Joe {ToPrettyString(joe):joe} desecrated {ToPrettyString(target):target}{(empowered ? " (empowered)" : "")}");
    }

    /// <summary>
    /// The guaranteed fracture: the part for the Joe's selected zone (the chest when it has none), then
    /// the root part (the torso), then any limb. <see cref="SharedBoneSystem.SeedFracture"/> refuses parts
    /// without bones and synthetic parts, so a boneless body returns false.
    /// </summary>
    public bool TryDesecrateFracture(EntityUid joe, EntityUid target, FractureSeverity severity, out EntityUid part)
    {
        part = default;

        var zone = _zoneTargeting.TryGetSelection(joe) ?? TargetBodyZone.Chest;
        var (type, symmetry) = SharedBodyZoneTargetingSystem.ToBodyPart(zone);
        EntityUid? selected = null;
        if (_bodyIndex.TryGetBodyPart(target, new CMUMedicalBodyPartKey(type, symmetry), out var selectedPart))
        {
            selected = selectedPart;
            if (_bone.SeedFracture(selectedPart, severity))
            {
                part = selectedPart;
                return true;
            }
        }

        EntityUid? root = null;
        if (_bodyIndex.TryGetRootPart(target, out var rootPart))
        {
            root = rootPart.Owner;
            if (rootPart.Owner != selected && _bone.SeedFracture(rootPart.Owner, severity))
            {
                part = rootPart.Owner;
                return true;
            }
        }

        foreach (var (limb, limbComp) in _bodyIndex.GetBodyParts(target))
        {
            if (limb == selected || limb == root)
                continue;

            if (limbComp.PartType is not (BodyPartType.Arm or BodyPartType.Hand or BodyPartType.Leg or BodyPartType.Foot))
                continue;

            if (_bone.SeedFracture(limb, severity))
            {
                part = limb;
                return true;
            }
        }

        return false;
    }

    #endregion
}
