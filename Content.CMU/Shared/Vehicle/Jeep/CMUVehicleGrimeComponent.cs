using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Dirt a vehicle picks up as it drives, fastest off-road, shown as dust and mud through its paint
/// shader. Blood lands on it as splats stored with its crayon drawings: from riders' wounds and from
/// whoever it runs down. Space cleaner, soap or a wet mop takes dirt, blood and crayon off.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleGrimeComponent : Component
{
    /// <summary>
    /// 0 clean to 1 caked, in steps of 1 / <see cref="DirtSteps"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Dirt;

    [DataField]
    public int DirtSteps = 10;

    /// <summary>
    /// Dirt per tile driven on bare ground; paved ground outdoors and floors indoors give less.
    /// </summary>
    [DataField]
    public float DirtPerTile = 0.004f;

    [DataField]
    public float PavedFactor = 0.35f;

    [DataField]
    public float IndoorFactor = 0.08f;

    /// <summary>
    /// Dirt as it builds up, before it is rounded to a step. Server only.
    /// </summary>
    [ViewVariables]
    public float RawDirt;

    /// <summary>
    /// Share of the grime (dirt, blood and crayon) each unit of a cleaning reagent takes off.
    /// </summary>
    [DataField]
    public float CleanPerUnit = 0.1f;

    /// <summary>
    /// Scrubbing the whole vehicle down with soap or a wet mop.
    /// </summary>
    [DataField]
    public TimeSpan ScrubDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Least brute damage a rider takes for their blood to splash the seat.
    /// </summary>
    [DataField]
    public float WoundSplashMinDamage = 3f;

    /// <summary>
    /// How often a bleeding rider may drip on their seat.
    /// </summary>
    [DataField]
    public TimeSpan DripInterval = TimeSpan.FromSeconds(3);

    [ViewVariables]
    public TimeSpan NextDrip;
}

[Serializable, NetSerializable]
public sealed partial class CMUVehicleScrubDoAfterEvent : SimpleDoAfterEvent;
