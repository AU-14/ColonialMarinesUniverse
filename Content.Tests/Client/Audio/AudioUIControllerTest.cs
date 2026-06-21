using System;
using System.Reflection;
using Content.Client.Audio;
using Moq;
using NUnit.Framework;
using Robust.Client.ResourceManagement;

namespace Content.Tests.Client.Audio;

[TestFixture]
public sealed class AudioUIControllerTest
{
    [Test]
    public void DefaultUiSoundLoadFailureDoesNotRetryThrowingFallback()
    {
        const string path = "/Audio/UserInterface/hover.ogg";

        var controller = new AudioUIController();
        var cache = new Mock<IResourceCache>(MockBehavior.Strict);
        AudioResource missing = null!;
        cache.Setup(x => x.TryGetResource(path, out missing))
            .Returns(false);
        cache.Setup(x => x.GetResource<AudioResource>(path, It.IsAny<bool>()))
            .Throws(new InvalidOperationException("Fallback resource was retried."));

        SetPrivateField(controller, "_cache", cache.Object);

        var method = typeof(AudioUIController).GetMethod(
            "GetSoundOrFallback",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        object result = null!;
        Assert.DoesNotThrow(() => result = method!.Invoke(controller, new object[] { path, path }));
        Assert.That(result, Is.Null);
        cache.Verify(x => x.GetResource<AudioResource>(path, It.IsAny<bool>()), Times.Never);
    }

    private static void SetPrivateField<T>(object instance, string fieldName, T value)
    {
        var field = instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null);
        field!.SetValue(instance, value);
    }
}
