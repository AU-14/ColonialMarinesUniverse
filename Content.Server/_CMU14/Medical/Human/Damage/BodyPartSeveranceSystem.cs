using System.Collections.Generic;
using System.Numerics;
using Content.Server.StatusEffectNew;
using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Damage.Events;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CMU14.Medical.Human.Damage;

/// <summary>
///     Applies ledger-backed traumatic severance once a body-part severance
///     event is raised by damage, xeno attacks, or other limb-removal systems.
/// </summary>
public sealed partial class BodyPartSeveranceSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private SharedHumanMedicalSystem _humanMedical = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly HashSet<EntityUid> _explicitSeveranceParts = new();

    private static readonly FixedPoint2 StumpBleedRate = FixedPoint2.New(4);

    private static readonly SoundSpecifier SeveranceSound =
        new SoundPathSpecifier("/Audio/_CMU14/Medical/crackandbleed.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyPartComponent, BodyPartSeveredEvent>(OnPartSevered);
        SubscribeLocalEvent<BodyComponent, BodyPartRemovedEvent>(OnBodyPartRemoved);
    }

    private void OnPartSevered(Entity<BodyPartComponent> ent, ref BodyPartSeveredEvent args)
    {
        if (!CanSever(args.Type))
            return;

        var isSynth = HasComp<SynthComponent>(args.Body);
        HumanMedicalComponent? medical = null;
        if (!isSynth)
            TryComp(args.Body, out medical);

        if (medical == null &&
            !isSynth)
        {
            return;
        }

        var symmetry = ent.Comp.Symmetry;
        if (medical != null &&
            IsAlreadyMissing(medical, args.Type, symmetry))
        {
            return;
        }

        _explicitSeveranceParts.Add(args.Part);
        try
        {
            if (!DetachPart(args.Part))
                return;
        }
        finally
        {
            _explicitSeveranceParts.Remove(args.Part);
        }

        if (medical != null &&
            !TryApplyLedgerSeverance((args.Body, medical), args.Part, args.Type, symmetry))
        {
            return;
        }

        FlingPartFromBody(args.Body, args.Part);
        HideHumanoidLimbLayer(args.Body, args.Type, symmetry);
        FinishSeverance(args.Body, args.Part, args.Type);
    }

    private void OnBodyPartRemoved(Entity<BodyComponent> body, ref BodyPartRemovedEvent args)
    {
        var isSynth = HasComp<SynthComponent>(body.Owner);
        HumanMedicalComponent? medical = null;
        if (!isSynth)
            TryComp(body.Owner, out medical);

        if (medical == null &&
            !isSynth)
        {
            return;
        }

        var part = args.Part.Owner;
        if (_explicitSeveranceParts.Contains(part))
            return;

        var partType = args.Part.Comp.PartType;
        var symmetry = args.Part.Comp.Symmetry;
        if (!CanSever(partType) ||
            medical != null &&
            (IsAlreadyMissing(medical, partType, symmetry) ||
             !TryApplyLedgerSeverance((body.Owner, medical), part, partType, symmetry)))
        {
            return;
        }

        FinishSeverance(body.Owner, part, partType);
    }

    public static MedicalTransaction CreateLedgerSeveranceTransaction(
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        var region = RegionForPart(type, symmetry);
        return LimbLossRules.CreateTraumaticSeverance(
            region,
            BleedKind.Stump,
            StumpBleedRate,
            BleedFlags.Arterial);
    }

    private bool TryApplyLedgerSeverance(
        Entity<HumanMedicalComponent> body,
        EntityUid part,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        var transaction = CreateLedgerSeveranceTransaction(type, symmetry);
        if (transaction.Count == 0)
            return false;

        var result = _humanMedical.ApplyTransaction(body, transaction);
        return result.Applied;
    }

    private void FinishSeverance(EntityUid body, EntityUid part, BodyPartType type)
    {
        ApplyMissingLimbStatus(body, part, type);
        _audio.PlayPvs(SeveranceSound, body);

        var ev = new BodyPartSeveranceAppliedEvent(body, part, type);
        RaiseLocalEvent(ref ev);
    }

    private void FlingPartFromBody(EntityUid body, EntityUid part)
    {
        // compensateFriction:true so the part lands at the target instead
        // of sliding indefinitely off-grid (prior speed-8 fling was
        // overshooting the visible map).
        _transform.SetCoordinates(part, Transform(body).Coordinates);
        _transform.AttachToGridOrMap(part);

        var angle = _random.NextFloat(0f, MathF.Tau);
        var distance = _random.NextFloat(1.0f, 2.0f);
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
        _throwing.TryThrow(part, direction, baseThrowSpeed: 4f, doSpin: true, compensateFriction: true);
    }

    private void HideHumanoidLimbLayer(EntityUid body, BodyPartType type, BodyPartSymmetry symmetry)
    {
        // SS14's body-part graph and HumanoidAppearance are independent — the
        // marine sprite still draws the limb layer until we explicitly hide it.
        if (!HasComp<HumanoidAppearanceComponent>(body))
            return;

        if (LayerForPart(type, symmetry) is not { } layer)
            return;

        _humanoid.SetLayerVisibility(body, layer, visible: false);
    }

    private static HumanoidVisualLayers? LayerForPart(BodyPartType type, BodyPartSymmetry symmetry) =>
        (type, symmetry) switch
        {
            (BodyPartType.Head, BodyPartSymmetry.None) => HumanoidVisualLayers.Head,
            (BodyPartType.Arm, BodyPartSymmetry.Left) => HumanoidVisualLayers.LArm,
            (BodyPartType.Arm, BodyPartSymmetry.Right) => HumanoidVisualLayers.RArm,
            (BodyPartType.Hand, BodyPartSymmetry.Left) => HumanoidVisualLayers.LHand,
            (BodyPartType.Hand, BodyPartSymmetry.Right) => HumanoidVisualLayers.RHand,
            (BodyPartType.Leg, BodyPartSymmetry.Left) => HumanoidVisualLayers.LLeg,
            (BodyPartType.Leg, BodyPartSymmetry.Right) => HumanoidVisualLayers.RLeg,
            (BodyPartType.Foot, BodyPartSymmetry.Left) => HumanoidVisualLayers.LFoot,
            (BodyPartType.Foot, BodyPartSymmetry.Right) => HumanoidVisualLayers.RFoot,
            _ => null,
        };

    private bool IsLocked(BodyPartType type) => type switch
    {
        BodyPartType.Head => _cfg.GetCVar(CMUMedicalCCVars.SeveranceHeadDisabled),
        BodyPartType.Torso => _cfg.GetCVar(CMUMedicalCCVars.SeveranceTorsoDisabled),
        _ => false,
    };

    private bool CanSever(BodyPartType type)
    {
        return _cfg.GetCVar(CMUMedicalCCVars.Enabled) &&
            _cfg.GetCVar(CMUMedicalCCVars.BodyPartEnabled) &&
            !IsLocked(type);
    }

    private static bool IsAlreadyMissing(
        HumanMedicalComponent medical,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        var region = RegionForPart(type, symmetry);
        return region == BodyRegion.None ||
            HumanMedicalLedger.GetRegion(medical, region).Presence == LimbPresence.Missing;
    }

    private bool DetachPart(EntityUid part)
    {
        if (!_containers.TryGetContainingContainer((part, null, null), out var container))
            return false;

        return _containers.Remove(part, container);
    }

    private void ApplyMissingLimbStatus(EntityUid body, EntityUid part, BodyPartType type)
    {
        if (!TryComp<BodyPartComponent>(part, out var partComp))
            return;

        if (StatusForPart(type, partComp.Symmetry) is not { } statusProto)
            return;

        _status.TrySetStatusEffectDuration(body, statusProto, duration: null);
    }

    private static EntProtoId? StatusForPart(BodyPartType type, BodyPartSymmetry symmetry) =>
        (type, symmetry) switch
        {
            (BodyPartType.Arm, BodyPartSymmetry.Left) => "StatusEffectCMUMissingArmLeft",
            (BodyPartType.Arm, BodyPartSymmetry.Right) => "StatusEffectCMUMissingArmRight",
            (BodyPartType.Hand, BodyPartSymmetry.Left) => "StatusEffectCMUMissingHandLeft",
            (BodyPartType.Hand, BodyPartSymmetry.Right) => "StatusEffectCMUMissingHandRight",
            (BodyPartType.Leg, BodyPartSymmetry.Left) => "StatusEffectCMUMissingLegLeft",
            (BodyPartType.Leg, BodyPartSymmetry.Right) => "StatusEffectCMUMissingLegRight",
            (BodyPartType.Foot, BodyPartSymmetry.Left) => "StatusEffectCMUMissingFootLeft",
            (BodyPartType.Foot, BodyPartSymmetry.Right) => "StatusEffectCMUMissingFootRight",
            _ => null,
        };

    private static BodyRegion RegionForPart(BodyPartType type, BodyPartSymmetry symmetry) =>
        (type, symmetry) switch
        {
            (BodyPartType.Arm, BodyPartSymmetry.Left) => BodyRegion.LeftArm,
            (BodyPartType.Arm, BodyPartSymmetry.Right) => BodyRegion.RightArm,
            (BodyPartType.Hand, BodyPartSymmetry.Left) => BodyRegion.LeftHand,
            (BodyPartType.Hand, BodyPartSymmetry.Right) => BodyRegion.RightHand,
            (BodyPartType.Leg, BodyPartSymmetry.Left) => BodyRegion.LeftLeg,
            (BodyPartType.Leg, BodyPartSymmetry.Right) => BodyRegion.RightLeg,
            (BodyPartType.Foot, BodyPartSymmetry.Left) => BodyRegion.LeftFoot,
            (BodyPartType.Foot, BodyPartSymmetry.Right) => BodyRegion.RightFoot,
            (BodyPartType.Head, BodyPartSymmetry.None) => BodyRegion.Head,
            _ => BodyRegion.None,
        };
}
