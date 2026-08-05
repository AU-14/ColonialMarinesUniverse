using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Humanoid.Markings;

public sealed partial class MarkingsGroupPrototype
{
    /// <summary>
    /// Markings that remain valid for the group but are excluded from profile selection and randomization.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<MarkingPrototype>> SelectionBlacklist = [];
}
