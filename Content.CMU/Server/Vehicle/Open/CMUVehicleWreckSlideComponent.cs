using System.Numerics;

namespace Content.Server.CMU14.Vehicle.Open;

/// <summary>
/// The wreck of a jeep that went up on the move, sliding on along its way until it slows to a stop,
/// then staying put like any other wreck.
/// </summary>
[RegisterComponent]
public sealed partial class CMUVehicleWreckSlideComponent : Component
{
    /// <summary>
    /// How fast it slows, in tiles a second each second.
    /// </summary>
    [DataField]
    public float Deceleration = 6f;

    /// <summary>
    /// Its own way and speed, kept apart from the body's so the floor's friction doesn't stop it short;
    /// a wall it runs into still does.
    /// </summary>
    [ViewVariables]
    public Vector2 Direction;

    [ViewVariables]
    public float Speed;
}
