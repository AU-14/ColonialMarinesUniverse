using System.Numerics;
using Content.Shared.StatusEffectNew;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Presentation.Visuals;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Medical.Anatomy.BodyParts;

/// <summary>
///     Head and torso safety locks are enforced upstream in
///     <c>SharedBodyPartHealthSystem.IsSeveranceLocked</c> — the system never
///     raises the event for a locked part — so this consumer doesn't need to
///     re-check those CCVars to be correct. The redundant check below is a
///     defence-in-depth guard against future callers.
/// </summary>
public sealed partial class BodyPartSeveranceSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DetachableOrganSystem _detachableOrgan = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedHideableHumanoidLayersSystem _hideableLayers = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private OrganRelationSystem _organRelation = default!;
    private static readonly ProtoId<DamageTypePrototype> Bloodloss = "Bloodloss";
    private const float StumpBleedDamage = 30f;
    private static readonly SoundSpecifier SeveranceSound =
        new SoundPathSpecifier("/Audio/CMU14/Medical/crackandbleed.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyPartHealthComponent, BodyPartSeverAttemptEvent>(OnPartSeverAttempt);
    }

    public void ClearMissingPartStatus(EntityUid body, BodyPartType type, BodyPartSymmetry symmetry)
    {
        if (TerminatingOrDeleted(body) ||
            StatusForPart(type, symmetry) is not { } status ||
            !_status.TryGetStatusEffect(body, status, out var effect) ||
            TerminatingOrDeleted(effect.Value))
            return;

        // Attachment commits the end of this exact missing-site source. Queued
        // removal would let a same-tick sever renew an entity still due for deletion.
        // This server system preserves the normal status/container removal callbacks.
        Del(effect.Value);
    }

    private void OnPartSeverAttempt(Entity<BodyPartHealthComponent> ent, ref BodyPartSeverAttemptEvent args)
    {
        if (args.Cancelled || args.Succeeded || args.Part != ent.Owner ||
            !TryComp<BodyPartComponent>(args.Part, out var partComp) ||
            partComp.Body != args.Body || partComp.PartType != args.Type)
            return;

        if (!_cfg.GetCVar(CMUMedicalCCVars.Enabled) || !_cfg.GetCVar(CMUMedicalCCVars.BodyPartEnabled))
        {
            return;
        }

        if (IsLocked(args.Type))
        {
            return;
        }

        if (!HasComp<CMUHumanMedicalComponent>(args.Body))
        {
            return;
        }

        var detachedParts = new List<(EntityUid Part, BodyPartType Type, BodyPartSymmetry Symmetry)>();
        detachedParts.Add((args.Part, args.Type, partComp.Symmetry));
        foreach (var child in _organRelation.AllChildren(args.Part))
        {
            if (TryComp<BodyPartComponent>(child.Owner, out var childPart) && childPart.Body == args.Body)
                detachedParts.Add((child.Owner, childPart.PartType, childPart.Symmetry));
        }

        if (_detachableOrgan.Detach(args.Part) is not { } detachedBody)
        {
            return;
        }

        args.Succeeded = true;
        args.DetachedBody = detachedBody;
        foreach (var detached in detachedParts)
        {
            HideHumanoidLimbLayer(args.Body, detached.Type, detached.Symmetry);
            ApplyMissingLimbStatus(args.Body, detached.Part, detached.Type);
        }

        if (!args.Surgical)
        {
            FlingPartFromBody(args.Body, detachedBody);
            ApplyStumpBleed(args.Body);
            _audio.PlayPvs(SeveranceSound, args.Body);
        }

        foreach (var detached in detachedParts)
        {
            var committed = new BodyPartSeveredEvent(args.Body, detached.Part, detached.Type);
            RaiseLocalEvent(detached.Part, ref committed, broadcast: true);
        }
    }

    private void FlingPartFromBody(EntityUid body, EntityUid detachedBody)
    {
        // compensateFriction:true so the part lands at the target instead
        // of sliding indefinitely off-grid (prior speed-8 fling was
        // overshooting the visible map).
        _transform.SetCoordinates(detachedBody, Transform(body).Coordinates);
        _transform.AttachToGridOrMap(detachedBody);

        var angle = _random.NextFloat(0f, MathF.Tau);
        var distance = _random.NextFloat(1.0f, 2.0f);
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
        _throwing.TryThrow(detachedBody, direction, baseThrowSpeed: 4f, doSpin: true, compensateFriction: true);
    }

    private void HideHumanoidLimbLayer(EntityUid body, BodyPartType type, BodyPartSymmetry symmetry)
    {
        // SS14's body-part graph and visual body layers are independent — the
        // marine sprite still draws the limb layer until we explicitly hide it.
        if (!HasComp<HideableHumanoidLayersComponent>(body))
            return;

        if (CMUMedicalVisualLayers.ForBodyPart(type, symmetry) is not { } layer)
            return;

        _hideableLayers.SetPermanentLayerOcclusion(body, layer, hidden: true);
    }

    private bool IsLocked(BodyPartType type) => type switch
    {
        BodyPartType.Head => _cfg.GetCVar(CMUMedicalCCVars.SeveranceHeadDisabled),
        BodyPartType.Torso => _cfg.GetCVar(CMUMedicalCCVars.SeveranceTorsoDisabled),
        _ => false,
    };

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
