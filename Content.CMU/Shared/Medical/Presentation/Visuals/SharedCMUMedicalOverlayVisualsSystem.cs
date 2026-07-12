using System;
using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Diagnostics.Examine;
using Content.Shared._CMU14.Medical.Treatment.FirstAid;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.Shared._CMU14.Medical.Presentation.Visuals;

public sealed partial class SharedCMUMedicalOverlayVisualsSystem : EntitySystem
{
    [Dependency] private CMUMedicalExamineProjectionSystem _examineProjection = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private CMUWoundLedgerSystem _woundLedger = default!;

    private const int SnapshotLayerCount = 10;

    private const CMUMedicalChangeFlags ProjectionChanges =
        CMUMedicalChangeFlags.Anatomy |
        CMUMedicalChangeFlags.Visuals;

    private static readonly byte[] DamageThresholds = [10, 20, 30, 50, 70, 100];

    private static readonly HumanoidVisualLayers[] SnapshotLayers = [
        HumanoidVisualLayers.Chest,
        HumanoidVisualLayers.Head,
        HumanoidVisualLayers.RArm,
        HumanoidVisualLayers.LArm,
        HumanoidVisualLayers.RHand,
        HumanoidVisualLayers.LHand,
        HumanoidVisualLayers.RLeg,
        HumanoidVisualLayers.LLeg,
        HumanoidVisualLayers.RFoot,
        HumanoidVisualLayers.LFoot,
    ];

    private readonly Dictionary<HumanoidVisualLayers, MedicalOverlayPartVisualBuilder> _builders = new(SnapshotLayerCount);
    private readonly List<CMUMedicalOverlayPartVisual> _parts = new(SnapshotLayerCount);
    private readonly List<CMUMedicalExaminePartProjection> _examineParts = new(SnapshotLayerCount);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUHumanMedicalComponent, CMUMedicalChangedEvent>(OnMedicalChanged);
    }

    private void OnMedicalChanged(
        Entity<CMUHumanMedicalComponent> ent,
        ref CMUMedicalChangedEvent args)
    {
        if ((args.Changes & ProjectionChanges) == CMUMedicalChangeFlags.None)
            return;

        RefreshBody(ent.Owner);
    }

    private void RefreshBody(EntityUid body)
    {
        if (_net.IsClient)
            return;

        if (!HasComp<CMUHumanMedicalComponent>(body))
            return;

        _builders.Clear();
        _examineParts.Clear();
        var totalBrute = FixedPoint2.Zero;
        var totalBurn = FixedPoint2.Zero;
        foreach (var (partUid, part) in _medicalIndex.GetBodyParts(body))
        {
            var bandaged = false;
            var partBrute = FixedPoint2.Zero;
            var partBurn = FixedPoint2.Zero;
            if (TryComp<BodyPartWoundComponent>(partUid, out var wound))
            {
                var entries = _woundLedger.GetEntries(wound);
                var visibleWounds = new List<CMUMedicalVisibleWound>(entries.Count);
                foreach (var entry in entries)
                {
                    visibleWounds.Add(new CMUMedicalVisibleWound(
                        entry.Wound.Type,
                        entry.Size,
                        entry.Wound.Damage,
                        entry.Mechanism,
                        entry.Wound.Treated,
                        entry.Cleanup));

                    bandaged |= entry.Bandages > 0;
                    var remaining = entry.Wound.Damage - entry.Wound.Healed;
                    if (remaining <= FixedPoint2.Zero)
                        continue;

                    if (entry.Wound.Type == WoundType.Burn)
                        totalBurn += remaining;
                    else
                        totalBrute += remaining;

                    switch (entry.Wound.Type)
                    {
                        case WoundType.Brute:
                            partBrute += remaining;
                            break;
                        case WoundType.Burn:
                            partBurn += remaining;
                            break;
                    }
                }

                if (visibleWounds.Count > 0 || wound.ExternalBleeding != ExternalBleedTier.None)
                {
                    _examineParts.Add(new CMUMedicalExaminePartProjection(
                        part.PartType,
                        part.Symmetry,
                        visibleWounds,
                        wound.ExternalBleeding));
                }
            }

            if (part.ToHumanoidLayers() is not { } layer)
                continue;

            var splinted = HasComp<CMUSplintedComponent>(partUid) || HasComp<CMUCastComponent>(partUid);
            if (bandaged || splinted)
                AddTreatmentVisual(_builders, layer, bandaged, splinted, partUid.GetHashCode());

            var bruteDamageLevel = PickDamageLevel(partBrute.Float());
            var burnDamageLevel = PickDamageLevel(partBurn.Float());
            if (bruteDamageLevel > 0 || burnDamageLevel > 0)
                AddDamageVisual(_builders, ToDamageLayer(layer), bruteDamageLevel, burnDamageLevel, partUid.GetHashCode());
        }

        _examineProjection.Replace(body, _examineParts, totalBrute, totalBurn);

        BuildVisuals(_builders, _parts);
        if (_parts.Count == 0)
        {
            if (HasComp<CMUMedicalOverlayVisualsComponent>(body))
                RemComp<CMUMedicalOverlayVisualsComponent>(body);

            return;
        }

        var visuals = EnsureComp<CMUMedicalOverlayVisualsComponent>(body);
        if (SameVisuals(visuals.Parts, _parts))
            return;

        visuals.Parts.Clear();
        visuals.Parts.AddRange(_parts);
        Dirty(body, visuals);
    }

    private static byte PickDamageLevel(float damage)
    {
        var threshold = (byte) 0;
        foreach (var candidate in DamageThresholds)
        {
            if (damage < candidate)
                break;

            threshold = candidate;
        }

        return threshold;
    }

    private static HumanoidVisualLayers ToDamageLayer(HumanoidVisualLayers layer)
    {
        return layer switch
        {
            HumanoidVisualLayers.LHand => HumanoidVisualLayers.LArm,
            HumanoidVisualLayers.RHand => HumanoidVisualLayers.RArm,
            HumanoidVisualLayers.LFoot => HumanoidVisualLayers.LLeg,
            HumanoidVisualLayers.RFoot => HumanoidVisualLayers.RLeg,
            _ => layer,
        };
    }

    private static void AddTreatmentVisual(
        Dictionary<HumanoidVisualLayers, MedicalOverlayPartVisualBuilder> builders,
        HumanoidVisualLayers layer,
        bool bandaged,
        bool splinted,
        int variantSeed)
    {
        builders.TryGetValue(layer, out var builder);
        builder.Bandaged |= bandaged;
        builder.Splinted |= splinted;
        builder.VariantSeed = builder.VariantSeed == 0 ? variantSeed : builder.VariantSeed;
        builders[layer] = builder;
    }

    private static void AddDamageVisual(
        Dictionary<HumanoidVisualLayers, MedicalOverlayPartVisualBuilder> builders,
        HumanoidVisualLayers layer,
        byte bruteDamageLevel,
        byte burnDamageLevel,
        int variantSeed)
    {
        builders.TryGetValue(layer, out var builder);
        if (bruteDamageLevel > builder.BruteDamageLevel)
            builder.BruteDamageLevel = bruteDamageLevel;

        if (burnDamageLevel > builder.BurnDamageLevel)
            builder.BurnDamageLevel = burnDamageLevel;

        builder.VariantSeed = builder.VariantSeed == 0 ? variantSeed : builder.VariantSeed;
        builders[layer] = builder;
    }

    private static void BuildVisuals(
        Dictionary<HumanoidVisualLayers, MedicalOverlayPartVisualBuilder> builders,
        List<CMUMedicalOverlayPartVisual> parts)
    {
        parts.Clear();
        foreach (var layer in SnapshotLayers)
        {
            if (!builders.TryGetValue(layer, out var builder))
                continue;

            parts.Add(new CMUMedicalOverlayPartVisual(
                layer,
                builder.Bandaged,
                builder.Splinted,
                builder.BruteDamageLevel,
                builder.BurnDamageLevel,
                builder.VariantSeed));
        }
    }

    private static bool SameVisuals(
        List<CMUMedicalOverlayPartVisual> current,
        List<CMUMedicalOverlayPartVisual> next)
    {
        if (current.Count != next.Count)
            return false;

        for (var i = 0; i < current.Count; i++)
        {
            if (current[i] != next[i])
                return false;
        }

        return true;
    }

    private struct MedicalOverlayPartVisualBuilder
    {
        public bool Bandaged;
        public bool Splinted;
        public byte BruteDamageLevel;
        public byte BurnDamageLevel;
        public int VariantSeed;
    }
}
