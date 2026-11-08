#pragma warning disable RA0002 // Exercise the world's immutable terrain recipe through the generation resolver.
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevelBuilding;

namespace Content.IntegrationTests.CMU14.Performance;

[TestFixture]
public sealed class CMUCavernSamplerTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task WorldSeedAndRecipeChangesPreserveCavernGeometry()
    {
        var first = await Pair.CreateTestMap();
        var second = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var building = Server.System<ZLevelBuildingSystem>();
            var sample = typeof(ZLevelBuildingSystem)
                .GetMethod("GetCavernField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .CreateDelegate<Func<Entity<ZGeneratedStoneComponent>, ZBuildableMapComponent, CMUCavernField?>>(building);

            var firstStone = SEntMan.EnsureComponent<ZGeneratedStoneComponent>(first.MapUid);
            firstStone.StoneGrid = first.Grid.Owner;
            var firstSettings = SEntMan.EnsureComponent<ZBuildableMapComponent>(first.MapUid);
            firstSettings.Caverns = new CMUCavernSettings { StartDepth = 0, RegionSize = 48 };
            var firstWorld = SEntMan.EnsureComponent<CMUCavernWorldComponent>(first.MapUid);
            firstWorld.Seed = 12345;
            firstWorld.Settings = firstSettings.Caverns;

            var secondStone = SEntMan.EnsureComponent<ZGeneratedStoneComponent>(second.MapUid);
            secondStone.StoneGrid = second.Grid.Owner;
            var secondSettings = SEntMan.EnsureComponent<ZBuildableMapComponent>(second.MapUid);
            secondSettings.Caverns = new CMUCavernSettings
            {
                StartDepth = 0, RegionSize = 48, Layout = CMUCavernLayout.Twin,
            };
            var secondWorld = SEntMan.EnsureComponent<CMUCavernWorldComponent>(second.MapUid);
            secondWorld.Seed = 54321;
            secondWorld.Settings = secondSettings.Caverns;

            var original = new CMUCavernField(firstWorld.Seed, firstWorld.Settings);
            Verify(sample((first.MapUid, firstStone), firstSettings)!, original);
            var secondExpected = new CMUCavernField(secondWorld.Seed, secondWorld.Settings);
            Verify(sample((second.MapUid, secondStone), secondSettings)!, secondExpected);
            Verify(sample((first.MapUid, firstStone), firstSettings)!, original);

            firstWorld.Seed = 54321;
            var reseeded = new CMUCavernField(firstWorld.Seed, firstWorld.Settings);
            Assert.That(reseeded.GetRegionPlan(Vector2i.Zero, -1).Chambers,
                Is.Not.EqualTo(original.GetRegionPlan(Vector2i.Zero, -1).Chambers));
            Verify(sample((first.MapUid, firstStone), firstSettings)!, reseeded);

            firstWorld.Settings = new CMUCavernSettings
            {
                StartDepth = 0, RegionSize = 48, Layout = CMUCavernLayout.Rift,
                Variant = CMUCavernVariant.PillarCathedral,
            };
            var changedRecipe = new CMUCavernField(firstWorld.Seed, firstWorld.Settings);
            Assert.That(changedRecipe.GetRegionPlan(Vector2i.Zero, -1).Chambers,
                Is.Not.EqualTo(reseeded.GetRegionPlan(Vector2i.Zero, -1).Chambers));
            Verify(sample((first.MapUid, firstStone), firstSettings)!, changedRecipe);
            Verify(sample((second.MapUid, secondStone), secondSettings)!, secondExpected);
        });
    }

    private static void Verify(CMUCavernField actual, CMUCavernField expected)
    {
        Assert.That(actual.GetRegionPlan(Vector2i.Zero, -1).Chambers,
            Is.EqualTo(expected.GetRegionPlan(Vector2i.Zero, -1).Chambers));
        for (var x = -16; x <= 16; x += 4)
        for (var y = -16; y <= 16; y += 4)
        for (var depth = -1; depth >= -2; depth--)
        {
            var tile = new Vector2i(x, y);
            Assert.That(actual.IsOpen(tile, depth), Is.EqualTo(expected.IsOpen(tile, depth)),
                $"Terrain at {tile}, depth {depth}, must follow this world's current recipe.");
        }
    }
}
