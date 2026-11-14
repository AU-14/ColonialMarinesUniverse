#pragma warning disable RA0002
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Camera;
using Content.Shared.Camera;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// COUPLING-201: a pad watches the hounds on its own camera network and no others. The clan's pad lists the clan's
// hounds only, and the hunters' pad never lists one of them
[TestFixture]
public sealed class BadBloodHoundPadScopeTest : GameTest
{
    private const string HunterHound = "CMUMobYautjaHellhound";
    private const string ClanHound = "CMUMobYautjaBadBloodHound";
    private const string HunterPad = "CMUYautjaHoundObservationPad";
    private const string ClanPad = "CMUBadBloodHoundObservationPad";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task EachPadListsOnlyItsOwnHounds()
    {
        var map = await Pair.CreateTestMap();
        var spawned = new List<EntityUid>();

        try
        {
            EntityUid hunterHound = default;
            EntityUid clanHound = default;
            EntityUid hunterPad = default;
            EntityUid clanPad = default;

            await Server.WaitPost(() =>
            {
                var entMan = Server.EntMan;
                hunterHound = entMan.SpawnEntity(HunterHound, map.GridCoords);
                clanHound = entMan.SpawnEntity(ClanHound, map.GridCoords);
                hunterPad = entMan.SpawnEntity(HunterPad, map.GridCoords);
                clanPad = entMan.SpawnEntity(ClanPad, map.GridCoords);
                spawned.AddRange(new[] { hunterHound, clanHound, hunterPad, clanPad });
            });

            await Pair.RunSeconds(1);

            await Server.WaitAssertion(() =>
            {
                var cameras = Server.System<CameraNetworkSystem>();
                var hunterFeeds = Feeds(cameras, hunterPad);
                var clanFeeds = Feeds(cameras, clanPad);

                Assert.Multiple(() =>
                {
                    Assert.That(clanFeeds, Does.Contain(clanHound), "the clan's pad lists the clan's hound");
                    Assert.That(clanFeeds.All(feed => Server.EntMan.HasComponent<BadBloodHoundComponent>(feed)), Is.True,
                        "the clan's pad lists nothing but clan hounds");
                    Assert.That(hunterFeeds, Does.Contain(hunterHound), "the hunters' pad lists the hunters' hellhound");
                    Assert.That(hunterFeeds.Any(feed => Server.EntMan.HasComponent<BadBloodHoundComponent>(feed)), Is.False,
                        "the hunters' pad never lists a clan hound");
                });

                Assert.Multiple(() =>
                {
                    Assert.That(cameras.CanAccess(Camera(clanPad), clanHound), Is.True);
                    Assert.That(cameras.CanAccess(Camera(clanPad), hunterHound), Is.False);
                    Assert.That(cameras.CanAccess(Camera(hunterPad), hunterHound), Is.True);
                    Assert.That(cameras.CanAccess(Camera(hunterPad), clanHound), Is.False);
                });
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                foreach (var uid in spawned)
                {
                    if (Server.EntMan.EntityExists(uid))
                        Server.EntMan.DeleteEntity(uid);
                }
            });
        }
    }

    // the pad's own camera console, spawned inside it on map init
    private EntityUid Camera(EntityUid pad)
    {
        var camera = Server.EntMan.GetComponent<YautjaHoundPadComponent>(pad).InternalCamera;
        Assert.That(camera, Is.Not.Null, "the pad has its camera console");
        return camera!.Value;
    }

    private List<EntityUid> Feeds(CameraNetworkSystem cameras, EntityUid pad)
    {
        var camera = Camera(pad);
        var receiver = Server.EntMan.GetComponent<CameraNetworkReceiverComponent>(camera);
        return cameras.GetAccessibleCameras((camera, receiver)).ToList();
    }
}
