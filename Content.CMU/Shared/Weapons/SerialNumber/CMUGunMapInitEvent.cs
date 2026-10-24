namespace Content.Shared.CMU14.Weapons.SerialNumber;

/// <summary>Raised on a gun once it's map-initialized, which is when it gets its serial number.</summary>
[ByRefEvent]
public record struct CMUGunMapInitEvent;
