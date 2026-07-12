using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.Body.Part;
using Content.Shared.Projectiles;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.ClawSharpness;
using Robust.Shared.Configuration;
using Robust.Shared.Random;
using AbominationComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationComponent;
using Content.Shared._CMU14.Medical.Core;

namespace Content.Shared._CMU14.Medical.Injuries.Trauma;

public sealed partial class SharedCMUTraumaSystem : EntitySystem
{
    private const float TorsoOrganPassThroughMultiplier = 1.3f;
    private const int XenoTorsoOrganPassThroughMinimumTier = 2;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IRobustRandom _random = default!;

    private EntityQuery<XenoComponent> _xenoQuery;
    private EntityQuery<XenoClawsComponent> _xenoClawsQuery;
    private EntityQuery<AbominationComponent> _abominationQuery;

    private CMUTraumaContactSettings _settings = CMUTraumaContactSettings.Default;

    public override void Initialize()
    {
        base.Initialize();

        _xenoQuery = GetEntityQuery<XenoComponent>();
        _xenoClawsQuery = GetEntityQuery<XenoClawsComponent>();
        _abominationQuery = GetEntityQuery<AbominationComponent>();

        _cfg.OnValueChanged(CMUMedicalCCVars.BoneProjectileHighDamageThreshold, v => _settings = _settings with { BallisticHighDamageThreshold = (FixedPoint2)v }, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.TraumaMeleeHighDamageThreshold, v => _settings = _settings with { MeleeHighDamageThreshold = (FixedPoint2)v }, true);

        _cfg.OnValueChanged(CMUMedicalCCVars.BoneProjectileHeadChance, v => _settings = _settings with { BallisticHeadBoneChance = v }, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.BoneProjectileTorsoChance, v => _settings = _settings with { BallisticTorsoBoneChance = v }, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.BoneProjectileArmChance, v => _settings = _settings with { BallisticArmBoneChance = v }, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.BoneProjectileLegChance, v => _settings = _settings with { BallisticLegBoneChance = v }, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.BoneProjectileOtherChance, v => _settings = _settings with { BallisticOtherBoneChance = v }, true);

        _cfg.OnValueChanged(CMUMedicalCCVars.TraumaBallisticHeadOrganChance, v => _settings = _settings with { BallisticHeadOrganChance = v }, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.TraumaBallisticTorsoOrganChance, v => _settings = _settings with { BallisticTorsoOrganChance = v }, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.TraumaBallisticVascularChance, v => _settings = _settings with { BallisticVascularChance = v }, true);

        _cfg.OnValueChanged(CMUMedicalCCVars.TraumaPierceBoneChance, v => _settings = _settings with { PierceBoneChance = v }, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.TraumaPierceOrganChance, v => _settings = _settings with { PierceOrganChance = v }, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.TraumaPierceVascularChance, v => _settings = _settings with { PierceVascularChance = v }, true);

        _cfg.OnValueChanged(CMUMedicalCCVars.TraumaSlashBoneChance, v => _settings = _settings with { SlashBoneChance = v }, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.TraumaSlashOrganChance, v => _settings = _settings with { SlashOrganChance = v }, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.TraumaSlashVascularChance, v => _settings = _settings with { SlashVascularChance = v }, true);

        _cfg.OnValueChanged(CMUMedicalCCVars.TraumaBluntBoneChance, v => _settings = _settings with { BluntBoneChance = v }, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.TraumaBluntOrganChance, v => _settings = _settings with { BluntOrganChance = v }, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.TraumaBluntVascularChance, v => _settings = _settings with { BluntVascularChance = v }, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.TraumaGroinOrganChanceMultiplier, v => _settings = _settings with { GroinOrganChanceMultiplier = v }, true);
    }

    public CMUTraumaContactResult CreateContactResult(
        BodyPartType partType,
        DamageSpecifier damage,
        bool hasOrgans,
        EntityUid? tool,
        CMUTraumaMechanism? explicitMechanism = null,
        TargetBodyZone? targetZone = null)
    {
        return CreateContactResult(partType, damage, hasOrgans, null, tool, default, explicitMechanism, targetZone);
    }

    public CMUTraumaContactResult CreateContactResult(
        BodyPartType partType,
        DamageSpecifier damage,
        bool hasOrgans,
        EntityUid? origin,
        EntityUid? tool,
        DamageImpact impact = default,
        CMUTraumaMechanism? explicitMechanism = null,
        TargetBodyZone? targetZone = null)
    {
        var mechanism = explicitMechanism ?? InferMechanism(damage, tool);
        impact = ResolveImpact(damage, mechanism, origin, tool, impact);
        var brute = GetTypeAmount(damage, "Blunt") +
                    GetTypeAmount(damage, "Slash") +
                    GetTypeAmount(damage, "Piercing");
        var settings = GetContactSettings(partType, mechanism, origin, tool, targetZone);

        return CMUTraumaContactModel.Create(
            mechanism,
            impact,
            partType,
            brute,
            hasOrgans,
            _random.NextFloat(),
            settings);
    }

    private CMUTraumaContactSettings GetContactSettings(
        BodyPartType partType,
        CMUTraumaMechanism mechanism,
        EntityUid? origin,
        EntityUid? tool,
        TargetBodyZone? targetZone)
    {
        var settings = partType == BodyPartType.Torso && targetZone == TargetBodyZone.GroinPelvis
            ? ApplyGroinOrganChanceBoost(_settings)
            : _settings;

        if (partType != BodyPartType.Torso)
            return settings;

        if (mechanism == CMUTraumaMechanism.Ballistic)
        {
            return settings with
            {
                BallisticOrganPassThrough = MultiplyTorsoPassThrough(settings.BallisticOrganPassThrough),
                HighEnergyOrganPassThrough = MultiplyTorsoPassThrough(settings.HighEnergyOrganPassThrough),
            };
        }

        if (!TryGetXenoSource(origin, tool, out _, out var xeno) ||
            xeno.Tier < XenoTorsoOrganPassThroughMinimumTier)
        {
            return settings;
        }

        return mechanism switch
        {
            CMUTraumaMechanism.Pierce => settings with
            {
                PierceOrganPassThrough = MultiplyTorsoPassThrough(settings.PierceOrganPassThrough),
                HighEnergyOrganPassThrough = MultiplyTorsoPassThrough(settings.HighEnergyOrganPassThrough),
            },
            CMUTraumaMechanism.Slash => settings with
            {
                SlashOrganPassThrough = MultiplyTorsoPassThrough(settings.SlashOrganPassThrough),
                HighEnergyOrganPassThrough = MultiplyTorsoPassThrough(settings.HighEnergyOrganPassThrough),
            },
            CMUTraumaMechanism.Blunt => settings with
            {
                BluntOrganPassThrough = MultiplyTorsoPassThrough(settings.BluntOrganPassThrough),
                HighEnergyOrganPassThrough = MultiplyTorsoPassThrough(settings.HighEnergyOrganPassThrough),
            },
            _ => settings,
        };
    }

    private static CMUTraumaContactSettings ApplyGroinOrganChanceBoost(CMUTraumaContactSettings settings)
    {
        var multiplier = MathF.Max(0f, settings.GroinOrganChanceMultiplier);
        return settings with
        {
            BallisticTorsoOrganChance = settings.BallisticTorsoOrganChance * multiplier,
            PierceOrganChance = settings.PierceOrganChance * multiplier,
            SlashOrganChance = settings.SlashOrganChance * multiplier,
            BluntOrganChance = settings.BluntOrganChance * multiplier,
        };
    }

    private static float MultiplyTorsoPassThrough(float value)
        => value * TorsoOrganPassThroughMultiplier;

    private DamageImpact ResolveImpact(
        DamageSpecifier damage,
        CMUTraumaMechanism mechanism,
        EntityUid? origin,
        EntityUid? tool,
        DamageImpact impact)
    {
        if (!impact.IsSpecified)
            impact = InferImpact(damage, mechanism, tool);

        return ApplySourceTraits(impact, origin, tool);
    }

    private DamageImpact InferImpact(DamageSpecifier damage, CMUTraumaMechanism mechanism, EntityUid? tool)
    {
        if (mechanism == CMUTraumaMechanism.Explosive)
            return DamageImpact.Explosion;

        if (tool is { } toolUid && HasComp<ProjectileComponent>(toolUid))
            return DamageImpact.Projectile;

        return mechanism switch
        {
            CMUTraumaMechanism.Ballistic => DamageImpact.Projectile,
            CMUTraumaMechanism.Slash or CMUTraumaMechanism.Pierce or CMUTraumaMechanism.Blunt => DamageImpact.ForMelee(damage),
            _ => default,
        };
    }

    private DamageImpact ApplySourceTraits(DamageImpact impact, EntityUid? origin, EntityUid? tool)
    {
        if (impact.Delivery != DamageImpactDelivery.Melee ||
            impact.Contact is DamageImpactContact.Burn or DamageImpactContact.Snag or DamageImpactContact.Blast)
        {
            return impact;
        }

        if (impact.Contact == DamageImpactContact.Crush)
            return impact.WithMinimumEnergy(DamageImpactEnergy.High);

        if (TryGetAbominationSource(origin, tool))
        {
            return impact with
            {
                Contact = impact.Contact == DamageImpactContact.Generic ? DamageImpactContact.Slash : impact.Contact,
                Penetration = impact.Penetration == DamageImpactPenetration.Unspecified ||
                              impact.Penetration < DamageImpactPenetration.Medium
                    ? DamageImpactPenetration.Medium
                    : impact.Penetration,
                Energy = impact.Energy == DamageImpactEnergy.Unspecified || impact.Energy < DamageImpactEnergy.High
                    ? DamageImpactEnergy.High
                    : impact.Energy,
            };
        }

        if (!TryGetXenoSource(origin, tool, out var xenoUid, out var xeno))
            return impact;

        var tier = xeno.Tier;
        var clawType = _xenoClawsQuery.TryComp(xenoUid, out var claws)
            ? claws.ClawType
            : XenoClawType.Normal;

        var penetration = tier >= 3 || clawType >= XenoClawType.VerySharp
            ? DamageImpactPenetration.High
            : tier >= 2 || clawType >= XenoClawType.Sharp
                ? DamageImpactPenetration.Medium
                : DamageImpactPenetration.Low;

        return impact with
        {
            Contact = impact.Contact == DamageImpactContact.Generic ? DamageImpactContact.Slash : impact.Contact,
            Penetration = impact.Penetration == DamageImpactPenetration.Unspecified || impact.Penetration < penetration
                ? penetration
                : impact.Penetration,
            Energy = impact.Energy == DamageImpactEnergy.Unspecified || impact.Energy < DamageImpactEnergy.High
                ? DamageImpactEnergy.High
                : impact.Energy,
        };
    }

    private bool TryGetXenoSource(EntityUid? origin, EntityUid? tool, out EntityUid uid, out XenoComponent xeno)
    {
        if (origin is { } originUid && _xenoQuery.TryComp(originUid, out var originXeno))
        {
            uid = originUid;
            xeno = originXeno!;
            return true;
        }

        if (tool is { } toolUid && _xenoQuery.TryComp(toolUid, out var toolXeno))
        {
            uid = toolUid;
            xeno = toolXeno!;
            return true;
        }

        uid = default;
        xeno = default!;
        return false;
    }

    private bool TryGetAbominationSource(EntityUid? origin, EntityUid? tool)
    {
        if (origin is { } originUid && _abominationQuery.HasComp(originUid))
            return true;

        return tool is { } toolUid && _abominationQuery.HasComp(toolUid);
    }

    private CMUTraumaMechanism InferMechanism(DamageSpecifier damage, EntityUid? tool)
    {
        if (tool is { } toolUid && HasComp<ProjectileComponent>(toolUid))
            return CMUTraumaMechanism.Ballistic;

        var blunt = GetTypeAmount(damage, "Blunt");
        var slash = GetTypeAmount(damage, "Slash");
        var piercing = GetTypeAmount(damage, "Piercing");

        if (piercing > FixedPoint2.Zero && piercing >= slash && piercing >= blunt)
            return CMUTraumaMechanism.Pierce;
        if (slash > FixedPoint2.Zero && slash >= blunt)
            return CMUTraumaMechanism.Slash;
        if (blunt > FixedPoint2.Zero)
            return CMUTraumaMechanism.Blunt;

        return CMUTraumaMechanism.Generic;
    }

    private static FixedPoint2 GetTypeAmount(DamageSpecifier damage, string type)
        => damage.DamageDict.TryGetValue(type, out var amount) && amount > FixedPoint2.Zero
            ? amount
            : FixedPoint2.Zero;
}
