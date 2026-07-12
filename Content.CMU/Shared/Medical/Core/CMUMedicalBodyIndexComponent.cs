using Content.Shared.Body.Part;

namespace Content.Shared._CMU14.Medical.Core;

/// <summary>
///     Server-owned structural index for a CMU medical body.
/// </summary>
[RegisterComponent]
[Access(typeof(CMUMedicalBodyIndexSystem))]
public sealed partial class CMUMedicalBodyIndexComponent : Component
{
    [ViewVariables]
    internal EntityUid? RootPart;

    [ViewVariables]
    internal readonly Dictionary<CMUMedicalBodyPartKey, EntityUid> BodyParts = new();

    [ViewVariables]
    internal readonly HashSet<EntityUid> Organs = new();

    [ViewVariables]
    internal readonly List<EntityUid> BodyPartOrder = new();

    [ViewVariables]
    internal readonly List<EntityUid> OrganOrder = new();

    [ViewVariables]
    internal readonly Dictionary<EntityUid, List<EntityUid>> PartOrgans = new();

    [ViewVariables]
    internal readonly Dictionary<EntityUid, List<CMUMedicalOrganSlotEntry>> PartOrganSlots = new();

    [ViewVariables]
    internal readonly Dictionary<EntityUid, List<CMUMedicalBodyPartSlotEntry>> PartChildSlots = new();

    [ViewVariables]
    internal readonly Dictionary<EntityUid, EntityUid> OrganParts = new();
}

/// <summary>
///     Canonical identity of a body part within a human body.
/// </summary>
public readonly record struct CMUMedicalBodyPartKey(BodyPartType Type, BodyPartSymmetry Symmetry);

/// <summary>
///     Deterministic structural view of an organ slot and its current occupant.
/// </summary>
public readonly record struct CMUMedicalOrganSlotEntry(string SlotId, EntityUid? Organ);

/// <summary>
///     Deterministic structural view of a child body-part slot and its current occupant.
/// </summary>
public readonly record struct CMUMedicalBodyPartSlotEntry(
    string SlotId,
    BodyPartType Type,
    EntityUid? Part);
