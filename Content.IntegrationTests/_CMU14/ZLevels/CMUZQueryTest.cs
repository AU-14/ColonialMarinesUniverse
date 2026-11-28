using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevelBuilding.Pit;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevelBuilding.Pit;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

/// <summary>
/// ARCH-02: the query facade answers every ceiling and opening meaning from one place.
/// </summary>
[TestFixture]
[TestOf(typeof(CMUSharedZLevelsSystem))]
public sealed class CMUZQueryTest : GameTest
{
    private readonly List<EntityUid> _cleanup = new();

    // Columns on the upper level, all at y = 0. The lower level is solid plating everywhere.
    private static readonly Vector2i Empty = new(0, 0);
    private static readonly Vector2i Transparent = new(1, 0);
    private static readonly Vector2i FallThrough = new(2, 0);
    private static readonly Vector2i Solid = new(3, 0);
    private static readonly Vector2i Pit = new(4, 0);
    private static readonly Vector2i FlagOnly = new(5, 0);
    private static readonly Vector2i Hatch = new(6, 0);

    [Test]
    public async Task EachCeilingModeAnswersEachTileAndEntityOpening()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var z = Server.System<CMUZLevelsSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            Tile T(string id) => new(tiles[id].TileId);

            var lower = maps.CreateMap(runMapInit: true);
            _cleanup.Add(lower);
            var upper = maps.CreateMap(runMapInit: true);
            _cleanup.Add(upper);
            var lowerGrid = SEntMan.EnsureComponent<MapGridComponent>(lower);
            var upperGrid = SEntMan.EnsureComponent<MapGridComponent>(upper);
            for (var x = -1; x <= 8; x++)
            for (var y = -1; y <= 1; y++)
            {
                maps.SetTile(lower, lowerGrid, new Vector2i(x, y), T("Plating"));
                maps.SetTile(upper, upperGrid, new Vector2i(x, y), T("Plating"));
            }

            var network = z.CreateZNetwork();
            _cleanup.Add(network.Owner);
            Assert.That(z.TryAddMapsIntoZNetwork(network, new() { [lower] = 0, [upper] = 1 }), Is.True);

            maps.SetTile(upper, upperGrid, Empty, Tile.Empty);
            maps.SetTile(upper, upperGrid, Transparent, T("Lattice"));
            maps.SetTile(upper, upperGrid, FallThrough, T("CMUFloorOpenAirSealed"));
            maps.SetTile(upper, upperGrid, FlagOnly, Tile.Empty);
            // The lower floor is solid, so the pit does not need a new level dug under it.
            var pit = SEntMan.SpawnEntity("RUMCPit", maps.GridTileToLocal(upper, upperGrid, Pit));
            Assert.That(SComp<TransformComponent>(pit).Anchored, Is.True);
            var hatch = SEntMan.SpawnEntity("RUMCPitHatchDown", maps.GridTileToLocal(upper, upperGrid, Hatch));
            Assert.That(SComp<TransformComponent>(hatch).Anchored, Is.True);

            // Roof flags under the pit and the flag-only column, whatever the roof writers decided.
            var roofs = Server.System<SharedRoofSystem>();
            var roof = SEntMan.EnsureComponent<RoofComponent>(lower);
            roofs.SetRoof((lower, lowerGrid, roof), Pit, true);
            roofs.SetRoof((lower, lowerGrid, roof), FlagOnly, true);
            roofs.SetRoof((lower, lowerGrid, roof), Empty, false);

            Vector2 At(Vector2i tile) => new(tile.X + 0.5f, tile.Y + 0.5f);
            void Expect(Vector2i tile, bool roofMode, bool fallThrough, bool opening, string what)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(z.IsCeiled(lower, At(tile), CMUZCeilingMode.Roof), Is.EqualTo(roofMode), $"Roof under {what}");
                    Assert.That(z.IsCeiled(lower, At(tile), CMUZCeilingMode.FallThrough), Is.EqualTo(fallThrough), $"FallThrough under {what}");
                    Assert.That(z.IsCeiled(lower, At(tile), CMUZCeilingMode.Opening), Is.EqualTo(opening), $"Opening under {what}");
                });
            }

            Expect(Empty, false, false, false, "an empty tile");
            // A grate holds you up but lets light and the roof through.
            Expect(Transparent, false, true, false, "a transparent tile");
            Expect(FallThrough, false, false, false, "a fall-through tile");
            Expect(Solid, true, true, true, "a solid tile");
            // The pit vetoes the roof even with a roof flag and a solid tile; the tile-only modes ignore entities.
            Expect(Pit, false, true, true, "a pit");
            Expect(FlagOnly, true, false, false, "a roof flag over an empty tile");

            Assert.Multiple(() =>
            {
                Assert.That(z.HasEntityOpening(upper, At(Pit)), Is.True, "A pit is an entity opening.");
                Assert.That(z.HasEntityOpening(upper, At(Solid)), Is.False, "Plain floor is not an entity opening.");
                Assert.That(z.HasEntityOpening((upper, upperGrid), Pit), Is.True);
                Assert.That(z.HasEntityOpening(upper, At(Hatch)), Is.False, "A closed hatch is not an opening.");
            });
            Server.System<HatchSystem>().SetOpen((hatch, SComp<HatchComponent>(hatch)), true);
            Assert.That(z.HasEntityOpening(upper, At(Hatch)), Is.True, "An open hatch is an entity opening.");

            Assert.Multiple(() =>
            {
                Assert.That(z.TryGetSameXY(lower, 1, new Vector2(3.25f, 0.75f), out var up), Is.True);
                Assert.That(up.MapId, Is.EqualTo(SComp<MapComponent>(upper).MapId));
                Assert.That(up.Position, Is.EqualTo(new Vector2(3.25f, 0.75f)));
                Assert.That(z.TryGetSameXY(upper, -1, new Vector2(-0.5f, 1.5f), out var down), Is.True);
                Assert.That(down.MapId, Is.EqualTo(SComp<MapComponent>(lower).MapId));
                Assert.That(down.Position, Is.EqualTo(new Vector2(-0.5f, 1.5f)));
                Assert.That(z.TryGetSameXY(upper, 1, Vector2.Zero, out _), Is.False, "Nothing is above the top level.");
                Assert.That(z.IsCeiled(upper, At(Solid), CMUZCeilingMode.FallThrough), Is.False, "Nothing is above the top level.");
            });

            // Generated non-localized stone: its roof flags mean any level above, so only the tile above counts.
            SEntMan.EnsureComponent<ZGeneratedStoneComponent>(lower).LocalizedToAuthoredLevel = false;
            Assert.Multiple(() =>
            {
                Assert.That(z.IsCeiled(lower, At(FlagOnly), CMUZCeilingMode.Roof), Is.False, "Generated stone ignores its roof flags.");
                Assert.That(z.IsCeiled(lower, At(Solid), CMUZCeilingMode.Roof), Is.True, "Generated stone still sees the tile above.");
                Assert.That(z.IsCeiled(lower, At(Pit), CMUZCeilingMode.Roof), Is.False);
            });
            SEntMan.GetComponent<ZGeneratedStoneComponent>(lower).LocalizedToAuthoredLevel = true;
            Assert.That(z.IsCeiled(lower, At(FlagOnly), CMUZCeilingMode.Roof), Is.True, "Localized stone keeps its roof flags.");
        });
    }

    [TearDown]
    public async Task CleanupScenario()
    {
        for (var i = _cleanup.Count - 1; i >= 0; i--)
            await Pair.DeleteEntityTreeLeafFirst(_cleanup[i]);
        _cleanup.Clear();
    }
}
