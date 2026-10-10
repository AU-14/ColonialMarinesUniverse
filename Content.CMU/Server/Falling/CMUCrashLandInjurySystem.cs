using Content.Shared._RMC14.CrashLand;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Falling;

/// <summary>
/// Falling out of a flying dropship without a parachute: whoever crash lands on the planet breaks every limb and
/// takes a severe concussion. Parachutes turn the crash into a paradrop before this ever happens.
/// </summary>
public sealed class CMUCrashLandInjurySystem : EntitySystem
{
    [Dependency] private SharedBoneSystem _bones = default!;
    [Dependency] private CMUChemicalMedicalSystem _chemicalMedical = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";

    /// <summary>Brain damage from the impact; takes a healthy 50-point brain into the damaged (severe concussion) stage.</summary>
    private static readonly FixedPoint2 ConcussionDamage = 32;

    private const FractureSeverity LimbFracture = FractureSeverity.Compound;
    private const FractureSeverity LegFracture = FractureSeverity.Shattered;

    /// <summary>Blunt damage from hitting the ground.</summary>
    private static readonly FixedPoint2 ImpactDamage = 105;

    // The crash-landing component blocks all damage until it's removed, which happens right after the landing
    // event, so the impact damage is applied on the next tick.
    private readonly List<EntityUid> _pendingImpacts = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUHumanMedicalComponent, CrashLandedEvent>(OnCrashLanded);
    }

    private void OnCrashLanded(Entity<CMUHumanMedicalComponent> ent, ref CrashLandedEvent args)
    {
        if (!args.ShouldDamage)
            return;

        foreach (var (part, partComp) in _medicalIndex.GetBodyParts(ent))
        {
            if (partComp.PartType == BodyPartType.Leg)
                _bones.SeedFracture(part, LegFracture);
            else if (partComp.PartType is BodyPartType.Arm or BodyPartType.Hand or BodyPartType.Foot)
                _bones.SeedFracture(part, LimbFracture);
        }

        _chemicalMedical.DamageOrgan<CMUBrainComponent>(ent, ConcussionDamage, Blunt, OrganDamageSource.Direct);
        _pendingImpacts.Add(ent);
    }

    public override void Update(float frameTime)
    {
        if (_pendingImpacts.Count == 0)
            return;

        foreach (var uid in _pendingImpacts)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            _damageable.TryChangeDamage(uid, new DamageSpecifier { DamageDict = { [Blunt] = ImpactDamage } },
                ignoreResistances: true);
        }

        _pendingImpacts.Clear();
    }
}
