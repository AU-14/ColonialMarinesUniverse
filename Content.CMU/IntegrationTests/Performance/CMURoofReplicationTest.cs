using System.IO;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using NetSerializer;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Performance;

[TestFixture]
public sealed class CMURoofReplicationTest : GameTest
{
    [Test]
    public async Task RoofChangesKeepOriginalWireBytesAndReachClient()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var roof = SEntMan.EnsureComponent<RoofComponent>(map.Grid);
            var roofs = Server.System<SharedRoofSystem>();
            roofs.SetRoof((map.Grid, null, roof), new Vector2i(1, 1), true);
            roofs.SetRoof((map.Grid, null, roof), new Vector2i(-1, -1), true);
            AssertOriginalWire(roof);
        });
        await Pair.RunTicksSync(10);
        var clientGrid = Pair.ToClientUid(map.Grid);
        await Client.WaitAssertion(() => Assert.That(CComp<RoofComponent>(clientGrid).Data,
            Is.EqualTo(new Dictionary<Vector2i, ulong>
            {
                [Vector2i.Zero] = 1UL << 9,
                [new Vector2i(-1, -1)] = 1UL << 63
            })));

        await Server.WaitAssertion(() =>
        {
            var roof = SComp<RoofComponent>(map.Grid);
            var roofs = Server.System<SharedRoofSystem>();
            roofs.SetRoof((map.Grid, null, roof), new Vector2i(1, 1), false);
            roofs.SetRoof((map.Grid, null, roof), new Vector2i(2, 1), true);
            AssertOriginalWire(roof);
        });
        await Pair.RunTicksSync(10);
        await Client.WaitAssertion(() => Assert.That(CComp<RoofComponent>(clientGrid).Data,
            Is.EqualTo(new Dictionary<Vector2i, ulong>
            {
                [Vector2i.Zero] = 1UL << 10,
                [new Vector2i(-1, -1)] = 1UL << 63
            })));
    }

    private void AssertOriginalWire(RoofComponent roof)
    {
        var serializer = Server.ResolveDependency<IRobustSerializer>();
        var serializerType = typeof(IRobustSerializer).Assembly.GetType("Robust.Shared.Serialization.RobustSerializer", true)!;
        var current = (Serializer) serializerType.GetField("_serializer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(serializer)!;
        var settings = (Settings) typeof(Serializer).GetField("Settings", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(current)!;
        // Keep the game's mapped strings/math serializers, but use the installed package's original dictionary writer.
        var original = new Serializer(current.GetTypeMap(), new Settings
        {
            CustomTypeSerializers = settings.CustomTypeSerializers
                .Where(s => !s.Handles(typeof(Dictionary<Vector2i, ulong>))).ToArray()
        });
        var state = SEntMan.GetComponentState(SEntMan.EventBus, roof, ServerSession, GameTick.Zero)!;
        using var oldBytes = new MemoryStream();
        using var newBytes = new MemoryStream();
        original.Serialize(oldBytes, state);
        serializer.Serialize(newBytes, state);
        Assert.That(newBytes.ToArray(), Is.EqualTo(oldBytes.ToArray()));
        newBytes.Position = 0;
        var decoded = original.Deserialize(newBytes);
        using var roundtrip = new MemoryStream();
        original.Serialize(roundtrip, decoded);
        Assert.That(roundtrip.ToArray(), Is.EqualTo(oldBytes.ToArray()));
    }
}
