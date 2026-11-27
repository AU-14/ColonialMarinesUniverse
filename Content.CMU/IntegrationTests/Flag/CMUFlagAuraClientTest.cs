using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Orders;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.util;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Flag;

/// <summary>
///     Flag orders are applied by the server on every pulse. The client must keep showing them between pulses
///     instead of expiring them before the refresh arrives.
/// </summary>
[TestFixture]
public sealed class CMUFlagAuraClientTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    private PlatoonPrototype? _originalGovfor;
    private PlatoonPrototype? _originalOpfor;

    // A round with selected platoons overrides the bearer's side, which this test rallies.
    [SetUp]
    public async Task ClearPlatoons()
    {
        await Server.WaitPost(() =>
        {
            var platoons = Server.System<PlatoonSpawnRuleSystem>();
            _originalGovfor = platoons.SelectedGovforPlatoon;
            _originalOpfor = platoons.SelectedOpforPlatoon;
            platoons.SelectedGovforPlatoon = null;
            platoons.SelectedOpforPlatoon = null;
        });
    }

    [TearDown]
    public async Task RestorePlatoons()
    {
        await Server.WaitPost(() =>
        {
            var platoons = Server.System<PlatoonSpawnRuleSystem>();
            platoons.SelectedGovforPlatoon = _originalGovfor;
            platoons.SelectedOpforPlatoon = _originalOpfor;
        });
    }

    [Test]
    public async Task FlagBuffsStayOnTheClientBetweenPulses()
    {
        var map = await Pair.CreateTestMap();
        NetEntity bearerNet = default;
        NetEntity nearNet = default;

        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = -2; x <= 4; x++)
            for (var y = -2; y <= 2; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);

            var bearer = SpawnMarine(map, new Vector2(0, 0));
            var near = SpawnMarine(map, new Vector2(2, 0));
            Server.PlayerMan.SetAttachedEntity(ServerSession!, bearer);

            // The carried flag's orders would last exactly as long as its pulse interval.
            var flag = SEntMan.SpawnEntity("AU14FlagCarriableUA", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(bearer, flag), Is.True);

            bearerNet = SEntMan.GetNetEntity(bearer);
            nearNet = SEntMan.GetNetEntity(near);
        });

        await Pair.RunSeconds(1);
        await Pair.RunUntilSynced();

        // Several pulses, checked on every tick.
        for (var tick = 0; tick < 200; tick++)
        {
            await Pair.RunTicksSync(1);
            await Client.WaitAssertion(() =>
            {
                foreach (var net in new[] { bearerNet, nearNet })
                {
                    var marine = CEntMan.GetEntity(net);
                    Assert.That(CEntMan.HasComponent<FocusOrderComponent>(marine), Is.True,
                        $"{CEntMan.ToPrettyString(marine)} lost its focus order on the client on tick {tick}.");
                }
            });
        }
    }

    private EntityUid SpawnMarine(TestMapData map, Vector2 tile)
    {
        var marine = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(tile + new Vector2(0.5f, 0.5f)));
        SEntMan.EnsureComponent<MarineComponent>(marine);
        Server.System<GunIFFSystem>().SetUserFaction(marine, "GOVFOR");
        return marine;
    }
}
