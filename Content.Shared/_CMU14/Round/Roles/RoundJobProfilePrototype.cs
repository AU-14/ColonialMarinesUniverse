using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._CMU14.Round.Roles;

public enum RoundJobSide : byte
{
    None,
    Govfor,
    Opfor,
    ThirdParty,
    Civilian,
    Threat,
}

/// <summary>
/// Shared job-role component overlays applied from <see cref="Content.Shared.Roles.JobPrototype.RoundProfiles"/>.
/// </summary>
[Prototype]
public sealed partial class RoundJobProfilePrototype : IPrototype, IInheritingPrototype
{
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<RoundJobProfilePrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Components shared by every job using this profile, regardless of side or force.
    /// </summary>
    [DataField]
    [AlwaysPushInheritance]
    public ComponentRegistry Components { get; private set; } = new();

    /// <summary>
    /// Components applied when a job's roundSide matches the dictionary key.
    /// </summary>
    [DataField]
    [AlwaysPushInheritance]
    public Dictionary<string, ComponentRegistry> SideComponents { get; private set; } = new();

    /// <summary>
    /// Components applied when a job's roundForce matches the dictionary key.
    /// </summary>
    [DataField]
    [AlwaysPushInheritance]
    public Dictionary<string, ComponentRegistry> ForceComponents { get; private set; } = new();

    [DataField]
    public bool RemoveExisting { get; private set; } = true;
}
