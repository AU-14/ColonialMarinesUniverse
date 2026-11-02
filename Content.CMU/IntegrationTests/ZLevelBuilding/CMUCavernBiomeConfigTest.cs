using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.ZLevelBuilding;
using Content.Server.GameTicking.Presets;
using Content.Shared.CMU14.ZLevelBuilding;

namespace Content.IntegrationTests.CMU.ZLevelBuilding;

public sealed class CMUCavernBiomeConfigTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Connected = true };

    [Test]
    public async Task PreviewLeavesPaletteEditableButGeneratedWorldLocksItsRecipe()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var config = Server.System<CMUCavernConfigSystem>();
            var round = Server.System<AuRoundSystem>();
            var previous = round.SelectedPreset;
            try
            {
                round.SetPreset(SProtoMan.Index<GamePresetPrototype>("Insurgency"));
                Assert.That(config.Configure("seed", "12345", null, out var feedback), Is.True, feedback);
                Assert.That(config.Configure("preset", "Volcanic", null, out feedback), Is.True, feedback);
                Assert.That(config.Preview("-5", out feedback), Is.True, feedback);
                Assert.That(config.Configure("biome", "LavaTubes", "0", out feedback), Is.True,
                    "Previewing cannot materialize a world or lock its palette.");

                var world = SEntMan.EnsureComponent<CMUCavernWorldComponent>(map.MapUid);
                config.Apply(world, new CMUCavernSettings());
                var field = new CMUCavernField(world.Seed, world.Settings!);
                for (var x = -3; x <= 3; x++)
                for (var y = -3; y <= 3; y++)
                foreach (var chamber in field.GetRegionPlan(new Vector2i(x, y), -5).Chambers)
                    Assert.That(chamber.Variant, Is.Not.EqualTo(CMUCavernVariant.LavaTubes),
                        "The lobby's disabled biome must stay disabled in the world's actual generation recipe.");
                Assert.That(config.Configure("preset", "Flooded", null, out _), Is.False,
                    "A generated world cannot accept a new palette, even during the lobby.");
                Assert.That(config.Configure("seed", "54321", null, out _), Is.False);
                var replay = new CMUCavernField(12345, world.Settings!);
                Assert.That(field.GetRegionPlan(Vector2i.Zero, -5).Chambers,
                    Is.EqualTo(replay.GetRegionPlan(Vector2i.Zero, -5).Chambers));
            }
            finally
            {
                SEntMan.RemoveComponent<CMUCavernWorldComponent>(map.MapUid);
                config.Configure("reset", "", null, out _);
                round.SetPreset(previous);
            }
        });
    }
}
