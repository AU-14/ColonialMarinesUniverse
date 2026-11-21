using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.Projectiles;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Client._RMC14.Weapons.Ranged.Prediction;

public sealed partial class GunPredictionSystem
{
    private void PredictFromContacts(Entity<PredictedProjectileClientComponent> ent, ProjectileComponent projectile,
        PhysicsComponent physics, MapId projectileMap)
    {
        HashSet<(NetEntity, MapCoordinates)>? hits = null;
        var firstHit = EntityUid.Invalid;
        var contacts = _physics.GetContacts(ent.Owner);
        while (contacts.MoveNext(out var contact))
        {
            var ourA = contact.EntityA == ent.Owner;
            var ourFixture = ourA ? contact.FixtureAId : contact.FixtureBId;
            var otherFixture = ourA ? contact.FixtureB : contact.FixtureA;
            // Fly-by sensors and soft targets should never consume a projectile.
            if (ourFixture != SharedProjectileSystem.ProjectileFixture || otherFixture is not { Hard: true })
                continue;

            var other = ourA ? contact.EntityB : contact.EntityA;
            if (!TryGetPredictedHit(projectile, other, projectileMap, out var hit))
                continue;

            if (hits == null)
            {
                hits = [];
                firstHit = other;
            }
            hits.Add(hit);
        }

        if (hits != null)
            PredictHit(ent, projectile, physics, firstHit, hits);
    }

    private bool TryGetPredictedHit(ProjectileComponent projectile, EntityUid target, MapId projectileMap,
        out (NetEntity Id, MapCoordinates Coordinates) hit)
    {
        hit = default;
        if (projectileMap == MapId.Nullspace ||
            _projectile.IsIgnoringSource(projectile, target) ||
            _ignorePredictionHitQuery.HasComp(target) ||
            !TryComp(target, out TransformComponent? transform) || transform.MapID != projectileMap ||
            !TryGetNetEntity(target, out var netTarget) || !netTarget.Value.Valid || netTarget.Value.IsClientSide())
        {
            return false;
        }

        hit = (netTarget.Value, _transform.GetMapCoordinates(transform));
        return true;
    }
}
