using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Physics;

[TestFixture]
[TestOf(typeof(TagSystem))]
public sealed class DiagonalWindowTagTest
{
    private static readonly ProtoId<TagPrototype> WindowTag = "Window";
    private static readonly string[] DiagonalWindows =
    {
        "WindowDiagonal",
        "ClockworkWindowDiagonal",
        "MiningWindowDiagonal",
        "PlasmaWindowDiagonal",
        "PlastitaniumWindowDiagonal",
        "ReinforcedWindowDiagonal",
        "ReinforcedPlasmaWindowDiagonal",
        "ReinforcedUraniumWindowDiagonal",
        "ShuttleWindowDiagonal",
        "UraniumWindowDiagonal",
    };

    [Test]
    public async Task DiagonalWindowsRetainWindowIdentity()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var tags = server.System<TagSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            foreach (var prototype in DiagonalWindows)
            {
                var window = entMan.SpawnEntity(prototype, map.GridCoords);
                Assert.That(tags.HasTag(window, WindowTag), Is.True, prototype);
                entMan.DeleteEntity(window);
            }
        });

        await pair.CleanReturnAsync();
    }
}
