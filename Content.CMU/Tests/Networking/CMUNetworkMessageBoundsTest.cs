using System;
using System.Collections.Generic;
using System.IO;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Content.Shared.Eui;
using Content.Shared.Mapping;
using Content.Shared.Players;
using Content.Shared.Players.JobWhitelist;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared._RMC14.LinkAccount;
using Content.Shared._RMC14.Mentor;
using Content.Shared._RMC14.PlayTimeTracking;
using Lidgren.Network;
using Moq;
using NUnit.Framework;
using Robust.Shared.Network;
using Robust.Shared.Network.Messages;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Tests.CMU14.Networking;

[TestFixture]
public sealed class CMUNetworkMessageBoundsTest
{
    private const int ClaimedLength = 1024 * 1024;

    private static IEnumerable<TestCaseData> TruncatedMessages()
    {
        yield return Case(new MsgConCompletion(), prefix: b => b.Write(0));
        yield return Case(new MsgUpdateAdminStatus());
        yield return Case(new MsgChatMessage());
        yield return Case(new MsgDeleteChatMessagesBy(), fixedLength: true, prefix: b => b.Write(0));
        yield return Case(new MsgEuiMessage(), prefix: b => b.Write(1u));
        yield return Case(new MsgEuiState(), prefix: b => b.Write(1u));
        yield return Case(new MsgJobWhitelist());
        yield return Case(new MsgRoleBans());
        yield return Case(new MsgRoleBans(), prefix: b => b.WriteVariableInt32(0), suffix: "AntagBans");
        yield return Case(new MsgPlayTime());
        yield return Case(new MsgUpdateCharacter(), prefix: b => b.Write(0));
        yield return Case(new LinkAccountStatusMsg(), prefix: b =>
        {
            b.Write(true);
            b.WritePadBits();
        });
        yield return Case(new RMCPatronListMsg());
        yield return Case(new MentorMessagesReceivedMsg(), fixedLength: true);
        yield return Case(new RMCExcludedTimersMsg());
        yield return Case(new MappingMapDataMessage(), prefix: b => b.WriteVariableInt32(1));
    }

    [TestCaseSource(nameof(TruncatedMessages))]
    public void TruncatedLengthIsRejectedBeforeAllocation(NetMessage message, NetIncomingMessage buffer)
    {
        // ReadFromBuffer must reject the length before allocating its claimed payload.
        // Keep the claim finite so a regressed test cannot exhaust the test process.
        Exception? error = null;
        var before = GC.GetAllocatedBytesForCurrentThread();
        try
        {
            message.ReadFromBuffer(buffer, null!);
        }
        catch (Exception exception)
        {
            error = exception;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.TypeOf<InvalidOperationException>());
            Assert.That(allocated, Is.LessThan(ClaimedLength / 2),
                "Reject malformed lengths before allocating a payload or collection.");
        });
    }

    [Test]
    public void NegativeCollectionLengthIsRejected()
    {
        var buffer = Incoming();
        buffer.WriteVariableInt32(-1);
        Assert.Throws<InvalidOperationException>(() => new MsgRoleBans().ReadFromBuffer(buffer, null!));
    }

    [Test]
    public void ValidCollectionsRoundTripIncludingUnalignedMentorMessages()
    {
        var roles = new MsgRoleBans();
        roles.JobBans.Add("Marine");
        roles.AntagBans.Add("Traitor");
        var receivedRoles = RoundTrip(roles, new MsgRoleBans());
        Assert.That(receivedRoles.JobBans, Is.EqualTo(roles.JobBans));
        Assert.That(receivedRoles.AntagBans, Is.EqualTo(roles.AntagBans));

        var mentors = new MentorMessagesReceivedMsg();
        var destination = new NetUserId(Guid.Parse("b15a7c70-c0ea-4934-a01c-f61425aaa274"));
        var time = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);
        mentors.Messages.Add(new MentorMessage(destination, "", null, "", "", time, false, false, true));
        mentors.Messages.Add(new MentorMessage(destination, "Marine", destination, "Mentor", "Help", time, true, false, false));
        var receivedMentors = RoundTrip(mentors, new MentorMessagesReceivedMsg());
        Assert.That(receivedMentors.Messages, Is.EqualTo(mentors.Messages));
    }

    [Test]
    public void ExactLengthPayloadReachesDeserializer()
    {
        var buffer = Incoming();
        buffer.Write(7u);
        buffer.WriteVariableInt32(3);
        buffer.Write(new byte[] { 10, 20, 30 });
        var expected = new CloseEuiMessage();
        var serializer = new Mock<IRobustSerializer>(MockBehavior.Strict);
        serializer.Setup(s => s.Deserialize<EuiMessageBase>(It.IsAny<Stream>()))
            .Returns((Stream stream) =>
            {
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                Assert.That(copy.ToArray(), Is.EqualTo(new byte[] { 10, 20, 30 }));
                return expected;
            });

        var message = new MsgEuiMessage();
        message.ReadFromBuffer(buffer, serializer.Object);
        Assert.That(message.Id, Is.EqualTo(7));
        Assert.That(message.Message, Is.SameAs(expected));
        serializer.VerifyAll();
    }

    [Test]
    public void MapLengthHintDoesNotAllocateBeforeDecompression()
    {
        var buffer = CompressedMap(ClaimedLength);

        Exception? error = null;
        var before = GC.GetAllocatedBytesForCurrentThread();
        try
        {
            new MappingMapDataMessage().ReadFromBuffer(buffer, null!);
        }
        catch (Exception exception)
        {
            error = exception;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.TypeOf<InvalidOperationException>());
            Assert.That(allocated, Is.LessThan(ClaimedLength / 2));
        });
    }

    [Test]
    public void ValidCompressedMapReachesDeserializer()
    {
        var expected = "map data";
        var serializer = new Mock<IRobustSerializer>(MockBehavior.Strict);
        serializer.Setup(s => s.DeserializeDirect(
            It.Is<Stream>(stream => stream.Length == 3 && stream.Position == 0), out expected));
        var message = new MappingMapDataMessage();
        message.ReadFromBuffer(CompressedMap(3), serializer.Object);

        Assert.That(message.Yml, Is.EqualTo(expected));
        serializer.VerifyAll();
    }

    private static NetIncomingMessage CompressedMap(int claimedLength)
    {
        var data = new byte[] { 10, 20, 30 };
        var compressed = new byte[ZStd.CompressBound(data.Length)];
        var compressedLength = ZStd.Compress(compressed, data);
        var buffer = Incoming();
        buffer.WriteVariableInt32(claimedLength);
        buffer.WriteVariableInt32(compressedLength);
        buffer.Write(compressed, 0, compressedLength);
        return buffer;
    }

    private static TestCaseData Case(NetMessage message, bool fixedLength = false,
        Action<NetIncomingMessage>? prefix = null, string suffix = "")
    {
        var buffer = Incoming();
        prefix?.Invoke(buffer);
        if (fixedLength)
            buffer.Write(ClaimedLength);
        else
            buffer.WriteVariableInt32(ClaimedLength);
        return new TestCaseData(message, buffer)
            .SetName($"TruncatedLengthIsRejectedBeforeAllocation_{message.GetType().Name}{suffix}");
    }

    private static T RoundTrip<T>(T sent, T received) where T : NetMessage
    {
        var outgoing = (NetOutgoingMessage) Activator.CreateInstance(typeof(NetOutgoingMessage), nonPublic: true)!;
        sent.WriteToBuffer(outgoing, null!);
        var incoming = Incoming();
        incoming.Data = outgoing.Data;
        incoming.LengthBits = outgoing.LengthBits;
        received.ReadFromBuffer(incoming, null!);
        return received;
    }

    private static NetIncomingMessage Incoming()
    {
        // Lidgren constructors are internal; use its real buffer without opening a socket.
        return (NetIncomingMessage) Activator.CreateInstance(typeof(NetIncomingMessage), nonPublic: true)!;
    }
}
