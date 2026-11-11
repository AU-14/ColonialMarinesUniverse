using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.CMU14.Weapons.Grenades;
using Content.Shared.Mobs.Components;
using Content.Shared.Trigger;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Weapons.Grenades;

/// <summary>
///     Remembers who a launched impact grenade struck, and spares them its blast's stun when it goes off.
/// </summary>
public sealed class CMULauncherImpactNoStunSystem : EntitySystem
{
    /// <summary>Long enough for the blast to reach them; explosions spread over a few ticks.</summary>
    private static readonly TimeSpan ExemptTime = TimeSpan.FromSeconds(3);

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly List<EntityUid> _expired = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMULauncherImpactNoStunComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<CMULauncherImpactNoStunComponent, TriggerEvent>(OnTrigger);
    }

    private void OnStartCollide(Entity<CMULauncherImpactNoStunComponent> ent, ref StartCollideEvent args)
    {
        // Only while it's flying out of a launcher.
        if (ent.Comp.HitMob != null ||
            !HasComp<ProjectileFixedDistanceComponent>(ent) ||
            !HasComp<MobStateComponent>(args.OtherEntity))
        {
            return;
        }

        ent.Comp.HitMob = args.OtherEntity;
    }

    private void OnTrigger(Entity<CMULauncherImpactNoStunComponent> ent, ref TriggerEvent args)
    {
        if (ent.Comp.HitMob is not { } mob || TerminatingOrDeleted(mob))
            return;

        var exempt = EnsureComp<CMUExplosionStunExemptComponent>(mob);
        exempt.Epicenter = _transform.GetMapCoordinates(ent);
        exempt.ExpireAt = _timing.CurTime + ExemptTime;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        _expired.Clear();
        var query = EntityQueryEnumerator<CMUExplosionStunExemptComponent>();
        while (query.MoveNext(out var uid, out var exempt))
        {
            if (now >= exempt.ExpireAt)
                _expired.Add(uid);
        }

        foreach (var uid in _expired)
        {
            RemComp<CMUExplosionStunExemptComponent>(uid);
        }
    }
}
