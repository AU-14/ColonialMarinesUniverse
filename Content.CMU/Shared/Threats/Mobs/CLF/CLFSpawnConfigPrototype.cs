using Robust.Shared.Prototypes;

namespace Content.Shared.AU14;

/// <summary>
/// Configuration for CLF spawn points and additional entities to spawn at round start
/// </summary>
[Prototype("clfSpawnConfig")]
public sealed partial class CLFSpawnConfigPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Entity prototypes to spawn at the chosen safehouse location at round start
    /// These will be spawned after all CLF players are placed
    /// </summary>
    [DataField("additionalItems")]
    public List<string> additionalItems { get; private set; } = new();
}
