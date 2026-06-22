using Content.Shared.Movement.Systems;

namespace Content.Shared._AU14.Formations;

/// <summary>
/// Shared (client + server) formation logic.
/// Cancels mob-collision push events on both sides so the client never
/// sends a MobCollisionMessage for formation members, eliminating the
/// jitter from soft-collision pushes fighting smooth trail movement.
/// </summary>
public sealed partial class SharedFormationSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FormationMemberComponent, AttemptMobCollideEvent>(OnAttemptCollide);
        SubscribeLocalEvent<FormationMemberComponent, AttemptMobTargetCollideEvent>(OnAttemptTargetCollide);
    }

    private static void OnAttemptCollide(Entity<FormationMemberComponent> ent, ref AttemptMobCollideEvent args)
        => args.Cancelled = true;

    private static void OnAttemptTargetCollide(Entity<FormationMemberComponent> ent, ref AttemptMobTargetCollideEvent args)
        => args.Cancelled = true;
}
