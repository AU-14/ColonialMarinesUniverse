using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Content.Client.CMU14.ThreeD.Scene;
using NUnit.Framework;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DMouseCaptureTest
{
    [Test]
    public void CachedHiddenPopupsDoNotPreventCapture()
    {
        var root = Headless<WindowRoot>();
        var popup = Headless<Popup>(root, false);
        Assert.That(CMU3DLiveSceneSystem.CanCaptureMouse(true, null, [popup]), Is.True);

        Visible(popup, true);
        Assert.That(CMU3DLiveSceneSystem.CanCaptureMouse(true, null, [popup]), Is.False);

        // Popup.Close hides the control but leaves it attached in the cached-popup path.
        Visible(popup, false);
        Assert.That(popup.Parent, Is.SameAs(root));
        Assert.That(CMU3DLiveSceneSystem.CanCaptureMouse(true, null, [popup]), Is.True);
    }

    [Test]
    public void ConsoleAndChatBlockOnlyWhileTheirFocusIsVisible()
    {
        var root = Headless<WindowRoot>();
        var console = Headless<Control>(root);
        var input = Headless<LineEdit>(console);
        Assert.That(CMU3DLiveSceneSystem.CanCaptureMouse(true, input, []), Is.False);
        Visible(console, false);
        Assert.That(CMU3DLiveSceneSystem.CanCaptureMouse(true, input, []), Is.True);
    }

    [Test]
    public void LosingWindowFocusAlwaysReleasesCapture()
    {
        Assert.That(CMU3DLiveSceneSystem.CanCaptureMouse(false, null, []), Is.False);
        Assert.That(CMU3DLiveSceneSystem.CanCaptureMouse(true, null, []), Is.True);
    }

    [Test]
    public void ConsoleRequestSurvivesClosingEscapeUntilKeyRelease()
    {
        var requested = true;
        // Request is queued while the console still owns focus.
        Assert.That(CMU3DLiveSceneSystem.NextMouseCapture(false, false, false, true, ref requested), Is.False);
        Assert.That(requested, Is.True);
        Assert.That(CMU3DLiveSceneSystem.NextMouseCapture(false, false, true, true, ref requested), Is.False);
        Assert.That(requested, Is.True);
        // UI may release focus in the key event before the next frame observes held Escape.
        Assert.That(CMU3DLiveSceneSystem.NextMouseCapture(false, true, true, true, ref requested), Is.False);
        Assert.That(requested, Is.True);
        Assert.That(CMU3DLiveSceneSystem.NextMouseCapture(false, true, false, true, ref requested), Is.True);
        Assert.That(requested, Is.False);
    }

    [Test]
    public void EscapeCancelsARequestMadeOverTheWorld()
    {
        var requested = true;
        Assert.That(CMU3DLiveSceneSystem.NextMouseCapture(false, true, true, false, ref requested), Is.False);
        Assert.That(requested, Is.False);
        Assert.That(CMU3DLiveSceneSystem.NextMouseCapture(false, true, false, false, ref requested), Is.False);
    }

    [Test]
    public void EscapeReleasesActiveCaptureWithoutRelockingWhenHeldOrReleased()
    {
        var requested = false;
        Assert.That(CMU3DLiveSceneSystem.NextMouseCapture(true, true, true, false, ref requested), Is.False);
        Assert.That(requested, Is.False);
        Assert.That(CMU3DLiveSceneSystem.NextMouseCapture(false, true, true, false, ref requested), Is.False);
        Assert.That(CMU3DLiveSceneSystem.NextMouseCapture(false, true, false, false, ref requested), Is.False);
    }

    [Test]
    public void FocusOrModalReleaseRequiresANewCaptureRequest()
    {
        var requested = false;
        Assert.That(CMU3DLiveSceneSystem.NextMouseCapture(true, false, false, false, ref requested), Is.False);
        Assert.That(requested, Is.False);
        Assert.That(CMU3DLiveSceneSystem.NextMouseCapture(false, true, false, false, ref requested), Is.False);
        requested = true;
        Assert.That(CMU3DLiveSceneSystem.NextMouseCapture(false, true, false, false, ref requested), Is.True);
        Assert.That(requested, Is.False);
    }

    [Test]
    public void DiagnosticsIdentifyVisibleBlockersAndIgnoreCachedHiddenControls()
    {
        var root = Headless<WindowRoot>();
        var popup = Headless<Popup>(root);
        var input = Headless<LineEdit>(root);
        Assert.That(CMU3DLiveSceneSystem.MouseCaptureBlocker(false, input, [popup]), Is.EqualTo("game window is not focused"));
        Assert.That(CMU3DLiveSceneSystem.MouseCaptureBlocker(true, input, [popup]), Is.EqualTo("keyboard focus belongs to LineEdit"));
        Assert.That(CMU3DLiveSceneSystem.MouseCaptureBlocker(true, null, [popup]), Is.EqualTo("visible modal Popup"));
        Visible(popup, false);
        Visible(input, false);
        Assert.That(CMU3DLiveSceneSystem.MouseCaptureBlocker(true, input, [popup]), Is.Null);
    }

    // Only the real Control.VisibleInTree traversal runs; no renderer or UI service is required.
    private static T Headless<T>(Control? parent = null, bool visible = true) where T : Control
    {
        var control = (T) RuntimeHelpers.GetUninitializedObject(typeof(T));
        typeof(Control).GetField("<Parent>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(control, parent);
        Visible(control, visible);
        return control;
    }

    private static void Visible(Control control, bool visible) =>
        typeof(Control).GetField("_visible", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(control, visible);
}
