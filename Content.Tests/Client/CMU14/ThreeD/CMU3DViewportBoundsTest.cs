using System;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Content.Client.CMU14.ThreeD;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Client.CMU14.ZLevels.Lighting;
using NUnit.Framework;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DViewportBoundsTest
{
    [TestCase(.73f)]
    [TestCase(1.5707963f)]
    [TestCase(3.1415927f)]
    public void ProjectedLightingUsesViewportCornersDuringEyeChanges(float yaw)
    {
        var size = new Vector2(1280, 800);
        var eye = new Eye { Position = new MapCoordinates(new Vector2(57, -31), new MapId(1)), Rotation = new Angle(yaw) };
        var projection = new MapViewport(eye, size);
        // CurrentEye can already belong to the new ghost. Bounds must not depend on it.
        var bounds = CMUZLevelProjectedLightingSystem.ProjectedBounds(projection, Vector2.Zero, size);
        foreach (var pixel in new[] { Vector2.Zero, new Vector2(size.X, 0), new Vector2(0, size.Y), size, size / 2 })
            Assert.That(bounds.Enlarged(.0001f).Contains(projection.ScreenToMap(pixel).Position), Is.True);
        Assert.That(bounds.Contains(eye.Position.Position + new Vector2(1000)), Is.False);
    }

    // Exercise the actual engine routine from the human right-click crash, without opening a GL window.
    // A headless control only needs its layout and projection here; none of its rendering services run.
    [TestCase(0f, -1.2f, false)]
    [TestCase(0f, 1.2f, true)]
    [TestCase(.73f, -.8f, false)]
    [TestCase(.73f, .8f, true)]
    [TestCase(1.5707963f, 0f, false)]
    [TestCase(1.5707963f, 1.2f, true)]
    [TestCase(3.1415927f, -1.2f, false)]
    [TestCase(-2.3f, .4f, true)]
    public void HumanContextMenuViewBoundsRemainValidAtAnyLookAngle(float yaw, float pitch, bool captured)
    {
        var size = new Vector2(1280, 800);
        var eye = new Eye { Position = new MapCoordinates(new Vector2(57, -31), new MapId(1)), Rotation = new Angle(-yaw) };
        var projection = new MapViewport(eye, size);
        var control = (CMU3DSceneControl) RuntimeHelpers.GetUninitializedObject(typeof(CMU3DSceneControl));
        SetField(typeof(Control), control, "_size", size);
        control.MapProjection = projection;
        control.SetCameraOverride(CMU3DFirstPersonCamera.Frame(Vector2.Zero, yaw, pitch, size));
        control.SetMouseCaptured(captured);

        var manager = new EyeManager { MainViewport = control };
        SetField(typeof(EyeManager), manager, "_currentEye", eye);

        // This is the exact call made by ExamineSystem.CanExamine for a living character.
        var bounds = manager.GetWorldViewbounds();
        Assert.That(bounds.Box.Width, Is.EqualTo(32).Within(.0001));
        Assert.That(bounds.Box.Height, Is.EqualTo(20).Within(.0001));
        Assert.That(bounds.Contains(eye.Position.Position), Is.True);
        Assert.That(bounds.Contains(projection.ScreenToMap(size * .75f).Position), Is.True);
        Assert.That(bounds.Contains(eye.Position.Position + new Vector2(1000)), Is.False);
        Assert.That(control.GetWorldToScreenMatrix(), Is.EqualTo(projection.GetWorldToScreenMatrix()));

        var before = bounds;
        control.SetMouseCaptured(!captured);
        Assert.That(manager.GetWorldViewbounds(), Is.EqualTo(before), "Capture must not collapse bounds to the crosshair ray.");
    }

    private static void SetField(Type type, object instance, string name, object value) =>
        type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);

    private sealed class MapViewport(Eye eye, Vector2 size) : IViewportControl
    {
        public IClydeWindow? Window => null;

        public MapCoordinates ScreenToMap(Vector2 point) => new(eye.Position.Position +
            (-eye.Rotation).RotateVec((point - size / 2) * new Vector2(1, -1) / 40), eye.Position.MapId);

        public MapCoordinates PixelToMap(Vector2 point) => throw new InvalidOperationException("Bounds must use the raw map projection.");

        public Vector2 WorldToScreen(Vector2 map) => Vector2.Transform(map, GetWorldToScreenMatrix());

        public Matrix3x2 GetWorldToScreenMatrix() => Matrix3x2.CreateTranslation(-eye.Position.Position) *
            Matrix3x2.CreateRotation((float) eye.Rotation.Theta) * Matrix3x2.CreateScale(40, -40) * Matrix3x2.CreateTranslation(size / 2);

        public Matrix3x2 GetLocalToScreenMatrix() => Matrix3x2.Identity;
    }
}
