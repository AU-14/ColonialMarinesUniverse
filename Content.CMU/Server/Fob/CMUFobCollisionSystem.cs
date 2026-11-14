using Content.Shared.Physics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Server.CMU14.Fob;

/// <summary>Switches FOB cargo between portable item physics and deployed obstacles.</summary>
public sealed partial class CMUFobCollisionSystem : EntitySystem
{
    [Dependency] private CollisionWakeSystem _collisionWake = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public void SetBlocking(EntityUid uid, bool blocking, CollisionGroup deployedLayer)
    {
        if (!TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        // Static equipment must stay in the broadphase even after it has stopped moving.
        _collisionWake.SetEnabled(uid, !Transform(uid).Anchored);
        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            // Packed cargo still hits walls, but does not become an invisible barrier to players.
            _physics.SetHard(uid, fixture, true, fixtures);
            _physics.SetCollisionLayer(uid, id, fixture, blocking ? (int) deployedLayer : 0, manager: fixtures);
            _physics.SetCollisionMask(uid, id, fixture,
                blocking ? (int) deployedLayer : (int) CollisionGroup.ItemMask, manager: fixtures);
        }
    }
}
