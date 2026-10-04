using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Buckle.Components;

namespace Content.Shared._CMU14.ZLevels.Core.EntitySystems;

public abstract partial class CMUSharedZLevelsSystem
{
    private void InitBuckle()
    {
        SubscribeLocalEvent<CMUZPhysicsComponent, BuckledEvent>(OnZPhysicsBuckled);
        SubscribeLocalEvent<CMUZPhysicsComponent, UnbuckledEvent>(OnZPhysicsUnbuckled);
    }

    private void OnZPhysicsBuckled(Entity<CMUZPhysicsComponent> ent, ref BuckledEvent args)
    {
        if (TrySyncZPhysicsWithStrap(ent, args.Strap.Owner))
            RemCompDeferred<CMUZFallingComponent>(ent.Owner);
    }

    private void OnZPhysicsUnbuckled(Entity<CMUZPhysicsComponent> ent, ref UnbuckledEvent args)
    {
        if (!TrySyncZPhysicsWithStrap(ent, args.Strap.Owner))
            return;

        Entity<CMUZPhysicsComponent?> nullableEnt = (ent.Owner, ent.Comp);
        WakeZPhysics(nullableEnt);
    }

    private bool TrySyncZPhysicsWithStrap(Entity<CMUZPhysicsComponent> ent, EntityUid strap)
    {
        if (!TryComp<CMUZPhysicsComponent>(strap, out var strapZPhysics))
            return false;

        var oldVelocity = ent.Comp.Velocity;
        var oldHeight = ent.Comp.LocalPosition;

        ent.Comp.Velocity = strapZPhysics.Velocity;
        ent.Comp.LocalPosition = strapZPhysics.LocalPosition;
        DirtyZPhysics(ent.Owner, ent.Comp, oldVelocity, oldHeight);
        return true;
    }
}
