using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NetSerializer;
using NUnit.Framework;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Tests.CMU14.Performance;

[TestFixture]
public sealed class CMUSerializerStatisticsTest
{
    private static IRobustSerializer CreateSerializer()
    {
        var type = Assembly.Load("Robust.Server").GetType("Robust.Server.Serialization.ServerRobustSerializer", true)!;
        var serializer = (IRobustSerializer) Activator.CreateInstance(type, true)!;
        // Isolate statistics from game startup traffic while exercising the real wrapper and codec.
        type.BaseType!.GetField("_serializer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(serializer, new Serializer([typeof(byte[]), typeof(string), typeof(Vector2i)]));
        return serializer;
    }

    private static object? Statistic(IRobustSerializer serializer, string name) =>
        serializer.GetType().GetProperty(name)!.GetValue(serializer);

    [Test]
    public void ConcurrentSerializationPreservesBytesCountersAndLargestType()
    {
        var serializer = CreateSerializer();
        var original = new Serializer([typeof(byte[]), typeof(string), typeof(Vector2i)]);
        object[] values = [new Vector2i(-81, 12345), new string('x', 64), new byte[512]];
        var expected = values.Select(value =>
        {
            using var stream = new MemoryStream();
            original.Serialize(stream, value);
            return stream.ToArray();
        }).ToArray();
        const int workers = 8;
        const int iterations = 384;
        Parallel.For(0, workers, _ =>
        {
            using var stream = new MemoryStream();
            for (var i = 0; i < iterations; i++)
            {
                // A prefix catches accidentally counting the absolute stream position.
                stream.SetLength(17);
                stream.Position = 17;
                var index = i % values.Length;
                serializer.Serialize(stream, values[index]);
                Assert.That(stream.GetBuffer().AsSpan(17, (int) stream.Length - 17)
                    .SequenceEqual(expected[index]), Is.True);
                stream.Position = 17;
                Assert.That(serializer.Deserialize(stream), Is.EqualTo(values[index]));
            }
        });
        var bytes = expected.Sum(value => (long) value.Length) * workers * iterations / values.Length;
        Assert.Multiple(() =>
        {
            Assert.That(Statistic(serializer, "ObjectsSerialized"), Is.EqualTo(workers * iterations));
            Assert.That(Statistic(serializer, "ObjectsDeserialized"), Is.EqualTo(workers * iterations));
            Assert.That(Statistic(serializer, "BytesSerialized"), Is.EqualTo(bytes));
            Assert.That(Statistic(serializer, "BytesDeserialized"), Is.EqualTo(bytes));
            Assert.That(Statistic(serializer, "LargestObjectSerializedBytes"), Is.EqualTo(expected[2].Length));
            Assert.That(Statistic(serializer, "LargestObjectDeserializedBytes"), Is.EqualTo(expected[2].Length));
            Assert.That(Statistic(serializer, "LargestObjectSerializedType"), Is.EqualTo(typeof(byte[])));
            Assert.That(Statistic(serializer, "LargestObjectDeserializedType"), Is.EqualTo(typeof(byte[])));
        });
    }

    [Test]
    public void UnseekableStreamsCountSuccessfulObjectsWithoutReadingPosition()
    {
        var serializer = CreateSerializer();
        using var stream = new UnseekableStream();
        serializer.SerializeDirect(stream, new byte[] { 1, 2, 3 });
        stream.Rewind();
        serializer.DeserializeDirect<byte[]>(stream, out var decoded);
        Assert.That(decoded, Is.EqualTo(new byte[] { 1, 2, 3 }));
        Assert.Multiple(() =>
        {
            Assert.That(Statistic(serializer, "ObjectsSerialized"), Is.EqualTo(1));
            Assert.That(Statistic(serializer, "ObjectsDeserialized"), Is.EqualTo(1));
            Assert.That(Statistic(serializer, "BytesSerialized"), Is.Zero);
            Assert.That(Statistic(serializer, "BytesDeserialized"), Is.Zero);
            Assert.That(Statistic(serializer, "LargestObjectSerializedBytes"), Is.Zero);
            Assert.That(Statistic(serializer, "LargestObjectDeserializedBytes"), Is.Zero);
        });
        stream.SetLength(0);
        stream.Rewind();
        Assert.Throws<EndOfStreamException>(() => serializer.DeserializeDirect<byte[]>(stream, out _));
        Assert.That(Statistic(serializer, "ObjectsDeserialized"), Is.EqualTo(1), "Failed reads must not count as completed objects.");
    }

    private sealed class UnseekableStream : MemoryStream
    {
        public override bool CanSeek => false;
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public void Rewind() => base.Position = 0;
    }
}
