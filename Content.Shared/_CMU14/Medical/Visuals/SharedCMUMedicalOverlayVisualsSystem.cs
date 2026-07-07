using System.Collections.Generic;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Items;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._CMU14.Medical.Wounds.Events;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.Shared._CMU14.Medical.Visuals;

public sealed partial class SharedCMUMedicalOverlayVisualsSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private INetManager _net = default!;

    private static readonly byte[] DamageThresholds = [10, 20, 30, 50, 70, 100];

    private static readonly HumanoidVisualLayers[] SnapshotLayers =
    [
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

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyPartWoundAppliedEvent>(OnWoundApplied);
        SubscribeLocalEvent<WoundTreatedEvent>(OnWoundTreated);
        SubscribeLocalEvent<BodyPartWoundsChangedEvent>(OnWoundsChanged);
        SubscribeLocalEvent<CMUSplintChangedEvent>(OnSplintChanged);
        SubscribeLocalEvent<CMUCastChangedEvent>(OnCastChanged);
        SubscribeLocalEvent<BodyComponent, BodyPartAddedEvent>(OnBodyPartChanged);
        SubscribeLocalEvent<BodyComponent, BodyPartRemovedEvent>(OnBodyPartChanged);
    }

    private void OnWoundApplied(ref BodyPartWoundAppliedEvent args)
    {
        RefreshBody(args.Body);
    }

    private void OnWoundTreated(ref WoundTreatedEvent args)
    {
        RefreshBody(args.Body);
    }

    private void OnWoundsChanged(ref BodyPartWoundsChangedEvent args)
    {
        RefreshPart(args.Part);
    }

    private void OnSplintChanged(ref CMUSplintChangedEvent args)
    {
        RefreshPart(args.Part);
    }

    private void OnCastChanged(ref CMUCastChangedEvent args)
    {
        RefreshPart(args.Part);
    }

    private void OnBodyPartChanged<TEvent>(Entity<BodyComponent> ent, ref TEvent args)
    {
        RefreshBody(ent.Owner);
    }

    private void RefreshPart(EntityUid part)
    {
        if (!TryComp<BodyPartComponent>(part, out var bodyPart) || bodyPart.Body is not { } body)
            return;

        RefreshBody(body);
    }

    private void RefreshBody(EntityUid body)
    {
        if (_net.IsClient)
            return;

        if (!HasComp<CMUHumanMedicalComponent>(body))
            return;

        var builders = new Dictionary<HumanoidVisualLayers, MedicalOverlayPartVisualBuilder>();
        foreach (var (partUid, part) in _body.GetBodyChildren(body))
        {
            if (part.ToHumanoidLayers() is not { } layer)
                continue;

            var bandaged = HasBandageOverlay(partUid);
            var splinted = HasComp<CMUSplintedComponent>(partUid) || HasComp<CMUCastComponent>(partUid);
            if (bandaged || splinted)
                AddTreatmentVisual(builders, layer, bandaged, splinted, partUid.GetHashCode());

            var (bruteDamageLevel, burnDamageLevel) = GetDamageLevels(partUid);
            if (bruteDamageLevel > 0 || burnDamageLevel > 0)
                AddDamageVisual(builders, ToDamageLayer(layer), bruteDamageLevel, burnDamageLevel, partUid.GetHashCode());
        }

        var parts = BuildVisuals(builders);
        if (parts.Count == 0)
        {
            if (HasComp<CMUMedicalOverlayVisualsComponent>(body))
                RemComp<CMUMedicalOverlayVisualsComponent>(body);

            return;
        }

        var visuals = EnsureComp<CMUMedicalOverlayVisualsComponent>(body);
        if (SameVisuals(visuals.Parts, parts))
            return;

        visuals.Parts.Clear();
        visuals.Parts.AddRange(parts);
        Dirty(body, visuals);
    }

    private bool HasBandageOverlay(EntityUid part)
    {
        if (!TryComp<BodyPartWoundComponent>(part, out var wound))
            return false;

        foreach (var woundBandages in wound.Bandages)
        {
            if (woundBandages > 0)
                return true;
        }

        return false;
    }

    private (byte Brute, byte Burn) GetDamageLevels(EntityUid part)
    {
        if (!TryComp<BodyPartWoundComponent>(part, out var wound))
            return (0, 0);

        var brute = 0f;
        var burn = 0f;
        foreach (var entry in wound.Wounds)
        {
            var remaining = entry.Damage - entry.Healed;
            if (remaining <= FixedPoint2.Zero)
                continue;

            switch (entry.Type)
            {
                case WoundType.Brute:
                    brute += remaining.Float();
                    break;
                case WoundType.Burn:
                    burn += remaining.Float();
                    break;
            }
        }

        return (PickDamageLevel(brute), PickDamageLevel(burn));
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

    private static List<CMUMedicalOverlayPartVisual> BuildVisuals(
        Dictionary<HumanoidVisualLayers, MedicalOverlayPartVisualBuilder> builders)
    {
        var parts = new List<CMUMedicalOverlayPartVisual>(builders.Count);
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

        return parts;
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
