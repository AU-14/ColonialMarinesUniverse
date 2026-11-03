using Content.Shared.Damage;

namespace Content.Shared.CMU14.Medical.Defibrillator;

// Lets tool-repair-only damage opt out of the defib revival threshold.
[ByRefEvent]
public record struct RMCDefibrillatorProtectedDamageEvent(EntityUid Target)
{
    public DamageSpecifier Protected = new();
}
