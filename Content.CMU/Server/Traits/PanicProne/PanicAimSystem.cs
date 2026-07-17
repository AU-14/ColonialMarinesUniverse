using Content.Shared._CMU14.Traits.PanicProne;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Maths;

namespace Content.Server._CMU14.Traits.PanicProne;

public sealed partial class PanicAimSystem : EntitySystem
{
    [Dependency] private CMGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PanicGunAimPenaltyComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
    }

    private void OnGunRefreshModifiers(Entity<PanicGunAimPenaltyComponent> weapon, ref GunRefreshModifiersEvent args)
    {
        if (!TryGetAimPenalty(weapon.Owner, out var aim))
            return;

        var spread = aim.SpreadMultiplier;
        if (spread <= 1.0f)
            return;

        args.AngleIncrease = ScaleAngle(args.AngleIncrease, spread);
        args.MinAngle = ScaleAngle(args.MinAngle, spread);
        args.MaxAngle = ScaleAngle(args.MaxAngle, spread);
    }

    private bool TryGetAimPenalty(EntityUid weapon, out PanicAimComponent aim)
    {
        aim = default!;

        if (!_gun.TryGetGunUser(weapon, out var user))
            return false;

        if (!TryComp(user.Owner, out PanicAimComponent? component))
            return false;

        aim = component;
        return true;
    }

    private static Angle ScaleAngle(Angle angle, float multiplier)
        => new(angle.Theta * multiplier);
}
