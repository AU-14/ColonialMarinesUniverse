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

}
