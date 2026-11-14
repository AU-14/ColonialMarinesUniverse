using System;
using System.Collections.Generic;
using System.IO;
using NetSerializer;
using NUnit.Framework;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Tests.CMU14.Performance;

[TestFixture]
public sealed class CMURoofSerializationTest
{
    private static ITypeSerializer CreateSerializer()
    {
        var type = typeof(IRobustSerializer).Assembly.GetType(
            "Robust.Shared.Serialization.NetVector2iUlongDictionarySerializer", true)!;
        return (ITypeSerializer) Activator.CreateInstance(type)!;
    }

    [Serializable]
    private sealed class RoofState
    {
        public Dictionary<Vector2i, ulong>? Roof;
        public Dictionary<Vector2i, ulong>? SecondRoof;
    }

    [Test]
    public void RoofWritesMatchUnmodifiedSerializerAndDecodeInBothDirections()
    {
        var original = new Serializer([typeof(RoofState)]);
        var optimized = new Serializer([typeof(RoofState)], new Settings
        {
            CustomTypeSerializers = [CreateSerializer()]
        });
        Assert.That(optimized.GetSHA256(), Is.EqualTo(original.GetSHA256()), "Type IDs must remain unchanged.");

        var populated = new Dictionary<Vector2i, ulong>
        {
            [new Vector2i(int.MinValue, int.MaxValue)] = ulong.MaxValue,
            [new Vector2i(-1, -8)] = 1UL << 63,
            [Vector2i.Zero] = 0
        };
        for (var i = 1; i <= 512; i++)
            populated.Add(new Vector2i(i, -i), (ulong) i);
        populated.Remove(new Vector2i(1, -1));
        populated.Add(new Vector2i(1024, -1024), 17);

        foreach (var roof in new Dictionary<Vector2i, ulong>?[] { null, new(), populated })
        {
            var state = new RoofState { Roof = roof, SecondRoof = populated };
            using var oldBytes = new MemoryStream();
            using var newBytes = new MemoryStream();
            original.Serialize(oldBytes, state);
            optimized.Serialize(newBytes, state);
            Assert.That(newBytes.ToArray(), Is.EqualTo(oldBytes.ToArray()));

            newBytes.Position = 0;
            var oldReader = (RoofState) original.Deserialize(newBytes);
            Assert.That(oldReader.Roof, Is.EqualTo(roof));
            Assert.That(oldReader.SecondRoof, Is.EqualTo(populated));
            oldBytes.Position = 0;
            var newReader = (RoofState) optimized.Deserialize(oldBytes);
            Assert.That(newReader.Roof, Is.EqualTo(roof));
            Assert.That(newReader.SecondRoof, Is.EqualTo(populated));
        }
    }

    [Test]
    public void ReaderStillRejectsDuplicateKeysAndWrongArrayTypes()
    {
        var serializer = new Serializer([typeof(Dictionary<Vector2i, ulong>), typeof(int[])], new Settings
        {
            CustomTypeSerializers = [CreateSerializer()]
        });
        foreach (var invalid in new object[]
                 {
                     new[] { new KeyValuePair<Vector2i, ulong>(Vector2i.Zero, 1), new(Vector2i.Zero, 2) },
                     new[] { 1, 2 }
                 })
        {
            using var stream = new MemoryStream();
            serializer.Serialize(stream, invalid);
            stream.Position = 0;
            Assert.Throws<InvalidDataException>(() => serializer.DeserializeDirect<Dictionary<Vector2i, ulong>>(stream, out _));
        }
    }
}
