using System.Collections.Frozen;

namespace Content.Shared._CMU14.Medical.Core;

/// <summary>
///     Immutable structural view of a CMU medical body at a specific revision.
/// </summary>
public sealed class CMUMedicalSnapshot
{
    public uint Revision { get; }
    public IReadOnlyDictionary<CMUMedicalBodyPartKey, EntityUid> BodyParts { get; }
    public IReadOnlySet<EntityUid> Organs { get; }

    internal EntityUid[] BodyPartOrder { get; }
    internal EntityUid[] OrganOrder { get; }

    internal CMUMedicalSnapshot(
        uint revision,
        IReadOnlyDictionary<CMUMedicalBodyPartKey, EntityUid> bodyParts,
        IEnumerable<EntityUid> organs,
        List<EntityUid> bodyPartOrder,
        List<EntityUid> organOrder)
    {
        Revision = revision;
        BodyParts = bodyParts.ToFrozenDictionary();
        Organs = organs.ToFrozenSet();
        BodyPartOrder = bodyPartOrder.ToArray();
        OrganOrder = organOrder.ToArray();
    }
}
