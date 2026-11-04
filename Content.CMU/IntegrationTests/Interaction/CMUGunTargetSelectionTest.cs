using System.Numerics;
using System.Reflection;
using Content.Client.Clickable;
using Content.Client.Gameplay;
using Content.Client.Weapons.Ranged.Systems;
using Content.IntegrationTests.Fixtures;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Target = (Robust.Shared.GameObjects.EntityUid, bool, bool, int, uint, float, float);

namespace Content.IntegrationTests.CMU14.Interaction;

[TestFixture]
public sealed class CMUGunTargetSelectionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public async Task CursorPrecedenceAndFallbackPreserveFixtureFilteringAndTargetOrder()
    {
        var map = await Pair.CreateTestMap();
        var targets = new List<NetEntity>();
        await Server.WaitAssertion(() =>
        {
            var player = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(5, 5)));
            Server.PlayerMan.SetAttachedEntity(ServerSession!, player);
            for (var i = 0; i < 3; i++)
                targets.Add(SEntMan.GetNetEntity(SEntMan.SpawnEntity("CMMobHuman", map.GridCoords)));
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var ids = targets.Select(CEntMan.GetEntity).ToArray();
            var xform = Client.System<SharedTransformSystem>();
            var sprites = Client.System<SpriteSystem>();
            var gun = Client.System<GunSystem>();
            var eye = Client.ResolveDependency<IEyeManager>().CurrentEye;
            eye.Rotation = Angle.Zero;
            var coordinates = xform.GetMapCoordinates(ids[0]);
            var best = typeof(GunSystem).GetMethod("GetBestTarget", Private)!
                .CreateDelegate<Func<IEye, MapCoordinates, NetEntity?>>(gun);
            var cursor = typeof(GunSystem).GetMethod("CMUGetCursorTarget", Private)!
                .CreateDelegate<Func<GameplayStateBase?, IEye, MapCoordinates, NetEntity?>>(gun);
            var check = typeof(GunSystem).GetMethod("CheckTarget", Private)!
                .CreateDelegate<Func<Entity<SpriteComponent, TransformComponent>, IEye, MapCoordinates, Target>>(gun);
            var eligible = typeof(GunSystem).GetMethod("CheckFixtures", Private)!
                .CreateDelegate<Func<Entity<FixturesComponent?>, bool>>(gun);
            var comparer = (IComparer<Target>) typeof(GunSystem).GetField("_comparer", Private)!.GetValue(gun)!;
            var state = new GameplayStateBase();
            IoCManager.InjectDependencies(state);
            typeof(GameplayStateBase).GetField("_comparer", Private)!.SetValue(state,
                Activator.CreateInstance(
                    typeof(GameplayStateBase).GetNestedType("ClickableEntityComparer", BindingFlags.NonPublic)!, true));

            foreach (var id in ids)
            {
                xform.SetWorldPosition(id, coordinates.Position);
                CEntMan.GetComponent<ClickableComponent>(id).Bounds =
                    new ClickableComponent.DirBoundData { All = new Box2(-1, -1, 1, 1) };
                sprites.SetDrawDepth(id, 1);
                CEntMan.GetComponent<SpriteComponent>(id).RenderOrder = 0;
            }

            // Independent legacy reference: collect and sort all eligible candidates, then choose the first.
            NetEntity? LegacyFallback()
            {
                var entries = new List<Target>();
                foreach (var entry in Client.System<SpriteTreeSystem>().QueryAabb(coordinates.MapId,
                             Box2.CenteredAround(coordinates.Position, Vector2.One)))
                {
                    if (eligible(new Entity<FixturesComponent?>(entry.Uid, null)))
                        entries.Add(check((entry.Uid, entry.Component, entry.Transform), eye, coordinates));
                }
                entries.Sort(comparer);
                if (entries.Count == 0 || !entries[0].Item2 && !entries[0].Item3)
                    return null;
                return CEntMan.GetNetEntity(entries[0].Item1);
            }

            void Check(EntityUid expected)
            {
                Assert.That(best(eye, coordinates), Is.EqualTo(CEntMan.GetNetEntity(expected)));
                Assert.That(best(eye, coordinates), Is.EqualTo(LegacyFallback()));
                Assert.That(cursor(state, eye, coordinates), Is.EqualTo(CEntMan.GetNetEntity(expected)));
                Assert.That(cursor(null, eye, coordinates), Is.EqualTo(LegacyFallback()));
            }

            Check(ids.Max());
            sprites.SetDrawDepth(ids[0], 20);
            Check(ids[0]);
            sprites.SetDrawDepth(ids[1], 20);
            CEntMan.GetComponent<SpriteComponent>(ids[1]).RenderOrder = 10;
            Check(ids[1]);
            Client.System<SharedPhysicsSystem>().SetCanCollide(ids[1], false);
            Assert.That(state.GetClickedEntity(coordinates, eye), Is.EqualTo(ids[1]));
            Assert.That(cursor(state, eye, coordinates), Is.EqualTo(CEntMan.GetNetEntity(ids[0])),
                "An ineligible top sprite must fall back to a shootable target below it.");
            Assert.That(best(eye, coordinates), Is.EqualTo(LegacyFallback()));
            foreach (var id in ids)
                Client.System<SharedPhysicsSystem>().SetCanCollide(id, false);
            Assert.That(cursor(state, eye, coordinates), Is.Null);
            Assert.That(cursor(null, eye, coordinates), Is.Null);
            Assert.That(best(eye, new MapCoordinates(coordinates.Position + new Vector2(100), coordinates.MapId)), Is.Null);
        });
    }
}
