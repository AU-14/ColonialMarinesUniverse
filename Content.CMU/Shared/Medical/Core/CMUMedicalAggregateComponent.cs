namespace Content.Shared._CMU14.Medical.Core;

/// <summary>
///     Server-owned revision and cached aggregate for a CMU medical body.
/// </summary>
[RegisterComponent]
[Access(typeof(CMUMedicalBodyIndexSystem), typeof(CMUMedicalChangeSystem))]
public sealed partial class CMUMedicalAggregateComponent : Component
{
    [ViewVariables]
    internal uint Revision;

    [ViewVariables]
    internal uint MedicalRevision;

    internal CMUMedicalSnapshot? Snapshot;

    internal CMUMedicalChangeFlags PendingChanges;

    internal bool PendingRevisionAdvancedByChange;
}
