using System.Numerics;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Injuries.Wounds.Events;
using Content.Shared.Decals;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Medical.Injuries.Wounds;

/// <summary>
/// Arterial bleeds don't just pool at the patient's feet: each spill squirts blood a few tiles away,
/// leaving splatters along the way, and the patient is told they're spurting blood.
/// </summary>
public sealed partial class CMUWoundsSystem
{
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float ArterialSprayMinRange = 1.5f;
    private const float ArterialSprayMaxRange = 3f;
    private const int ArterialSpraySteps = 3;
    private static readonly TimeSpan ArterialPopupCooldown = TimeSpan.FromSeconds(5);

    private static readonly ProtoId<DecalPrototype>[] BloodSplatters =
    [
        "RMCDecalBloodSplatter1",
        "RMCDecalBloodSplatter2",
        "RMCDecalBloodSplatter3",
        "RMCDecalBloodSplatter4",
        "RMCDecalBloodSplatter5",
        "RMCDecalBloodSplatter6",
    ];

    private readonly Dictionary<EntityUid, TimeSpan> _nextArterialPopup = new();

    private bool TrySprayArterialBlood(Entity<BloodstreamComponent> ent, Solution blood)
    {
        if (!TryGetArterialSource(ent, out var part))
            return false;

        var origin = _transform.GetMapCoordinates(ent);
        var direction = _random.NextAngle().ToVec();
        var range = _random.NextFloat(ArterialSprayMinRange, ArterialSprayMaxRange);
        var color = blood.GetColor(Proto);

        // Walk out along the spray and stop at the first wall, so blood never lands on the far side of one.
        MapCoordinates? landing = null;
        for (var step = 1; step <= ArterialSpraySteps; step++)
        {
            var target = origin.Offset(direction * range * step / ArterialSpraySteps);
            if (!_interaction.InRangeUnobstructed(origin, target, range + 0.5f, CollisionGroup.Impassable,
                    uid => uid == ent.Owner))
            {
                break;
            }

            landing = target;
            _decals.TryAddDecal(
                _random.Pick(BloodSplatters),
                _transform.ToCoordinates(target).Offset(new Vector2(-0.5f, -0.5f)),
                out _,
                color: color,
                rotation: _random.NextAngle(),
                cleanable: true);
        }

        var spillAt = landing is { } land ? _transform.ToCoordinates(land) : Transform(ent).Coordinates;
        _puddle.TrySpillAt(spillAt, blood, out _, sound: false);

        PopupArterialSpray(ent, part);
        return true;
    }

    private void PopupArterialSpray(EntityUid body, EntityUid part)
    {
        var now = Timing.CurTime;
        if (_nextArterialPopup.TryGetValue(body, out var next) && now < next)
            return;

        _nextArterialPopup[body] = now + ArterialPopupCooldown;
        _popup.PopupEntity(Loc.GetString("cmu-wound-arterial-spray", ("part", Name(part))), body, body,
            PopupType.MediumCaution);
    }

    /// <summary>An unoccluded arterial wound or open stump that would spray blood.</summary>
    private bool TryGetArterialSource(EntityUid body, out EntityUid part)
    {
        foreach (var (candidate, _) in MedicalIndex.GetBodyParts(body))
        {
            if (IsBloodFlowOccluded(candidate))
                continue;

            if (_stumps.HasUnclampedStump(candidate) ||
                TryComp<BodyPartWoundComponent>(candidate, out var wounds) &&
                wounds.ExternalBleeding == ExternalBleedTier.Arterial)
            {
                part = candidate;
                return true;
            }
        }

        part = default;
        return false;
    }

    private void OnArterialBodyShutdown(Entity<BloodstreamComponent> ent, ref ComponentShutdown args)
    {
        _nextArterialPopup.Remove(ent);
    }
}
