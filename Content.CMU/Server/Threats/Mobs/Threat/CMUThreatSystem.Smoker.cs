using Content.Shared.CMU14.Threats.Mobs.Threat;

namespace Content.Server.CMU14.Threats.Mobs.Threat;

public sealed partial class CMUThreatSystem
{
    private void OnSmokerAction(Entity<CMUSmokerThreatComponent> ent, ref CMUSmokerTongueActionEvent args)
    {
        if (args.Handled || !TryStartSmokerTongue(ent, args.Target, ent.Comp))
            return;
        args.Handled = true;
    }

    private bool TryStartSmokerTongue(EntityUid uid, EntityUid target, CMUSmokerThreatComponent smoker)
    {
        if (_timing.CurTime < smoker.NextUseAt ||
            HasComp<CMUThreatPinComponent>(target) ||
            !CanTarget(uid, target, smoker.Range))
            return false;

        smoker.NextUseAt = _timing.CurTime + smoker.Cooldown;
        PlayAbilitySound(uid, smoker.Sound);
        Spawn(smoker.SmokePrototype, Transform(uid).Coordinates);
        StartPin(uid, target, CMUThreatPinKind.Smoker, smoker.ChokeDamage,
            smoker.ChokeDamageInterval, smoker.MaxPinDuration,
            smoker.PullSpeed, smoker.HoldDistance, true);
        return true;
    }
}
