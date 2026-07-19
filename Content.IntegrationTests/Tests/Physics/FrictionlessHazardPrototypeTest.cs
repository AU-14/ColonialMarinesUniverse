#nullable enable

using Content.Shared.Friction;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Physics;

[TestFixture]
public sealed class FrictionlessHazardPrototypeTest
{
    private static readonly EntProtoId[] HazardPrototypes =
    [
        "Singularity",
        "TeslaEnergyBall",
        "TeslaMiniEnergyBall",
    ];

    [Test]
    public async Task HazardsIgnoreTileFriction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var prototypeId in HazardPrototypes)
                {
                    var prototype = server.ProtoMan.Index(prototypeId);
                    Assert.That(
                        prototype.TryComp<TileFrictionModifierComponent>(
                            out var modifier,
                            server.EntMan.ComponentFactory),
                        Is.True,
                        $"{prototypeId} must ignore tile friction.");
                    Assert.That(
                        modifier?.Modifier,
                        Is.EqualTo(0f),
                        $"{prototypeId} must use a zero tile-friction modifier.");
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
