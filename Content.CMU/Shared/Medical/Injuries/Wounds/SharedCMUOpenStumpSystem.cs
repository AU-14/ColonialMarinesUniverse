using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Core;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Medical.Injuries.Wounds;

public abstract partial class SharedCMUOpenStumpSystem : EntitySystem
{
    [Dependency] protected CMUMedicalBodyIndexSystem MedicalIndex = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>Opens a stump on <paramref name="parentPart"/> for the limb of the given type and side.</summary>
    public void AddStump(EntityUid parentPart, BodyPartType type, BodyPartSymmetry symmetry)
    {
        var comp = EnsureComp<CMUOpenStumpComponent>(parentPart);
        foreach (var stump in comp.Stumps)
        {
            if (stump.Type == type && stump.Symmetry == symmetry)
                return;
        }

        comp.Stumps.Add(new CMUStump { Type = type, Symmetry = symmetry });
        Dirty(parentPart, comp);
    }

    public bool TryFindStump(EntityUid body, BodyPartType type, BodyPartSymmetry symmetry, out EntityUid parentPart, out CMUStump stump)
    {
        foreach (var (partUid, _) in MedicalIndex.GetBodyParts(body))
        {
            if (!TryComp<CMUOpenStumpComponent>(partUid, out var comp))
                continue;

            foreach (var candidate in comp.Stumps)
            {
                if (candidate.Type != type || candidate.Symmetry != symmetry)
                    continue;

                parentPart = partUid;
                stump = candidate;
                return true;
            }
        }

        parentPart = default;
        stump = default!;
        return false;
    }

    /// <summary>The first open stump on the body that is still bleeding, if any.</summary>
    public bool TryFindUnclampedStump(EntityUid body, out EntityUid parentPart, out CMUStump stump)
    {
        foreach (var (partUid, _) in MedicalIndex.GetBodyParts(body))
        {
            if (!TryComp<CMUOpenStumpComponent>(partUid, out var comp))
                continue;

            foreach (var candidate in comp.Stumps)
            {
                if (candidate.Clamped)
                    continue;

                parentPart = partUid;
                stump = candidate;
                return true;
            }
        }

        parentPart = default;
        stump = default!;
        return false;
    }

    public bool HasUnclampedStump(EntityUid part)
    {
        if (!TryComp<CMUOpenStumpComponent>(part, out var comp))
            return false;

        foreach (var stump in comp.Stumps)
        {
            if (!stump.Clamped)
                return true;
        }

        return false;
    }

    public void SetClamped(EntityUid parentPart, CMUStump stump, bool clamped, EntProtoId? refund)
    {
        if (!TryComp<CMUOpenStumpComponent>(parentPart, out var comp))
            return;

        stump.Clamped = clamped;
        stump.ClampRefund = clamped ? refund : null;
        Dirty(parentPart, comp);
    }

    /// <summary>Closes the stump for one limb slot, e.g. when that limb is reattached.</summary>
    public void CloseStump(EntityUid body, BodyPartType type, BodyPartSymmetry symmetry)
    {
        if (!TryFindStump(body, type, symmetry, out var parentPart, out var stump) ||
            !TryComp<CMUOpenStumpComponent>(parentPart, out var comp))
        {
            return;
        }

        DropTourniquet(parentPart, stump);
        comp.Stumps.Remove(stump);
        if (comp.Stumps.Count == 0)
            RemComp<CMUOpenStumpComponent>(parentPart);
        else
            Dirty(parentPart, comp);
    }

    /// <summary>Closes every stump on a part, e.g. at the end of the stump closure surgery.</summary>
    public void CloseAllStumps(EntityUid parentPart)
    {
        if (!TryComp<CMUOpenStumpComponent>(parentPart, out var comp))
            return;

        foreach (var stump in comp.Stumps)
            DropTourniquet(parentPart, stump);

        RemComp<CMUOpenStumpComponent>(parentPart);
    }

    /// <summary>A tourniquet clamped on a stump falls to the floor when the stump is closed.</summary>
    private void DropTourniquet(EntityUid parentPart, CMUStump stump)
    {
        if (!_net.IsServer || !stump.Clamped || stump.ClampRefund is not { } proto)
            return;

        var anchor = CompOrNull<BodyPartComponent>(parentPart)?.Body ?? parentPart;
        Spawn(proto, _transform.GetMoverCoordinates(anchor));
    }
}
