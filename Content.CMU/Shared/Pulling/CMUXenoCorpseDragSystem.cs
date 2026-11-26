using Content.Shared._RMC14.Xenonids;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Physics.Components;

namespace Content.Shared.CMU14.Pulling;

public sealed partial class CMUXenoCorpseDragSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PullableComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    public float GetCorpseDragMultiplier(EntityUid target)
    {
        if (!TryComp<XenoComponent>(target, out var xeno) || !_mobState.IsDead(target))
            return 1f;

        return xeno.CorpseDragMultiplier;
    }

    public bool IsPullerMoving(EntityUid puller)
    {
        // A released movement key can leave the puller coasting or finishing a tile step.
        const float stationarySpeed = 0.1f;
        return TryComp<InputMoverComponent>(puller, out var mover) && mover.HasDirectionalMovement ||
               TryComp<PhysicsComponent>(puller, out var body) &&
               body.LinearVelocity.LengthSquared() > stationarySpeed * stationarySpeed;
    }

    public bool MustStopToHandleCorpse(EntityUid puller, EntityUid target)
        => GetCorpseDragMultiplier(target) < 1f && IsPullerMoving(puller);

    private void OnMobStateChanged(Entity<PullableComponent> ent, ref MobStateChangedEvent args)
    {
        if (ent.Comp.Puller is { } puller &&
            (args.OldMobState == MobState.Dead || args.NewMobState == MobState.Dead) &&
            TryComp<XenoComponent>(ent, out var xeno) && xeno.CorpseDragMultiplier < 1f)
        {
            _speed.RefreshMovementSpeedModifiers(puller);
        }
    }
}
