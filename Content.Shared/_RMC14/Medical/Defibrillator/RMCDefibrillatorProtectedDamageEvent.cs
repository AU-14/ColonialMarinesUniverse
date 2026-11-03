using Content.Shared.Damage;

namespace Content.Shared._RMC14.Medical.Defibrillator;

// CMU14 class: lets tool-repair-only damage opt out of the defib revival threshold
[ByRefEvent]
public record struct RMCDefibrillatorProtectedDamageEvent(EntityUid Target)
{
    public DamageSpecifier Protected = new();
}
