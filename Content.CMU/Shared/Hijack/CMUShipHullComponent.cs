namespace Content.Shared.CMU14.Hijack;

/// <summary>
/// A mainship deck that can be repositioned by the crash sequence but must not
/// drift under shuttle physics while evacuation craft are parked in its bays.
/// </summary>
[RegisterComponent]
public sealed partial class CMUShipHullComponent : Component;
