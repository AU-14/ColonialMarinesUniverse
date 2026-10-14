// ReSharper disable CheckNamespace

using Robust.Shared.Prototypes;

namespace Content.Shared.Traits;

public sealed partial class TraitPrototype
{
    /// <summary>
    ///     Traits that cannot be taken together with this one. Only needs listing on one side.
    /// </summary>
    [DataField("cmuExcludes")]
    public List<ProtoId<TraitPrototype>> CMUExcludes { get; private set; } = new();

    /// <summary>
    ///     Whether this trait and <paramref name="other"/> rule each other out, from either side's list.
    /// </summary>
    public bool CMUConflictsWith(TraitPrototype other)
    {
        return CMUExcludes.Contains(other.ID) || other.CMUExcludes.Contains(ID);
    }
}
