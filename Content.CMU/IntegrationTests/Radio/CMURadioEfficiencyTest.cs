using System.Reflection;
using System.IO;
using Content.Client.CMU14.Radio;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Radio;
using Content.Shared.CMU14.Radio;
using Content.Shared.PowerCell;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.IntegrationTests.CMU14.Radio;

[TestFixture]
public sealed class CMURadioEfficiencyTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task RelayRetuningMovementAndRemovalImmediatelyChangeCoverage()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var range = Server.System<ANPRCRangeSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var receiver = SEntMan.SpawnEntity(null, map.GridCoords);
            var uid = SEntMan.SpawnEntity(null, map.GridCoords);
            var anchor = SEntMan.AddComponent<ANPRCRelayAnchorComponent>(uid);
            ProtoId<RadioChannelPrototype> first = "radioGovforAlpha";
            ProtoId<RadioChannelPrototype> second = "radioGovforCommand";
            anchor.Channels.Add(first);
            range.RefreshAnchorChannels((uid, anchor));
            Assert.That(range.GetRangeTier(receiver, first.Id, out _), Is.EqualTo(ANPRCRangeTier.Full));

            anchor.Channels.Clear();
            anchor.Channels.Add(second);
            range.RefreshAnchorChannels((uid, anchor));
            Assert.That(range.GetRangeTier(receiver, first.Id, out _), Is.EqualTo(ANPRCRangeTier.OutOfRange));
            Assert.That(range.GetRangeTier(receiver, second.Id, out _), Is.EqualTo(ANPRCRangeTier.Full));

            transform.SetCoordinates(uid, map.GridCoords.Offset(new System.Numerics.Vector2(100, 0)));
            Assert.That(range.GetRangeTier(receiver, second.Id, out _), Is.EqualTo(ANPRCRangeTier.OutOfRange));
            transform.SetCoordinates(uid, map.GridCoords);
            Assert.That(range.GetRangeTier(receiver, second.Id, out _), Is.EqualTo(ANPRCRangeTier.Full));
            SEntMan.RemoveComponent<ANPRCRelayAnchorComponent>(uid);
            Assert.That(range.GetRangeTier(receiver, second.Id, out _), Is.EqualTo(ANPRCRangeTier.OutOfRange));
        });
    }

}
