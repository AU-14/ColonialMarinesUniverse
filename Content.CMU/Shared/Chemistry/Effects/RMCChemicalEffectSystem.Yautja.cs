using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Threats.Mobs.Abomination;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Chemistry.Effects;

public sealed partial class RMCChemicalEffectSystem
{
    [Dependency] private IGameTiming _yautjaTiming = default!;

    internal bool HasAbominationInfection(EntityUid target) => HasComp<AbominationInfectionComponent>(target);

    internal void CureAbominationInfection(EntityUid target) => RemComp<AbominationInfectionComponent>(target);

    internal void StabilizeYautjaOrgans(EntityUid target)
    {
        foreach (var organ in MedicalBodyIndex.GetOrgans(target))
        {
            var stabilized = EnsureComp<CMUOrganStabilizedComponent>(organ.Owner);
            stabilized.ExpiresAt = _yautjaTiming.CurTime + TimeSpan.FromSeconds(2);
            Dirty(organ.Owner, stabilized);
        }
    }
}
