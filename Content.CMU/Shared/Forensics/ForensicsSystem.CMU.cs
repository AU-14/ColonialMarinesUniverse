using Content.Shared.Forensics.Components;

namespace Content.Shared.Forensics.Systems;

public sealed partial class ForensicsSystem
{
    private static readonly string[] CMUSampleSlots = { "outerClothing", "jumpsuit" };

    /// <summary>
    /// Physical contact between two people (fighting, grabbing, CPR) leaves hair and skin from each on the other's clothes.
    /// Gloves don't stop this.
    /// </summary>
    private void CMUTransferContactSample(EntityUid user, EntityUid target)
    {
        if (!_dnaQuery.TryComp(user, out var userDna) || userDna.DNA == null ||
            !_dnaQuery.HasComp(target))
        {
            return;
        }

        var recipient = target;
        foreach (var slot in CMUSampleSlots)
        {
            if (_inventory.TryGetSlotEntity(target, slot, out var worn))
            {
                recipient = worn.Value;
                break;
            }
        }

        var forensics = EnsureComp<ForensicsComponent>(recipient);
        if (forensics.DNAs.Add(userDna.DNA))
            Dirty(recipient, forensics);
    }

    /// <summary>
    /// DNA left on the clothes someone is wearing, so scanning a person also picks up who they've been in contact with.
    /// </summary>
    public IEnumerable<string> CMUGetWornSampleDna(EntityUid wearer)
    {
        foreach (var slot in CMUSampleSlots)
        {
            if (!_inventory.TryGetSlotEntity(wearer, slot, out var worn) ||
                !_forensicsQuery.TryComp(worn, out var forensics))
            {
                continue;
            }

            foreach (var dna in forensics.DNAs)
            {
                yield return dna;
            }
        }
    }

    /// <summary>
    /// Leaves contact samples between two people, for interactions that don't already raise a contact event (CPR).
    /// </summary>
    public void CMUApplyPersonContact(EntityUid a, EntityUid b)
    {
        CMUTransferContactSample(a, b);
        CMUTransferContactSample(b, a);
    }
}
