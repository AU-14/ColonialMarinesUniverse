using Content.Shared.Botany.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Botany.Items.Components;

/// <summary>
/// Component for a container for plant seed. Contains all info (values for components) for new plant to grow from seed.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(BotanySystem))]
public sealed partial class SeedComponent : Component
{
    /// <summary>
    /// Name of a base plant prototype to spawn.
    /// </summary>
    [DataField("plantId")]
    public EntProtoId PlantProtoId;

    /// <summary>
    /// Hidden entity with cloned plant components used to override defaults when planting.
    /// </summary>
    [DataField]
    public EntityUid? PlantData;

    /// <summary>
    /// If not null, overrides the plant's initial health. Otherwise, the plant's initial health is set to the Endurance value.
    /// </summary>
    [DataField]
    public float? HealthOverride;
}
