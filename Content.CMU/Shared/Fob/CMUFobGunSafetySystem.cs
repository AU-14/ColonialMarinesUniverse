using Content.Shared.CMU14.Sentry;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Fob;

/// <summary>Fail closed on both sides, including manual and remote attempts to use the underlying gun.</summary>
public sealed partial class CMUFobGunSafetySystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUFobSupportGunComponent, AttemptShootEvent>(OnAttemptShoot);
        if (_net.IsClient)
            SubscribeLocalEvent<CMUFobSupportGunComponent, CMUSentryRepairAttemptEvent>(OnRepairAttempt);
    }

    private void OnAttemptShoot(Entity<CMUFobSupportGunComponent> ent, ref AttemptShootEvent args)
    {
        var authorization = new CMUFobFireAuthorizationEvent(args.User);
        if (_net.IsServer)
            RaiseLocalEvent(ent, ref authorization);
        if (!authorization.Authorized)
            args.Cancelled = true;
    }

    private void OnRepairAttempt(Entity<CMUFobSupportGunComponent> ent, ref CMUSentryRepairAttemptEvent args)
    {
        // Repairs of this variant are authoritative; ordinary sentries keep their existing prediction.
        if (!_net.IsServer)
            args.Cancelled = true;
    }
}
