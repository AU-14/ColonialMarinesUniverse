namespace Content.Shared.CMU14.Doors;

/// <summary>
///     An atmospherics alert computer. It announces, out loud, every <see cref="CMUFirelockSealComponent"/> firelock on
///     its ship that seals itself against escaping air, and the area it's in. Its screen lists every area aboard with a
///     breach, a fire, bad air or a sealed firelock.
/// </summary>
[RegisterComponent]
public sealed partial class CMUStationAlertComputerComponent : Component
{
    /// <summary>How often the screen rescans the ship while someone has it open.</summary>
    [DataField]
    public TimeSpan ScanInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    ///     How long after the computer starts up it notes which tiles are airless by design, like the outer hull, so
    ///     they never show as breaches.
    /// </summary>
    [DataField]
    public TimeSpan BaselineDelay = TimeSpan.FromSeconds(10);

    [ViewVariables]
    public TimeSpan BaselineAt;

    [ViewVariables]
    public bool Baselined;

    [ViewVariables]
    public TimeSpan NextScan;

    /// <summary>How long an area stays quiet after being called out, so a breach doesn't flood the chat.</summary>
    [DataField]
    public TimeSpan AreaCooldown = TimeSpan.FromSeconds(15);

    [ViewVariables]
    public Dictionary<string, TimeSpan> LastAlert = new();
}

/// <summary>
///     Raised broadcast when a <see cref="CMUFirelockSealComponent"/> firelock seals itself because of the air.
/// </summary>
[ByRefEvent]
public readonly record struct CMUFirelockSealedEvent(EntityUid Firelock);
