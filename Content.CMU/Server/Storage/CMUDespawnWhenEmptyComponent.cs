using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.CMU14.Storage;

/// <summary>
/// Deletes a storage item once it has been empty and lying loose on the ground for <see cref="Delay"/>.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CMUDespawnWhenEmptySystem))]
public sealed partial class CMUDespawnWhenEmptyComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromMinutes(1);

    /// <summary>When the entity will be deleted, or null while it is not both empty and on the ground.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? DespawnAt;
}
