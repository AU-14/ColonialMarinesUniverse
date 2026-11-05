using Content.Shared.CMU14.Yautja;

namespace Content.Server.CMU14.Medical.Treatment.Surgery;

/// <summary>Uses the same technology access rule for surgery selection and execution.</summary>
public sealed partial class CMUYautjaSurgeryAccessSystem : EntitySystem
{
    [Dependency] private YautjaPowerSystem _power = default!;

    public bool CanOperate(EntityUid user)
    {
        if (HasComp<YautjaComponent>(user) ||
            TryComp<YautjaThrallComponent>(user, out var thrall) && thrall.Blooded && thrall.TechAuthorized)
            return true;

        if (!HasComp<YautjaTechAuthorizedComponent>(user))
            return false;

        // Research knowledge persists, but its surgical interface belongs to the worn, unlocked bracer.
        return !HasComp<CMUYautjaResearchOperatorComponent>(user) ||
            _power.TryGetWornBracer(user, out var bracer) &&
            TryComp<CMUYautjaExperimentComponent>(bracer, out var experiment) && experiment.Unlocked;
    }
}
