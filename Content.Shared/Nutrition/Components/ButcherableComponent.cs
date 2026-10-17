using Content.Shared.Kitchen;
using Content.Shared.Storage;
using Content.Shared.Tools.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Nutrition.Components;

/// <summary>
/// Indicates that the entity can be butchered through use of a butcher hook.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedKitchenSpikeSystem), typeof(ToolRefinableSystem), Other = AccessPermissions.Read)]
public sealed partial class ButcherableComponent : Component
{
    /// <summary>
    /// List of the entities that this entity should spawn after being butchered.
    /// </summary>
    /// <remarks>
    /// Note that <see cref="SharedKitchenSpikeSystem"/> spawns one item at a time and decreases the amount until it's zero and then removes the entry.
    /// </remarks>
    [DataField("spawned", required: true), AutoNetworkedField]
    public List<EntitySpawnEntry> SpawnedEntities = [];

    /// <summary>
    /// Time required to butcher that entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ButcherDelay = 8.0f;

    /// <summary>
    /// Selects which legacy butchering route can consume this entity.
    /// Tool refinement remains independently available when configured.
    /// </summary>
    [DataField("butcheringType"), AutoNetworkedField]
    public ButcheringType Type = ButcheringType.Knife;

    /// <summary>
    /// If true, tool refinement and kitchen spikes must wait until the victim is unrevivable.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool WaitForRot;
}

public enum ButcheringType : byte
{
    Knife,
    Spike,
    Gibber,
}
