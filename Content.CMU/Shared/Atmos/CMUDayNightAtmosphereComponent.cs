using Content.Shared.Atmos;

namespace Content.Shared.CMU14.Atmos;

/// <summary>
/// Drives the day-night temperature cycle for a whole z-network's map
/// atmospheres. Author it once per network, next to the canonical
/// <see cref="Components.MapAtmosphereComponent"/> (conventionally the base
/// deck's map entity). Every deck carries its own MapAtmosphere mechanically,
/// so the system fans the cycle out to each member map while keeping each
/// deck's authored moles as its own baseline: only the temperature moves, one
/// clock for the entire network. Outdoor relaxation follows the live mixtures
/// without further wiring.
/// </summary>
[RegisterComponent]
public sealed partial class CMUDayNightAtmosphereComponent : Component
{
    /// <summary>Length of one full cycle.</summary>
    [DataField]
    public TimeSpan CycleDuration = TimeSpan.FromHours(1);

    /// <summary>Warmest mixture temperature, reached a quarter into the cycle.</summary>
    [DataField]
    public float DayTemperature = 303.15f;

    /// <summary>Coldest mixture temperature, reached three quarters in.</summary>
    [DataField]
    public float NightTemperature = 263.15f;

    /// <summary>Shifts the whole cycle. Zero starts the round mid-morning on the rise.</summary>
    [DataField]
    public TimeSpan PhaseOffset;

    // Server runtime, not serialized. One baseline per non-space network
    // member that declares a MapAtmosphere; keys are map entities.
    [ViewVariables]
    public readonly Dictionary<EntityUid, GasMixture> Baselines = new();

    [ViewVariables]
    public TimeSpan CycleStart;

    [ViewVariables]
    public TimeSpan NextUpdate;

    // Set by StartCycle. Duplicate detection yields only to a component that
    // actually initialized: a RemCompDeferred'd duplicate stays visible to
    // queries until the flush, and two same-frame MapInits would both see
    // each other and strip themselves, leaving the network with no driver.
    [ViewVariables]
    public bool Initialized;
}
