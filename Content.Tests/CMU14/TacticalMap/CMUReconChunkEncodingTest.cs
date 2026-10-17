using Content.Shared.CMU14.TacticalMap.Reconstruction;
using NUnit.Framework;
using System.Linq;

namespace Content.Tests.CMU14.TacticalMap;

[TestFixture]
public sealed class CMUReconChunkEncodingTest
{
    [Test]
    public void MixedStructuresRoundTripWithSmallerPayload()
    {
        var cells = new byte[256];
        var styles = new uint[256];
        var directions = new byte[256];
        for (var i = 0; i < 256; i++)
        {
            cells[i] = (byte) (1 + i % 4);
            styles[i] = 0xf1230000u + (uint) (i % 8);
            directions[i] = (byte) (i % 4);
        }
        var raw = new CMUReconChunk(3, 7, 2, cells, styles, directions, Revision: 12);
        var packed = CMUReconChunkEncoding.Pack(raw);
        Assert.That(CMUReconChunkEncoding.EstimatedBytes(packed), Is.LessThan(400));
        Assert.That(CMUReconChunkEncoding.TryUnpack(packed, out var decoded), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(decoded.Cells, Is.EqualTo(cells));
            Assert.That(decoded.Appearance, Is.EqualTo(styles));
            Assert.That(decoded.Directions, Is.EqualTo(directions));
            Assert.That(decoded.Revision, Is.EqualTo(12));
            Assert.That((decoded.Level, decoded.X, decoded.Y), Is.EqualTo((3, 7, 2)));
        });
    }

    [Test]
    public void UniqueCellsKeepRawEncoding()
    {
        var raw = new CMUReconChunk(0, 0, 0, new byte[256], Enumerable.Range(0, 256).Select(i => (uint) i).ToArray(), new byte[256]);
        Assert.That(CMUReconChunkEncoding.Pack(raw), Is.EqualTo(raw));
    }

    [Test]
    public void RejectsTruncatedPaletteAndOutOfRangeIndices()
    {
        var packed = CMUReconChunkEncoding.Pack(new CMUReconChunk(0, 0, 0, new byte[256], new uint[256], new byte[256]));
        Assert.That(CMUReconChunkEncoding.TryUnpack(packed with { Packed = packed.Packed![..^1] }, out _), Is.False);
        packed.Packed![^1] = 1;
        Assert.That(CMUReconChunkEncoding.TryUnpack(packed, out _), Is.False);
        Assert.That(CMUReconChunkEncoding.TryUnpack(packed with { Packed = [] }, out _), Is.False);
    }
}
