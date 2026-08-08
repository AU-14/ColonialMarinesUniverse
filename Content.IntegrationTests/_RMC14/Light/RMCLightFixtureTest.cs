using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._RMC14.Light;

[TestFixture]
public sealed class RMCLightFixtureTest : GameTest
{
    private static readonly EntProtoId[] FixturePrototypes =
    [
        "RMCLightFixtureAlwaysPowered",
        "RMCLightFixture",
        "RMCLightFixtureSmallAlwaysPowered",
        "RMCLightFixtureSmall",
    ];

    [SidedDependency(Side.Client)] private readonly IComponentFactory _componentFactory = default!;

    [Test]
    [RunOnSide(Side.Client)]
    public void LightOriginsMatchPolygonWallOccluders()
    {
        foreach (var id in FixturePrototypes)
        {
            var prototype = CProtoMan.Index(id);
            Assert.That(prototype.TryGetComponent<PointLightComponent>(out var light, _componentFactory),
                Is.True,
                $"{id} is missing its point light");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(light!.Enabled, Is.True, $"{id} does not create light");
                Assert.That(light.Offset,
                    Is.EqualTo(new Vector2(0, 0.495f)),
                    $"{id} places its light origin inside the polygon wall occluder");
            }
        }
    }
}
