using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Threats.Mobs.CLF;

/// <summary>
///     Configuration for CLF spawn points and additional entities to spawn at round start
/// </summary>
[Prototype("clfSpawnConfig")]
public sealed partial class CLFSpawnConfigPrototype : IPrototype
{
    /// <summary>
    ///     Entity prototypes to spawn at the chosen safehouse location at round start
    ///     These will be spawned after all CLF players are placed
    /// </summary>
    [DataField("additionalItems")]
    public List<string> additionalItems { get; private set; } = new();

    [IdDataField]
    public string ID { get; private set; } = default!;
}
