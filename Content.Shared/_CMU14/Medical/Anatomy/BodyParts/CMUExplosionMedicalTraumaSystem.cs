using System.Collections.Generic;
using System.Numerics;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Injuries.Shrapnel;
using Content.Shared._CMU14.Medical.Injuries.Trauma;
using Content.Shared._RMC14.Explosion;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Shared._CMU14.Medical.Anatomy.BodyParts;

public sealed partial class CMUExplosionMedicalTraumaSystem : EntitySystem
{
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedBodyPartHealthSystem _partHealth = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedCMUShrapnelSystem _shrapnel = default!;

    private const float ExposureFalloffTiles = 9f;
    private const float FullExposureDamage = 100f;
    private const float MinimumNormalDamageMultiplier = 0.65f;
    private const float MaximumNormalDamageMultiplier = 1.35f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUHumanMedicalComponent, ExplosionReceivedEvent>(
            OnExplosionReceived,
            before: [typeof(SharedRMCExplosionSystem)]);
    }

    private void OnExplosionReceived(Entity<CMUHumanMedicalComponent> ent, ref ExplosionReceivedEvent args)
    {
        if (args.Damage.GetTotal() <= FixedPoint2.Zero)
            return;

        var exposure = ComputeExposure(ent.Owner, args.Epicenter, args.Damage.GetTotal().Float());
        ApplyNormalDamageCorrection(ent.Owner, args.Damage, exposure);

        var weightedParts = BuildWeightedParts(ent.Owner, args.Epicenter);
        if (weightedParts.Count == 0)
            return;

        var propagation = 0.35f + exposure * 0.55f;
        foreach (var weighted in weightedParts)
        {
            var scale = propagation * weighted.Weight;
            if (scale <= 0f)
                continue;

            _partHealth.TryApplyPartDamage(
                ent.Owner,
                weighted.Part,
                args.Damage,
                scale,
                mechanism: CMUTraumaMechanism.Explosive,
                impact: DamageImpact.Explosion);
        }

        _shrapnel.TryApplyExplosionShrapnel(ent.Owner, args.Explosion, exposure, weightedParts);
    }

    private void ApplyNormalDamageCorrection(EntityUid body, DamageSpecifier damage, float exposure)
    {
        var multiplier = Math.Clamp(
            MinimumNormalDamageMultiplier + exposure * (MaximumNormalDamageMultiplier - MinimumNormalDamageMultiplier),
            MinimumNormalDamageMultiplier,
            MaximumNormalDamageMultiplier);
        var correction = multiplier - 1f;

        if (MathF.Abs(correction) < 0.01f)
            return;

        _damageable.TryChangeDamage(body, damage * correction, ignoreResistances: true);
    }

    private float ComputeExposure(EntityUid body, MapCoordinates epicenter, float totalDamage)
    {
        var damageFactor = Math.Clamp(totalDamage / FullExposureDamage, 0.2f, 1f);
        var rangeFactor = 1f;

        var bodyCoordinates = _transform.GetMapCoordinates(body);
        if (bodyCoordinates.MapId == epicenter.MapId)
        {
            var distance = (bodyCoordinates.Position - epicenter.Position).Length();
            rangeFactor = Math.Clamp(1f - distance / ExposureFalloffTiles, 0.15f, 1f);
        }

        return Math.Clamp(damageFactor * 0.35f + rangeFactor * 0.65f, 0.15f, 1f);
    }

    private List<SharedCMUShrapnelSystem.WeightedBodyPart> BuildWeightedParts(EntityUid body, MapCoordinates epicenter)
    {
        var rawParts = new List<SharedCMUShrapnelSystem.WeightedBodyPart>();
        var totalWeight = 0f;
        var hasDirection = TryGetBlastDirection(body, epicenter, out var facingDot, out var lateralSide);

        foreach (var (partUid, part) in _medicalIndex.GetBodyParts(body))
        {
            var weight = GetBaseWeight(part.PartType) *
                         GetOrientationMultiplier(part.PartType, part.Symmetry, hasDirection, facingDot, lateralSide);
            if (weight <= 0f)
                continue;

            rawParts.Add(new SharedCMUShrapnelSystem.WeightedBodyPart(partUid, part.PartType, part.Symmetry, weight));
            totalWeight += weight;
        }

        if (totalWeight <= 0f)
            return rawParts;

        for (var i = 0; i < rawParts.Count; i++)
        {
            var part = rawParts[i];
            rawParts[i] = part with { Weight = part.Weight / totalWeight };
        }

        rawParts.Sort(static (a, b) => b.Weight.CompareTo(a.Weight));
        return rawParts;
    }

    private bool TryGetBlastDirection(
        EntityUid body,
        MapCoordinates epicenter,
        out float facingDot,
        out BodyPartSymmetry lateralSide)
    {
        facingDot = 0f;
        lateralSide = BodyPartSymmetry.None;

        var bodyCoordinates = _transform.GetMapCoordinates(body);
        if (bodyCoordinates.MapId != epicenter.MapId)
            return false;

        var toBlast = epicenter.Position - bodyCoordinates.Position;
        if (toBlast.LengthSquared() < 0.001f)
            return false;

        toBlast = Vector2.Normalize(toBlast);
        var forward = _transform.GetWorldRotation(body).ToWorldVec();
        facingDot = Math.Clamp(Vector2.Dot(forward, toBlast), -1f, 1f);

        var lateral = forward.X * toBlast.Y - forward.Y * toBlast.X;
        lateralSide = lateral >= 0f ? BodyPartSymmetry.Left : BodyPartSymmetry.Right;
        return true;
    }

    private static float GetBaseWeight(BodyPartType type) => type switch
    {
        BodyPartType.Torso => 0.38f,
        BodyPartType.Head => 0.12f,
        BodyPartType.Arm => 0.14f,
        BodyPartType.Hand => 0.04f,
        BodyPartType.Leg => 0.11f,
        BodyPartType.Foot => 0.03f,
        BodyPartType.Tail => 0.05f,
        BodyPartType.Other => 0.02f,
        _ => 0f,
    };

    private static float GetOrientationMultiplier(
        BodyPartType type,
        BodyPartSymmetry symmetry,
        bool hasDirection,
        float facingDot,
        BodyPartSymmetry lateralSide)
    {
        if (!hasDirection)
            return 1f;

        if (facingDot > 0.35f)
        {
            return type switch
            {
                BodyPartType.Head => 1.35f,
                BodyPartType.Torso => 1.2f,
                BodyPartType.Arm or BodyPartType.Hand => 1.1f,
                _ => 1f,
            };
        }

        if (facingDot < -0.35f)
        {
            return type switch
            {
                BodyPartType.Head => 0.55f,
                BodyPartType.Torso => 1.15f,
                BodyPartType.Arm or BodyPartType.Hand => 0.9f,
                BodyPartType.Leg or BodyPartType.Foot => 0.85f,
                _ => 1f,
            };
        }

        if (symmetry == lateralSide)
        {
            return type switch
            {
                BodyPartType.Arm or BodyPartType.Hand => 1.6f,
                BodyPartType.Leg or BodyPartType.Foot => 1.35f,
                _ => 1f,
            };
        }

        if (symmetry is not BodyPartSymmetry.None)
        {
            return type switch
            {
                BodyPartType.Arm or BodyPartType.Hand => 0.65f,
                BodyPartType.Leg or BodyPartType.Foot => 0.75f,
                _ => 1f,
            };
        }

        return type switch
        {
            BodyPartType.Head => 0.8f,
            BodyPartType.Torso => 1.1f,
            _ => 1f,
        };
    }
}
