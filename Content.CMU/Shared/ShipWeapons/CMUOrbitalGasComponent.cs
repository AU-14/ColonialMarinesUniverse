namespace Content.Shared.CMU14.ShipWeapons;

[RegisterComponent]
public sealed partial class CMUOrbitalGasComponent : Component
{
}

[ByRefEvent]
public readonly record struct CMUOrbitalGasExposureEvent(EntityUid Victim);
