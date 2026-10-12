using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Core;

namespace Content.Shared.CMU14.Medical.Anatomy.BodyParts;

// one answer to "can this body hold itself up?" so severance, stand attempts and the
// downed-action exemption can't disagree about what a missing leg is
public sealed partial class CMULegSupportSystem : EntitySystem
{
    [Dependency] private CMUMedicalBodyIndexSystem _index = default!;

    public bool HasLegsToStand(Entity<BodyComponent?> body)
    {
        if (!Resolve(body, ref body.Comp, false))
            return true;

        if (body.Comp.LegEntities.Count < 2)
            return false;

        // a leg with an empty foot slot is a stump you can't stand on (cm13 parity).
        // species whose legs have no foot slot never trip this
        foreach (var leg in body.Comp.LegEntities)
        {
            if (MissingFoot(leg))
                return false;
        }

        return true;
    }

    public bool MissingFoot(EntityUid leg)
    {
        foreach (var slot in _index.GetBodyPartSlots(leg))
        {
            if (slot.Type == BodyPartType.Foot && slot.Part == null)
                return true;
        }

        return false;
    }
}
