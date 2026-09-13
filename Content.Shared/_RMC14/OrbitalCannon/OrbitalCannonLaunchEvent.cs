namespace Content.Shared._RMC14.OrbitalCannon;

[ByRefEvent]
public readonly record struct OrbitalCannonLaunchEvent(EntityUid Cannon, TimeSpan Cooldown, string CannonFaction);
