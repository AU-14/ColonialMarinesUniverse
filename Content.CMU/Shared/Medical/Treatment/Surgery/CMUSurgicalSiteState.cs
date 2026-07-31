namespace Content.Shared._CMU14.Medical.Treatment.Surgery;

public enum CMUSurgicalAccess : byte
{
    Closed,
    Incised,
    Shallow,
    BoneCut,
    Deep,
}

public enum CMUSurgicalHemostasis : byte
{
    None,
    Uncontrolled,
    Clamped,
}

/// <summary>
///     Derived state for one surgical body site. Access and hemostasis are
///     independent so retracting tissue never requires clamping bleeders.
/// </summary>
public readonly record struct CMUSurgicalSiteState(
    CMUSurgicalAccess Access,
    CMUSurgicalHemostasis Hemostasis);
