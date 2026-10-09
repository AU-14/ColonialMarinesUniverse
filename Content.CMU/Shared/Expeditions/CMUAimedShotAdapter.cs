namespace Content.Shared._RMC14.Weapons.Ranged.AimedShot;

public abstract partial class SharedRMCAimedShotSystem
{
    public void CancelControlledAimedShot(EntityUid gun)
    {
        if (!TryComp<AimedShotComponent>(gun, out var aimed))
            return;
        while (aimed.Targets.Count > 0)
            RemoveTarget((gun, aimed), aimed.Targets[0]);
    }

    /// <summary>Native entry point for a server-controlled wielder; retains all aiming checks and effects.</summary>
    public bool TryStartControlledAimedShot(EntityUid gun, EntityUid user, EntityUid target)
    {
        if (!TryComp<AimedShotComponent>(gun, out var aimed) || !CanAimShot((gun, aimed), target, user))
            return false;
        if (!aimed.Activated)
        {
            var toggle = new AimedShotActionEvent();
            RaiseLocalEvent(gun, toggle);
        }
        AimedShotRequested(GetNetEntity(gun), GetNetEntity(user), GetNetEntity(target));
        return aimed.Targets.Contains(target);
    }
}
