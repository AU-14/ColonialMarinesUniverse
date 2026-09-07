using Content.Shared._RMC14.Synth;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

namespace Content.Shared.CMU14.Medical.Anatomy.BodyParts;

public sealed partial class SharedCMURoboticLimbSystem
{
    private void OnSynthDamageChanged(Entity<SynthComponent> ent, ref DamageChangedEvent args)
    {
        if (_net.IsClient)
            return;

        var damage = _damageable.GetAllDamage(ent.Owner);
        var bruteRepaired = GroupSum(damage, BruteGroup) <= FixedPoint2.Zero;
        var burnRepaired = GroupSum(damage, BurnGroup) <= FixedPoint2.Zero;
        if (!bruteRepaired && !burnRepaired)
            return;

        // Synth tools repair the body aggregate. Clear the corresponding examine
        // counters when that damage group has been fully repaired.
        foreach (var (part, _) in _medicalIndex.GetBodyParts(ent.Owner))
        {
            if (!TryComp<CMURoboticLimbComponent>(part, out var robotic))
                continue;

            var brute = bruteRepaired ? FixedPoint2.Zero : robotic.BruteDamage;
            var burn = burnRepaired ? FixedPoint2.Zero : robotic.BurnDamage;
            if (brute == robotic.BruteDamage && burn == robotic.BurnDamage)
                continue;

            robotic.BruteDamage = brute;
            robotic.BurnDamage = burn;
            Dirty(part, robotic);
        }
    }
}
