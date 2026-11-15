using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.ViewVariables;

namespace Content.Shared.CMU14.Physics;

/// <summary>
/// Routes ordinary VV body-type paths through physics so edits update contacts and spatial trees.
/// Type handlers do not intercept the engine's separate direct-member editing interface.
/// </summary>
public sealed partial class CMUPhysicsViewVariablesSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IViewVariablesManager _viewVariables = default!;

    public override void Initialize()
    {
        base.Initialize();
        _viewVariables.GetTypeHandler<PhysicsComponent>().AddPath(nameof(PhysicsComponent.BodyType),
            (_, body) => body.BodyType, SetBodyType);
    }

    public override void Shutdown()
    {
        _viewVariables.GetTypeHandler<PhysicsComponent>().RemovePath(nameof(PhysicsComponent.BodyType));
        base.Shutdown();
    }

    private void SetBodyType(EntityUid uid, BodyType value, PhysicsComponent? body)
    {
        // Although BodyType is marked Flags, the solver accepts only individual supported values.
        if (value is not (BodyType.Kinematic or BodyType.KinematicController or BodyType.Static or BodyType.Dynamic))
            throw new ArgumentException("A physics body must have exactly one supported body type.", nameof(value));

        _physics.SetBodyType(uid, value, body: body);
    }
}
