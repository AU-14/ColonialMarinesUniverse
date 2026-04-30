using System.Numerics;
using Content.Server.StatusEffectNew;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.BodyPart.Events;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
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

namespace Content.Server._CMU14.Medical.BodyPart;

/// <summary>
///     Head and torso safety locks are enforced upstream in
///     <c>SharedBodyPartHealthSystem.IsSeveranceLocked</c> — the system never
///     raises the event for a locked part — so this consumer doesn't need to
///     re-check those CCVars to be correct. The redundant check below is a
///     defence-in-depth guard against future callers.
/// </summary>
public sealed class BodyPartSeveranceSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    private ISawmill _sawmill = default!;

    private static readonly ProtoId<DamageTypePrototype> Bloodloss = "Bloodloss";
    private const float StumpBleedDamage = 30f;
    private static readonly SoundSpecifier SeveranceSound =
        new SoundPathSpecifier("/Audio/_CMU14/Medical/crackandbleed.ogg");

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("cmu.medical.severance");
        SubscribeLocalEvent<BodyPartHealthComponent, BodyPartSeveredEvent>(OnPartSevered);
    }

    private void OnPartSevered(Entity<BodyPartHealthComponent> ent, ref BodyPartSeveredEvent args)
    {
        _sawmill.Info($"Severance triggered: body={ToPrettyString(args.Body)} part={ToPrettyString(args.Part)} type={args.Type}");

        if (!_cfg.GetCVar(CMUMedicalCCVars.Enabled) || !_cfg.GetCVar(CMUMedicalCCVars.BodyPartEnabled))
        {
            _sawmill.Info("  → bailed: layer CCVars off.");
            return;
        }

        if (IsLocked(args.Type))
        {
            _sawmill.Info($"  → bailed: severance locked for {args.Type}.");
            return;
        }

        if (!HasComp<CMUHumanMedicalComponent>(args.Body))
        {
            _sawmill.Info("  → bailed: target lacks CMUHumanMedicalComponent.");
            return;
        }

        var symmetry = TryComp<BodyPartComponent>(args.Part, out var partComp)
            ? partComp.Symmetry
            : BodyPartSymmetry.None;

        if (!DetachPart(args.Part))
        {
            _sawmill.Warning("  → DetachPart failed (no containing container).");
            return;
        }

        FlingPartFromBody(args.Body, args.Part);
        HideHumanoidLimbLayer(args.Body, args.Type, symmetry);
        ApplyStumpBleed(args.Body);
        ApplyMissingLimbStatus(args.Body, args.Part, args.Type);
        _audio.PlayPvs(SeveranceSound, args.Body);
        _sawmill.Info($"  → severed {args.Type}/{symmetry} OK.");
    }

    private void FlingPartFromBody(EntityUid body, EntityUid part)
    {
        // compensateFriction:true so the part lands at the target instead
        // of sliding indefinitely off-grid (prior speed-8 fling was
        // overshooting the visible map).
        if (!TryComp<TransformComponent>(body, out var bodyXform))
            return;

        _transform.SetCoordinates(part, bodyXform.Coordinates);
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

    private bool DetachPart(EntityUid part)
    {
        if (!_containers.TryGetContainingContainer((part, null, null), out var container))
            return false;

        return _containers.Remove(part, container);
    }

    private void ApplyStumpBleed(EntityUid body)
    {
        if (!_proto.TryIndex(Bloodloss, out var bloodloss))
            return;

        if (!TryComp<DamageableComponent>(body, out var damageable))
            return;

        // AddDamage bypasses BeforeDamageChangedEvent: RMC's MaxDamageComponent
        // cancels TryChangeDamage once the marine has hit the cap (the same
        // hit that triggered severance can be that cap). Stump bleed is a
        // side-channel injury, not a TotalDamage contributor.
        var bleed = new DamageSpecifier(bloodloss, FixedPoint2.New(StumpBleedDamage));
        _damageable.AddDamage(body, damageable, bleed);
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
}
