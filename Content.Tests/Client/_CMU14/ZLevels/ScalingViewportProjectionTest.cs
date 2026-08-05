using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
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
    public void ZLevelRenderPassesUseRenderCVarsOnly()
    {
        Assert.That(ScalingViewport.ShouldUseZLevelRenderPasses(zLevelsEnabled: true, renderEnabled: true), Is.True);
        Assert.That(ScalingViewport.ShouldUseZLevelRenderPasses(zLevelsEnabled: false, renderEnabled: true), Is.False);
        Assert.That(ScalingViewport.ShouldUseZLevelRenderPasses(zLevelsEnabled: true, renderEnabled: false), Is.False);
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

    [Test]
    public void LowerRenderGraceIsScopedToTheExactView()
    {
        var eye = new Eye();
        var identity = new ScalingViewport.ZLevelViewIdentity(
            eye,
            new EntityUid(1),
            new EntityUid(2),
            new EntityUid(3));
        var mismatchedIdentities = new[]
        {
            new ScalingViewport.ZLevelViewIdentity(new Eye(), identity.ViewEntity, identity.BaseMapUid, identity.NetworkUid),
            new ScalingViewport.ZLevelViewIdentity(eye, new EntityUid(4), identity.BaseMapUid, identity.NetworkUid),
            new ScalingViewport.ZLevelViewIdentity(eye, identity.ViewEntity, new EntityUid(5), identity.NetworkUid),
            new ScalingViewport.ZLevelViewIdentity(eye, identity.ViewEntity, identity.BaseMapUid, new EntityUid(6)),
        };

        foreach (var mismatch in mismatchedIdentities)
        {
            var grace = new ScalingViewport.LowerRenderGraceState();
            var initialDepth = grace.Resolve(
                identity,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1),
                maxDepth: 3,
                hasLowerMap: true,
                discoveredLowestDepth: -2,
                out var initiallyActive);
            var mismatchedDepth = grace.Resolve(
                mismatch,
                TimeSpan.FromSeconds(1.1),
                TimeSpan.FromSeconds(1),
                maxDepth: 3,
                hasLowerMap: true,
                discoveredLowestDepth: 0,
                out var mismatchActive);

            Assert.Multiple(() =>
            {
                Assert.That(initialDepth, Is.EqualTo(-2));
                Assert.That(initiallyActive, Is.False);
                Assert.That(mismatchedDepth, Is.Zero);
                Assert.That(mismatchActive, Is.False);
            });
        }
    }

    [Test]
    public void LowerRenderGraceReusesDepthForTheSameViewWithinDeadline()
    {
        var identity = new ScalingViewport.ZLevelViewIdentity(
            new Eye(),
            new EntityUid(1),
            new EntityUid(2),
            new EntityUid(3));
        var grace = new ScalingViewport.LowerRenderGraceState();
        grace.Resolve(
            identity,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            maxDepth: 3,
            hasLowerMap: true,
            discoveredLowestDepth: -3,
            out _);

        var depth = grace.Resolve(
            identity,
            TimeSpan.FromSeconds(1.1),
            TimeSpan.FromSeconds(1),
            maxDepth: 2,
            hasLowerMap: true,
            discoveredLowestDepth: 0,
            out var active);

        Assert.Multiple(() =>
        {
            Assert.That(depth, Is.EqualTo(-2));
            Assert.That(active, Is.True);
        });
    }

    [Test]
    public void RenderVisibilitySnapshotRequiresTheExactViewAndMap()
    {
        var eye = new Eye();
        var identity = new ScalingViewport.ZLevelViewIdentity(
            eye,
            new EntityUid(1),
            new EntityUid(2),
            new EntityUid(3));
        var mapId = new MapId(4);
        var state = new ScalingViewport.ZLevelRenderVisibilityState();
        state.Publish(identity, mapId, new[] { -2, -1 });

        var depths = new List<int>();
        var matched = state.TryCopyRenderedLowerDepths(identity, mapId, depths);

        Assert.Multiple(() =>
        {
            Assert.That(matched, Is.True);
            Assert.That(depths, Is.EqualTo(new[] { -2, -1 }));
        });

        var mismatchedIdentity = new ScalingViewport.ZLevelViewIdentity(
            new Eye(),
            identity.ViewEntity,
            identity.BaseMapUid,
            identity.NetworkUid);
        Assert.That(
            state.TryCopyRenderedLowerDepths(mismatchedIdentity, mapId, depths),
            Is.False);
        Assert.That(depths, Is.Empty);
        Assert.That(
            state.TryCopyRenderedLowerDepths(identity, new MapId(5), depths),
            Is.False);
        Assert.That(depths, Is.Empty);
    }

    [Test]
    public void InvalidatedRenderVisibilitySnapshotCannotBeConsumed()
    {
        var identity = new ScalingViewport.ZLevelViewIdentity(
            new Eye(),
            new EntityUid(1),
            new EntityUid(2),
            new EntityUid(3));
        var state = new ScalingViewport.ZLevelRenderVisibilityState();
        state.Publish(identity, new MapId(4), new[] { -1 });
        state.Invalidate();

        var depths = new List<int>();
        Assert.That(
            state.TryCopyRenderedLowerDepths(identity, new MapId(4), depths),
            Is.False);
        Assert.That(depths, Is.Empty);
    }
}
