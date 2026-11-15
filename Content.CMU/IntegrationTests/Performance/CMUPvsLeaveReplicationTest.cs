using Content.IntegrationTests.Fixtures;
using Content.Shared.Eye;
using Robust.Server.GameObjects;
using Robust.Shared;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Performance;

[TestFixture]
public sealed class CMUPvsLeaveReplicationTest : GameTest
{
    [Test]
    public async Task EqualCountVisibilitySwapAndReentryReachUnmodifiedClient()
    {
        var map = await Pair.CreateTestMap();
        EntityUid first = default;
        EntityUid second = default;
        NetEntity secondNet = default;
        await Server.WaitPost(() =>
        {
            var viewer = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.EnsureComponent<EyeComponent>(viewer);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, viewer);
            Server.PlayerMan.SetStatus(ServerSession!, SessionStatus.InGame);
            Server.CfgMan.SetCVar(CVars.NetPVS, true);
            first = SEntMan.SpawnEntity(null, map.GridCoords);
            second = SEntMan.SpawnEntity(null, map.GridCoords);
            secondNet = SEntMan.GetNetEntity(second);
            Server.System<VisibilitySystem>().AddLayer(second, (int) VisibilityFlags.Ghost);
        });
        await Pair.RunTicksSync(10);
        var firstClient = Pair.ToClientUid(first);
        await Client.WaitAssertion(() =>
        {
            var metadata = CEntMan.GetComponent<MetaDataComponent>(firstClient);
            Assert.That(metadata.Flags & MetaDataFlags.Detached, Is.EqualTo(MetaDataFlags.None));
            Assert.That(CEntMan.TryGetEntity(secondNet, out _), Is.False);
        });

        await Server.WaitPost(() =>
        {
            var visibility = Server.System<VisibilitySystem>();
            visibility.AddLayer(first, (int) VisibilityFlags.Ghost);
            visibility.RemoveLayer(second, (int) VisibilityFlags.Ghost);
        });
        await Pair.RunTicksSync(10);
        var secondClient = Pair.ToClientUid(second);
        await Client.WaitAssertion(() =>
        {
            var firstMetadata = CEntMan.GetComponent<MetaDataComponent>(firstClient);
            var secondMetadata = CEntMan.GetComponent<MetaDataComponent>(secondClient);
            Assert.That(firstMetadata.Flags & MetaDataFlags.Detached, Is.EqualTo(MetaDataFlags.Detached));
            Assert.That(secondMetadata.Flags & MetaDataFlags.Detached, Is.EqualTo(MetaDataFlags.None));
        });

        await Server.WaitPost(() =>
        {
            Server.System<MetaDataSystem>().SetEntityName(first, "PVS reentry update");
            var visibility = Server.System<VisibilitySystem>();
            visibility.AddLayer(second, (int) VisibilityFlags.Ghost);
            visibility.RemoveLayer(first, (int) VisibilityFlags.Ghost);
        });
        await Pair.RunTicksSync(10);
        await Client.WaitAssertion(() =>
        {
            var metadata = CEntMan.GetComponent<MetaDataComponent>(firstClient);
            Assert.That(metadata.Flags & MetaDataFlags.Detached, Is.EqualTo(MetaDataFlags.None));
            Assert.That(metadata.EntityName, Is.EqualTo("PVS reentry update"));
            var secondMetadata = CEntMan.GetComponent<MetaDataComponent>(secondClient);
            Assert.That(secondMetadata.Flags & MetaDataFlags.Detached, Is.EqualTo(MetaDataFlags.Detached));
        });
    }
}
