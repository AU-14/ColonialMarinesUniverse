using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Ship;

/// <summary>
///     Put on one map of a ship's z-network. Off-hull space tiles anywhere in the network are zero-g instead of a drop
///     to the deck below: people float there, can push up or down through open space, and never fall into a chasm.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUZeroGSpaceComponent : Component;

/// <summary>
///     Put on one map of a ship's z-network. When a hijacked dropship crashes into the ship, its hull windows can be
///     broken, breathing and pressure start to matter, and every APC aboard is knocked out.
/// </summary>
[RegisterComponent]
public sealed partial class CMUHijackCrashHullBreachComponent : Component
{
    /// <summary>
    ///     Components added to each <see cref="CMUCrashBreakableHullComponent"/> once the ship is hit.
    /// </summary>
    [DataField]
    public EntProtoId HullTemplate = "CMUCrashBreakableHullTemplate";

    /// <summary>How long after the crash the hijack pipe explosions can break hull windows too.</summary>
    [DataField]
    public TimeSpan PipeBreaksWindowsDelay = TimeSpan.FromMinutes(1);

    /// <summary>How long after the crash every floor tile aboard can be blown out by explosions.</summary>
    [DataField]
    public TimeSpan TilesBreakableDelay = TimeSpan.FromMinutes(4);

    /// <summary>When the hijacked dropship crashed into the ship, or null if it hasn't.</summary>
    [ViewVariables]
    public TimeSpan? CrashedAt;

    [ViewVariables]
    public bool PipeBreaksWindows;

    [ViewVariables]
    public bool TilesBreakable;
}

/// <summary>
///     Put on every deck of a ship as soon as a hijacked dropship crashes into it.
/// </summary>
[RegisterComponent]
public sealed partial class CMUShipCrashedMapComponent : Component;

/// <summary>
///     Put on every deck of a crashed ship once the hijack's pipe explosions are allowed to break its hull windows.
/// </summary>
[RegisterComponent]
public sealed partial class CMUHijackPipesBreakHullComponent : Component;

/// <summary>
///     Put on every deck of a crashed ship once it starts coming apart: explosions break any floor tile aboard, straight
///     through to space, the way they break multi-Z grates.
/// </summary>
[RegisterComponent]
public sealed partial class CMUHullCollapsingMapComponent : Component
{
    /// <summary>Debris left by each tile an explosion breaks. Kept light, since a lot of tiles go at once.</summary>
    [DataField]
    public EntProtoId Debris = "CMRodMetal1";

    [DataField]
    public float DebrisChance = 0.15f;
}

/// <summary>
///     An indestructible hull piece that becomes breakable once a hijacked dropship crashes into its ship.
/// </summary>
[RegisterComponent]
public sealed partial class CMUCrashBreakableHullComponent : Component
{
    /// <summary>
    ///     Components it gets once the ship is hit, if not the ship's <see cref="CMUHijackCrashHullBreachComponent.HullTemplate"/>.
    /// </summary>
    [DataField]
    public EntProtoId? Template;

    /// <summary>Whether the template's components replace ones it already has, like an acid immunity.</summary>
    [DataField]
    public bool ReplaceExisting;
}

/// <summary>
///     Put on a map entity to make low and high pressure hurt humans while they're on it, or anywhere else in its
///     z-network. Like breathing, pressure damage is otherwise off for humans.
/// </summary>
[RegisterComponent]
public sealed partial class CMUPressureRequiredMapComponent : Component;

/// <summary>
///     Keeps its ship's gravity on while it has power. Losing power turns gravity off on every deck of the ship.
/// </summary>
[RegisterComponent]
public sealed partial class CMUShipGravityGeneratorComponent : Component
{
    /// <summary>Whether the generator was powered when its ship's gravity was last set, or null before that.</summary>
    [ViewVariables]
    public bool? LastPowered;
}

/// <summary>
///     Xenos can't damage this: any damage that comes from a xeno, by claw, spit or ability, is cancelled. Used on the
///     ship's hull windows so a hijack crash doesn't give xenos a way to vent the ship.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUXenoProofComponent : Component;

/// <summary>
///     Raised on a moving entity while it's weightless, so a zero-g ship can change how it moves.
/// </summary>
[ByRefEvent]
public record struct CMUZeroGMovementEvent
{
    /// <summary>Floating over open space: nothing to push off unless a solid object is right there, and no drag.</summary>
    public bool Adrift;

    /// <summary>Multiplies the weightless walk and sprint speed, for things like crawling.</summary>
    public float SpeedMultiplier;

    /// <summary>Multiplies how quickly they get up to speed. Drag and drifting aren't affected.</summary>
    public float AccelerationMultiplier;

    public CMUZeroGMovementEvent()
    {
        SpeedMultiplier = 1f;
        AccelerationMultiplier = 1f;
    }
}

