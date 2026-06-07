using System.Numerics;
using System.Reflection;
using System.Linq;
using Content.Client.Viewport;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Tests.Client._CMU14.ZLevels;

[TestFixture]
public sealed class ScalingViewportProjectionTest
{
    [Test]
    public void ScalingViewportDoesNotDirectlyInjectEntitySystems()
    {
        var injectedEntitySystems = typeof(ScalingViewport)
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field => field.GetCustomAttribute<DependencyAttribute>() != null)
            .Where(field => typeof(IEntitySystem).IsAssignableFrom(field.FieldType))
            .Select(field => field.Name)
            .ToArray();

        Assert.That(injectedEntitySystems, Is.Empty);
    }

    [Test]
    public void ZLevelRenderPassesAreSuppressedDuringPlacement()
    {
        Assert.That(ScalingViewport.ShouldUseZLevelRenderPasses(placementActive: true, zLevelsEnabled: true, renderEnabled: true), Is.False);
        Assert.That(ScalingViewport.ShouldUseZLevelRenderPasses(placementActive: false, zLevelsEnabled: true, renderEnabled: true), Is.True);
        Assert.That(ScalingViewport.ShouldUseZLevelRenderPasses(placementActive: false, zLevelsEnabled: false, renderEnabled: true), Is.False);
        Assert.That(ScalingViewport.ShouldUseZLevelRenderPasses(placementActive: false, zLevelsEnabled: true, renderEnabled: false), Is.False);
    }

    [Test]
    public void ZLevelRenderPassProjectsInputThroughBaseEye()
    {
        var baseEye = new Eye
        {
            Position = new MapCoordinates(new Vector2(10, 20), new MapId(4)),
        };
        var zEye = new ScalingViewport.ZEye
        {
            Position = new MapCoordinates(new Vector2(10, 20), new MapId(5)),
        };

        var projectionEye = ScalingViewport.GetInputProjectionEye(baseEye, zEye);
        var projected = ScalingViewport.ProjectViewportLocalToMap(
            new Vector2(100, 100),
            new Vector2i(200, 200),
            Vector2.One,
            projectionEye!);

        Assert.That(projectionEye, Is.SameAs(baseEye));
        Assert.That(projected.MapId, Is.EqualTo(new MapId(4)));
        Assert.That(projected.Position, Is.EqualTo(new Vector2(10, 20)));
    }

    [Test]
    public void NormalRenderPassProjectsInputThroughRenderEye()
    {
        var baseEye = new Eye
        {
            Position = new MapCoordinates(new Vector2(10, 20), new MapId(4)),
        };
        var renderEye = new Eye
        {
            Position = new MapCoordinates(new Vector2(30, 40), new MapId(5)),
        };

        var projectionEye = ScalingViewport.GetInputProjectionEye(baseEye, renderEye);

        Assert.That(projectionEye, Is.SameAs(renderEye));
    }
}
