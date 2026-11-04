using System;
using System.Reflection;
using Content.Client.Voting;
using Content.Shared.Voting;
using Moq;
using NUnit.Framework;
using Robust.Client;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio.Sources;
using Robust.Shared.Network;

namespace Content.Tests.Client.Voting;

[TestFixture]
public sealed class VoteManagerTest
{
    [Test]
    public void SoundLoadFailureDoesNotPreventStartupOrReceivingVotePermissions()
    {
        const string path = "/Audio/Effects/voteding.ogg";
        var manager = new VoteManager();
        var audio = new Mock<IAudioManager>(MockBehavior.Strict);
        var cache = new Mock<IResourceCache>(MockBehavior.Strict);
        var client = new Mock<IBaseClient>();
        var net = new Mock<IClientNetManager>(MockBehavior.Strict);
        AudioResource missing = null;
        cache.Setup(x => x.TryGetResource(path, out missing)).Returns(false);
        cache.Setup(x => x.GetResource<AudioResource>(path, It.IsAny<bool>()))
            .Throws(new ArgumentException("An item with the same key has already been added. Key: 0"));

        ProcessMessage<MsgVoteCanCall> receivePermissions = null;
        net.Setup(x => x.RegisterNetMessage(It.IsAny<ProcessMessage<MsgVoteData>>(), NetMessageAccept.Both));
        net.Setup(x => x.RegisterNetMessage(It.IsAny<ProcessMessage<MsgVoteCanCall>>(), NetMessageAccept.Both))
            .Callback<ProcessMessage<MsgVoteCanCall>, NetMessageAccept>((callback, _) => receivePermissions = callback);

        SetPrivateField(manager, "_audio", audio.Object);
        SetPrivateField(manager, "_res", cache.Object);
        SetPrivateField(manager, "_client", client.Object);
        SetPrivateField(manager, "_netManager", net.Object);

        Assert.DoesNotThrow(manager.Initialize, "A failed notification sound must not abort client startup.");
        Assert.That(receivePermissions, Is.Not.Null);
        receivePermissions!(new MsgVoteCanCall { CanCall = true, VotesUnavailable = [] });
        Assert.That(manager.CanCallVote, Is.True, "Voting must still receive server messages without audio.");
        client.VerifyAdd(x => x.RunLevelChanged += It.IsAny<EventHandler<RunLevelChangedEventArgs>>(), Times.Once);
        net.Verify(x => x.RegisterNetMessage(It.IsAny<ProcessMessage<MsgVoteData>>(), NetMessageAccept.Both), Times.Once);
        audio.VerifyNoOtherCalls();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void LoadedSoundAllowsStartupWithOrWithoutAnAudioSource(bool sourceAvailable)
    {
        const string path = "/Audio/Effects/voteding.ogg";
        var manager = new VoteManager();
        var audio = new Mock<IAudioManager>(MockBehavior.Strict);
        var source = new Mock<IAudioSource>(MockBehavior.Strict);
        var cache = new Mock<IResourceCache>(MockBehavior.Strict);
        var client = new Mock<IBaseClient>();
        var net = new Mock<IClientNetManager>(MockBehavior.Strict);
        var resource = new AudioResource();
        cache.Setup(x => x.TryGetResource(path, out resource)).Returns(true);
        audio.Setup(x => x.CreateAudioSource(resource.AudioStream))
            .Returns(sourceAvailable ? source.Object : null);
        source.SetupSet(x => x.Global = true);
        net.Setup(x => x.RegisterNetMessage(It.IsAny<ProcessMessage<MsgVoteData>>(), NetMessageAccept.Both));
        net.Setup(x => x.RegisterNetMessage(It.IsAny<ProcessMessage<MsgVoteCanCall>>(), NetMessageAccept.Both));

        SetPrivateField(manager, "_audio", audio.Object);
        SetPrivateField(manager, "_res", cache.Object);
        SetPrivateField(manager, "_client", client.Object);
        SetPrivateField(manager, "_netManager", net.Object);

        Assert.DoesNotThrow(manager.Initialize);
        audio.Verify(x => x.CreateAudioSource(resource.AudioStream), Times.Once);
        source.VerifySet(x => x.Global = true, sourceAvailable ? Times.Once() : Times.Never());
        net.VerifyAll();
        client.VerifyAdd(x => x.RunLevelChanged += It.IsAny<EventHandler<RunLevelChangedEventArgs>>(), Times.Once);
    }

    private static void SetPrivateField<T>(object instance, string fieldName, T value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field!.SetValue(instance, value);
    }
}
