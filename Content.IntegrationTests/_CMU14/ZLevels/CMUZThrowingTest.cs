#pragma warning disable RA0002 // Regression setup controls the thrower's view.

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.Hands.Systems;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Gravity;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
[TestOf(typeof(CMUSharedZLevelsSystem))]
public sealed class CMUZThrowingTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestCase(false)]
    [TestCase(true)]
    public async Task HeldSatchelOnlyRisesToRequestedLevel(bool lookUp)
    {
        var levels = new EntityUid[3];
        Entity<CMUZLevelsNetworkComponent> network = default;
        var satchel = EntityUid.Invalid;
        var expectedLevel = lookUp ? 1 : 0;
        var highestLevel = 0;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var maps = Server.System<SharedMapSystem>();
                var zLevels = Server.System<CMUZLevelsSystem>();
                var tiles = Server.ResolveDependency<ITileDefinitionManager>();
                var floor = new Tile(tiles["Plating"].TileId);
                network = zLevels.CreateZNetwork();
                var depths = new Dictionary<EntityUid, int>();

                for (var i = 0; i < levels.Length; i++)
                {
                    var map = maps.CreateMap(runMapInit: true);
                    levels[i] = map;
                    var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
                    var gravity = SEntMan.EnsureComponent<GravityComponent>(map);
                    gravity.Enabled = true;
                    gravity.Inherent = true;
                    maps.SetTile(map, grid, new Vector2i(8, 8), floor);

                    // Open sky above the thrower, with a roof beside them to catch upward throws.
                    if (i < 2)
                    {
                        for (var x = i == 0 ? -1 : 1; x <= 8; x++)
                        {
                            for (var y = -1; y <= 1; y++)
                                maps.SetTile(map, grid, new Vector2i(x, y), floor);
                        }
                    }

                    depths.Add(map, i);
                }

                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, depths), Is.True);
                var origin = new EntityCoordinates(levels[0], new Vector2(0.5f));
                var thrower = SEntMan.SpawnEntity("MobHuman", origin);
                satchel = SEntMan.SpawnEntity("CMSatchel", origin);
                var hands = Server.System<HandsSystem>();
                Assert.That(hands.TryPickupAnyHand(thrower, satchel), Is.True);
                SEntMan.EnsureComponent<CMUZLevelViewerComponent>(thrower).LookUp = lookUp;
                Server.System<CMUZLevelShootingSystem>().SetShootDown(thrower, false);

                Assert.That(hands.ThrowHeldItem(thrower,
                    new EntityCoordinates(levels[0], new Vector2(4.5f, 0.5f))), Is.True);
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(satchel), Is.True,
                    "The satchel must enter the actual throw lifecycle.");
            });

            // Observe the flight as well as its landing: an extra rise can fall back to the intended roof.
            for (var tick = 0; tick < 120; tick++)
            {
                await Server.WaitRunTicks(1);
                await Server.WaitAssertion(() =>
                {
                    var map = SEntMan.GetComponent<TransformComponent>(satchel).MapUid!.Value;
                    var depth = SEntMan.GetComponent<CMUZLevelMapComponent>(map).Depth;
                    highestLevel = Math.Max(highestLevel, depth);
                    Assert.That(depth, Is.LessThanOrEqualTo(expectedLevel),
                        "A throw must not rise above the level requested by the thrower.");
                });
            }

            await Server.WaitAssertion(() =>
            {
                Assert.That(highestLevel, Is.EqualTo(expectedLevel),
                    "Looking up must still allow a throw onto the adjacent roof.");
                Assert.That(SEntMan.GetComponent<TransformComponent>(satchel).MapUid,
                    Is.EqualTo(levels[expectedLevel]), "The satchel must land on the requested level.");
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(satchel), Is.False,
                    "The check must include the completed throw.");
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                foreach (var level in levels)
                {
                    if (level.Valid && !SEntMan.Deleted(level))
                        SEntMan.DeleteEntity(level);
                }

                if (network.Owner.Valid && !SEntMan.Deleted(network))
                    SEntMan.DeleteEntity(network);
            });
        }
    }
}
