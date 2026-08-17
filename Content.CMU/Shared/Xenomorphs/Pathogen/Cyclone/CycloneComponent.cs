using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Cyclone;

/// <summary>
/// Harbinger Cyclone ability. Channels briefly then hits nearby targets.
/// If it hits enough targets, triggers additional expanding spin cycles.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUXenoCycloneSystem))]
public sealed partial class CMUXenoCycloneComponent : Component
{
    [DataField, AutoNetworkedField]
    public float PlasmaCost = 0f;

    /// <summary>Delay before the first AoE hit fires.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ActivationDelay = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public float BaseRange = 2f;

    [DataField, AutoNetworkedField]
    public float BaseDamage = 25f;

    /// <summary>Min hits on first spin to trigger extra cycles.</summary>
    [DataField, AutoNetworkedField]
    public int MinHitsForCycles = 2;

    [DataField, AutoNetworkedField]
    public int Cycles = 4;

    [DataField, AutoNetworkedField]
    public float CycleDamage = 15f;

    [DataField, AutoNetworkedField]
    public TimeSpan CycleDelay = TimeSpan.FromSeconds(3);

    /// <summary>Range expands by 1 each cycle.</summary>
    [DataField, AutoNetworkedField]
    public float RangeGrowthPerCycle = 1f;
}
