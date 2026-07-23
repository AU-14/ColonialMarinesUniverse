using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Throwing;

namespace Content.Shared._CMU14.ZLevels.Core.EntitySystems;

public abstract partial class CMUSharedZLevelsSystem
{
    private const float ThrowUpZVelocity = 6.5f;

    private void InitThrowing()
    {
        SubscribeLocalEvent<CMUZPhysicsComponent, ThrownEvent>(OnThrown);
    }

    private void OnThrown(Entity<CMUZPhysicsComponent> ent, ref ThrownEvent args)
    {
        if (args.User is not { } user ||
            !TryComp<CMUZLevelViewerComponent>(user, out var viewer) ||
            !viewer.LookUp)
        {
            return;
        }

        if (ent.Comp.Velocity >= ThrowUpZVelocity)
            return;

        Entity<CMUZPhysicsComponent?> nullableEnt = (ent.Owner, ent.Comp);
        SetZVelocity(nullableEnt, ThrowUpZVelocity);
    }
}
