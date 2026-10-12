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
using Robust.Shared.Random;

namespace Content.Server.CMU14.Medical.Injuries.Wounds;

/// <summary>
/// Arterial bleeds squirt blood a few tiles away every <see cref="ArterialSprayInterval"/>, however fast they bleed,
/// leaving splatters along the way, and the patient is told they're spurting blood. Spills in between pool at the
/// patient's feet as usual.
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
    private static readonly TimeSpan ArterialSprayInterval = TimeSpan.FromSeconds(10);

    private static readonly ProtoId<DecalPrototype>[] BloodSplatters =
    [
        "CMUBloodSplatter1",
        "CMUBloodSplatter2",
        "CMUBloodSplatter3",
        "CMUBloodSplatter4",
        "CMUBloodSplatter5",
        "CMUBloodSplatter6",
    ];

    private readonly Dictionary<EntityUid, TimeSpan> _nextArterialSpray = new();

    private bool TrySprayArterialBlood(Entity<BloodstreamComponent> ent, Solution blood)
    {
        if (!TryGetArterialSource(ent, out var part, out var stump))
            return false;

        var now = Timing.CurTime;
        if (_nextArterialSpray.TryGetValue(ent, out var next) && now < next)
            return false;

        _nextArterialSpray[ent] = now + ArterialSprayInterval;

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

        // A stump has nothing left to name, so say which limb used to be there.
        var message = stump is { } open
            ? Loc.GetString("cmu-wound-arterial-spray-stump", ("limb", Loc.GetString("cmu-wound-stump-limb",
                ("symmetry", open.Symmetry.ToString().ToLowerInvariant()),
                ("type", open.Type.ToString().ToLowerInvariant()))))
            : Loc.GetString("cmu-wound-arterial-spray", ("part", Name(part)));
        _popup.PopupEntity(message, ent, ent, PopupType.MediumCaution);
        return true;
    }

    /// <summary>
    /// True while the body has an arterial bleed that sprays: an arterial wound or unclamped stump that no tourniquet
    /// holds shut.
    /// </summary>
    public bool IsSprayingArterialBlood(EntityUid body) => TryGetArterialSource(body, out _, out _);

    /// <summary>
    /// An unoccluded arterial wound or unclamped stump that would spray blood. For a stump, <paramref name="part"/>
    /// is the part it hangs off and <paramref name="stump"/> says which limb is missing.
    /// </summary>
    private bool TryGetArterialSource(EntityUid body, out EntityUid part, out CMUStump? stump)
    {
        foreach (var (candidate, _) in MedicalIndex.GetBodyParts(body))
        {
            if (IsBloodFlowOccluded(candidate))
                continue;

            if (TryComp<CMUOpenStumpComponent>(candidate, out var stumps))
            {
                foreach (var open in stumps.Stumps)
                {
                    if (open.Clamped)
                        continue;

                    part = candidate;
                    stump = open;
                    return true;
                }
            }

            if (TryComp<BodyPartWoundComponent>(candidate, out var wounds) &&
                wounds.ExternalBleeding == ExternalBleedTier.Arterial)
            {
                part = candidate;
                stump = null;
                return true;
            }
        }

        part = default;
        stump = null;
        return false;
    }

    private void OnArterialBodyShutdown(Entity<BloodstreamComponent> ent, ref ComponentShutdown args)
    {
        _nextArterialSpray.Remove(ent);
    }
}
