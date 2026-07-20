using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Physics;

[TestFixture]
[TestOf(typeof(TagSystem))]
public sealed class DiagonalWindowTagTest
{
    private static readonly ProtoId<TagPrototype> WindowTag = "Window";

    [Test]
    public async Task BaseDiagonalWindowRetainsWindowIdentity()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var tags = server.System<TagSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var window = entMan.SpawnEntity("WindowDiagonal", map.GridCoords);
            Assert.That(tags.HasTag(window, WindowTag), Is.True);
            entMan.DeleteEntity(window);
        });

        await pair.CleanReturnAsync();
    }
}
