#pragma warning disable RA0002 // Regression tests deliberately inspect and configure component state.

using System.Numerics;
using Content.Client._RMC14.Water;
using Content.Client.Graphics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Water;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared.Buckle.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics; // CMU14: empty-fixture surface regression.
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
[NonParallelizable]
public sealed class WaterSubmersionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    // CMU14: client surface updates also run on entities without collision fixtures.
    [Test]
    public async Task EmptyFixturesDoNotCrashSurfaceSampling()
    {
        await Client.WaitAssertion(() =>
        {
            var maps = Client.System<SharedMapSystem>();
            var mapUid = maps.CreateMap(out var mapId);
            var grid = maps.CreateGridEntity(mapId);
            var tiles = Client.ResolveDependency<ITileDefinitionManager>();
            maps.SetTile(grid, Vector2i.Zero, new Tile(tiles["Plating"].TileId));
            var coords = new EntityCoordinates(grid, new Vector2(0.5f, 0.5f));
            var user = CEntMan.SpawnEntity(null, coords);
            var fixtures = CEntMan.EnsureComponent<FixturesComponent>(user);
            Assert.That(fixtures.Fixtures, Is.Empty);
            var system = Client.System<RMCWaterSystem>();
            Assert.That(system.TryGetWaterSurface(user, out _, out _, out _), Is.False);

            var water = CEntMan.SpawnEntity("CMFloorDeepWaterEntity", coords);
            Assert.That(system.TryGetWaterSurface(user, out var surface, out var depth, out _), Is.True);
            Assert.That(surface, Is.EqualTo(water), "Anchored water remains detectable without collision fixtures.");
            Assert.That(depth, Is.GreaterThan(0));
            CEntMan.DeleteEntity(mapUid);
        });
    }

    [Test]
    public async Task WaterDepthSoundAndCoverFollowTheOccupiedTile()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var transform = Server.System<SharedTransformSystem>();
            var waterSystem = Server.System<RMCWaterSystem>();
            var shallow = SEntMan.SpawnEntity("CMFloorShallowWaterEntity", map.GridCoords);
            var deepCoords = map.GridCoords.Offset(new Vector2(1, 0));
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            Server.System<SharedMapSystem>().SetTile(map.Grid,
                map.Tile.GridIndices + new Vector2i(1, 0), new Tile(tiles["Plating"].TileId));
            var deep = SEntMan.SpawnEntity("CMFloorDeepWaterEntity", deepCoords);
            var mob = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var mover = SEntMan.GetComponent<InputMoverComponent>(mob);

            AssertSurface(shallow, 8, false);
            AssertSound("RMCWaterWading");
            mover.HeldMoveButtons = MoveButtons.Walk;
            var silent = new GetMobFootstepSoundEvent();
            SEntMan.EventBus.RaiseLocalEvent(mob, ref silent);
            Assert.That(silent.Handled, Is.True);
            Assert.That(silent.Sound, Is.Null, "Walking must not fall back to shoes or water footsteps.");
            mover.HeldMoveButtons = MoveButtons.None;

            transform.SetCoordinates(mob, deepCoords);
            AssertSurface(deep, 18, false);
            AssertSound("RMCWaterDeepWading");
            var catwalk = SEntMan.SpawnEntity("CMCatwalk", deepCoords);
            AssertSurface(deep, 18, true);
            var onBridge = new GetMobFootstepSoundEvent();
            SEntMan.EventBus.RaiseLocalEvent(mob, ref onBridge);
            Assert.That(onBridge.Handled, Is.False, "The catwalk should supply the normal footstep sound.");
            SEntMan.DeleteEntity(catwalk);
            AssertSurface(deep, 18, false);

            // The physics body can overlap the next tile, but visuals must use the mob's center.
            transform.SetCoordinates(mob, map.GridCoords.Offset(new Vector2(-1, 0)));
            Assert.That(waterSystem.TryGetWaterSurface(mob, out _, out _, out _), Is.False);

            void AssertSurface(EntityUid expectedWater, float expectedDepth, bool expectedCovered)
            {
                Assert.That(waterSystem.TryGetWaterSurface(mob, out var water, out var depth, out var covered), Is.True);
                Assert.That(water, Is.EqualTo(expectedWater));
                Assert.That(depth, Is.EqualTo(expectedDepth));
                Assert.That(covered, Is.EqualTo(expectedCovered));
            }

            void AssertSound(string expected)
            {
                var sound = new GetMobFootstepSoundEvent();
                SEntMan.EventBus.RaiseLocalEvent(mob, ref sound);
                Assert.That(sound.Handled, Is.True);
                Assert.That(sound.Sound, Is.TypeOf<SoundCollectionSpecifier>());
                Assert.That(((SoundCollectionSpecifier)sound.Sound!).Collection!.Value.Id, Is.EqualTo(expected));
            }
        });
    }

    [Test]
    public async Task CarriedBuckledAndAirborneMobsDoNotSubmerge()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mob = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var water = Server.System<RMCWaterSystem>();
            Assert.That(water.CanSubmerge(mob), Is.True);
            SEntMan.AddComponent<BeingFiremanCarriedComponent>(mob);
            Assert.That(water.CanSubmerge(mob), Is.False);
            SEntMan.RemoveComponent<BeingFiremanCarriedComponent>(mob);
            var buckle = SEntMan.EnsureComponent<BuckleComponent>(mob);
            buckle.BuckledTo = SEntMan.SpawnEntity(null, map.GridCoords);
            Assert.That(water.CanSubmerge(mob), Is.False);
            buckle.BuckledTo = null;
            var thrown = SEntMan.EnsureComponent<ThrownItemComponent>(mob);
            Assert.That(water.CanSubmerge(mob), Is.False);
            thrown.Landed = true;
            Assert.That(water.CanSubmerge(mob), Is.True);
        });
    }

    [Test]
    public async Task ClientRestoresOffsetsAndKeepsWakesSeparateFromCamouflage()
    {
        await Client.WaitAssertion(() =>
        {
            var maps = Client.System<SharedMapSystem>();
            var mapUid = maps.CreateMap(out var mapId);
            var grid = maps.CreateGridEntity(mapId);
            var tiles = Client.ResolveDependency<ITileDefinitionManager>();
            maps.SetTile(grid, Vector2i.Zero, new Tile(tiles["Plating"].TileId));
            maps.SetTile(grid, new Vector2i(1, 0), new Tile(tiles["Plating"].TileId));
            var coords = new EntityCoordinates(grid, new Vector2(0.5f, 0.5f));
            var water = CEntMan.SpawnEntity("CMFloorDeepWaterEntity", coords);
            var mob = CEntMan.SpawnEntity("MobHuman", coords);
            var sprites = Client.System<SpriteSystem>();
            var visuals = Client.System<RMCWaterVisualsSystem>();
            var sprite = CEntMan.GetComponent<SpriteComponent>(mob);
            var baseOffset = new Vector2(0.1f, 0.2f);
            sprites.SetOffset((mob, sprite), baseOffset);

            visuals.FrameUpdate(1f);
            var visual = CEntMan.GetComponent<RMCWaterVisualsComponent>(mob);
            Assert.That(sprite.Offset, Is.EqualTo(baseOffset - new Vector2(0, 18 / 32f)));
            Assert.That(sprites.HasPostShader((mob, sprite), ContentPostShaderIds.WaterSubmersion), Is.True);
            Assert.That(visual.Splash, Is.Not.Null);
            var wake = visual.Splash!.Value;
            Assert.That(CEntMan.GetComponent<TransformComponent>(wake).ParentUid, Is.EqualTo(grid.Owner));
            CEntMan.AddComponent<EntityTurnInvisibleComponent>(mob);
            Assert.That(sprites.HasPostShader((mob, sprite), "RMCInvisible"), Is.True);
            sprites.SetColor((mob, sprite), Color.White.WithAlpha(0.1f));
            visuals.FrameUpdate(1f);
            Assert.That(CEntMan.GetComponent<SpriteComponent>(wake).Color.A, Is.EqualTo(1));

            CEntMan.EnsureComponent<StandingStateComponent>(mob).Standing = false;
            visuals.FrameUpdate(1f);
            Assert.That(visual.Immersed, Is.True);
            Assert.That(visual.SplashState, Is.EqualTo("bubbles"));
            CEntMan.GetComponent<MobStateComponent>(mob).CurrentState = MobState.Dead;
            visuals.FrameUpdate(1f);
            Assert.That(visual.Splash, Is.Null, "Dead submerged mobs must not emit bubbles.");

            Client.System<SharedTransformSystem>().SetCoordinates(mob, coords.Offset(Vector2.UnitX));
            visuals.FrameUpdate(1f);
            Assert.That(sprite.Offset, Is.EqualTo(baseOffset));
            Assert.That(sprites.HasPostShader((mob, sprite), ContentPostShaderIds.WaterSubmersion), Is.False);
            Assert.That(sprites.HasPostShader((mob, sprite), "RMCInvisible"), Is.True, "Leaving water must retain camouflage.");
            Assert.That(visual.Splash, Is.Null);

            // Xeno rest is separate from StandingState: actual rest actions must submerge larvae.
            CEntMan.DeleteEntity(water);
            CEntMan.SpawnEntity("CMFloorShallowWaterEntity", coords);
            var larva = CEntMan.SpawnEntity("CMXenoLarva", coords);
            visuals.FrameUpdate(1f);
            var larvaVisual = CEntMan.GetComponent<RMCWaterVisualsComponent>(larva);
            Assert.That(larvaVisual.Immersed, Is.False);
            CEntMan.AddComponent<XenoRestingComponent>(larva);
            visuals.FrameUpdate(1f);
            Assert.That(larvaVisual.Immersed, Is.True);
            Assert.That(larvaVisual.SplashState, Is.EqualTo("bubbles"));
            CEntMan.RemoveComponent<XenoRestingComponent>(larva);
            visuals.FrameUpdate(1f);
            Assert.That(larvaVisual.Immersed, Is.False);
            CEntMan.DeleteEntity(mapUid);
        });
    }

    [Test]
    public async Task ImportedResourcesLoadAndWaterPrototypesHaveDepth()
    {
        await Client.WaitAssertion(() =>
        {
            var resources = Client.ResolveDependency<IResourceCache>();
            foreach (var size in new[] { 32, 48, 64, 88 })
            {
                var rsi = resources.GetResource<RSIResource>(new ResPath($"/Textures/_RMC14/Effects/Water/splash{size}.rsi")).RSI;
                foreach (var state in new[] { "coast_shallow", "coast_deep", "shallow", "intermediate", "deep", "bubbles" })
                    Assert.That(rsi.TryGetState(state, out _), Is.True, $"{size}/{state}");
            }
            Assert.That(CProtoMan.Index<ShaderPrototype>("RMCWaterSubmersion").InstanceUnique(), Is.Not.Null);
            foreach (var id in new[] { "CMFloorShallowWaterEntity", "CMFloorDeepWaterEntity", "RMCEntityDesertWaterShallow", "AUEntityShepBeachCornerEdge" })
            {
                var entity = CEntMan.SpawnEntity(id, MapCoordinates.Nullspace);
                Assert.That(CEntMan.GetComponent<RMCWaterComponent>(entity).Depth, Is.GreaterThan(0));
                Assert.That(CEntMan.HasComponent<FloorOccluderComponent>(entity), Is.False);
                CEntMan.DeleteEntity(entity);
            }
        });
    }
}
