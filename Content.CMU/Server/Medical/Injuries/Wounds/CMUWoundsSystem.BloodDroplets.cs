using System.Numerics;
using Content.Server.Decals;
using Content.Shared.Body.Components;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Injuries.Wounds.Events;
using Content.Shared.Decals;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Medical.Injuries.Wounds;

public sealed partial class CMUWoundsSystem
{
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedCMUOpenStumpSystem _stumps = default!;

    private static readonly ProtoId<DecalPrototype>[] BloodDroplets =
    [
        "CMUBloodDroplet1",
        "CMUBloodDroplet2",
        "CMUBloodDroplet3",
        "CMUBloodDroplet4",
        "CMUBloodDroplet5",
    ];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodstreamComponent, CMUWoundBloodSpillEvent>(OnWoundBloodSpill);
    }

    private void OnWoundBloodSpill(Entity<BloodstreamComponent> ent, ref CMUWoundBloodSpillEvent args)
    {
        if (args.Handled || !ShouldLeaveBloodDroplets(ent))
            return;

        // Decals have no contact fixture or reagent transfer, so small drips cannot stain feet.
        _decals.TryAddDecal(
            _random.Pick(BloodDroplets),
            Transform(ent).Coordinates.Offset(new Vector2(-0.5f, -0.5f)),
            out _,
            color: args.Solution.GetColor(Proto),
            rotation: _random.NextAngle(),
            cleanable: true);
        args.Handled = true;
    }

    private bool ShouldLeaveBloodDroplets(EntityUid body)
    {
        var bleeding = false;
        var moderateBleeds = 0;
        foreach (var (part, _) in MedicalIndex.GetBodyParts(body))
        {
            if (IsBloodFlowOccluded(part))
                continue;

            // An open stump is an arterial source even when another wound causes this spill.
            if (_stumps.HasUnclampedStump(part))
                return false;

            if (!TryComp<BodyPartWoundComponent>(part, out var wounds))
                continue;

            switch (wounds.ExternalBleeding)
            {
                case ExternalBleedTier.Severe:
                case ExternalBleedTier.Arterial:
                    return false;
                case ExternalBleedTier.Moderate:
                    if (++moderateBleeds >= 2)
                        return false;
                    bleeding = true;
                    break;
                case ExternalBleedTier.Minor:
                    bleeding = true;
                    break;
            }
        }

        return bleeding;
    }
}
