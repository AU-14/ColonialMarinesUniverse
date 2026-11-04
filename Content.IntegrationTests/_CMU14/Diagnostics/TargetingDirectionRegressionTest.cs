using Content.Client._RMC14.Weapons.Ranged.Targeting;
using Content.Shared._RMC14.Targeting;
using Robust.Client.Graphics;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class TargetingDirectionRegressionTest
{
    [TestCase("empty")]
    [TestCase("unresolved")]
    [TestCase("deleted")]
    [TestCase("other-map")]
    public async Task TargetDirectionRequiresALiveOriginOnTheSameMap(string originState)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var map = maps.CreateMap(out _, runMapInit: true);
            var target = entities.SpawnEntity(null, new EntityCoordinates(map, Vector2.Zero));
            var targeted = entities.AddComponent<RMCTargetedComponent>(target);
            var origin = entities.SpawnEntity(null, new EntityCoordinates(map, Vector2.UnitX));
            if (originState == "other-map")
                entities.System<SharedTransformSystem>().SetCoordinates(origin,
                    new EntityCoordinates(maps.CreateMap(out _, runMapInit: true), Vector2.One));
            else if (originState == "deleted")
                entities.DeleteEntity(origin);
            if (originState != "empty")
                targeted.TargetedBy.Add(originState == "unresolved" ? EntityUid.Invalid : origin);

            var overlay = pair.Client.ResolveDependency<IOverlayManager>().GetOverlay<TargetingOverlay>();
            Assert.That(overlay.TryGetTargetDirection((target, targeted, entities.GetComponent<TransformComponent>(target)), out _), Is.False);

            // A remaining visible targeter still supplies the direction when another leaves PVS.
            var visible = entities.SpawnEntity(null, new EntityCoordinates(map, Vector2.UnitY));
            targeted.TargetedBy.Insert(0, visible);
            Assert.That(overlay.TryGetTargetDirection((target, targeted, entities.GetComponent<TransformComponent>(target)), out _), Is.True);
        });
        await pair.CleanReturnAsync();
    }
}
