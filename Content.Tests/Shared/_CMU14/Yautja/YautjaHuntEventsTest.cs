using System;
using Content.Shared._CMU14.Yautja;
using NUnit.Framework;
using Robust.Shared.Serialization;

namespace Content.Tests.Shared._CMU14.Yautja;

[TestFixture]
public sealed class YautjaHuntEventsTest
{
    [Test]
    public void PreserveEscapeChoiceIsNetworkSerializable()
    {
        var eventType = typeof(YautjaPreserveEscapeChoiceEvent);

        Assert.Multiple(() =>
        {
            Assert.That(Attribute.IsDefined(eventType, typeof(SerializableAttribute)), Is.True);
            Assert.That(Attribute.IsDefined(eventType, typeof(NetSerializableAttribute)), Is.True);
        });
    }
}
