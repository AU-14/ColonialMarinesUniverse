using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Robust.Shared;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class CMUMohawkHijackPvsTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task OccupantReentersPvsAfterDeckRetirement(bool moveOnReturn)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            Destructive = true,
        });
        EntityUid ship = default;
        EntityUid occupant = default;
        EntityUid viewer = default;
        EntityUid groundMap = default;
        EntityUid[] decks = [];
        EntityUid[] holdingMaps = [];
        NetEntity occupantNet = default;
        NetEntity deckNet = default;
        NetEntity groundNet = default;
        var position = new Vector2(-2.5f, 1.5f);
        await pair.Server.WaitAssertion(() =>
        {
            pair.Server.CfgMan.SetCVar(CVars.NetPVS, true);
            var entities = pair.Server.EntMan;
            entities.System<SharedMapSystem>().CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath("/Maps/CMU14/Dropships/Mohawk/midway.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
            var assembly = entities.GetComponent<MultiDeckDropshipComponent>(ship);
            decks = assembly.Decks.Values.ToArray();
            var deck = assembly.Decks[-1];
            groundMap = entities.GetComponent<TransformComponent>(deck).MapUid!.Value;
            groundNet = entities.GetNetEntity(groundMap);
            occupant = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(deck, position));
            viewer = entities.SpawnEntity(null, new EntityCoordinates(deck, position));
            entities.EnsureComponent<EyeComponent>(viewer);
            pair.Server.PlayerMan.SetAttachedEntity(pair.Server.PlayerMan.Sessions.Single(), viewer);
            occupantNet = entities.GetNetEntity(occupant);
            deckNet = entities.GetNetEntity(deck);
        });
        await pair.RunTicksSync(30);
        await pair.Client.WaitAssertion(() =>
        {
            Assert.That(pair.Client.CfgMan.GetCVar(CVars.NetPVSEntityExitBudget),
                Is.EqualTo(CVars.NetPVSEntityExitBudget.DefaultValue));
            var entities = pair.Client.EntMan;
            var uid = entities.GetEntity(occupantNet);
            Assert.That(entities.GetComponent<TransformComponent>(uid).ParentUid,
                Is.EqualTo(entities.GetEntity(deckNet)));
            Assert.That(entities.GetComponent<FixturesComponent>(uid).Fixtures.Values.Sum(f => f.ProxyCount),
                Is.GreaterThan(0));
        });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<SharedTransformSystem>().SetCoordinates(viewer,
                new EntityCoordinates(groundMap, new Vector2(1000, 1000)));
            entities.System<MultiDeckDropshipSystem>().RemoveSecondaryDecks(ship);
            Assert.That(entities.GetComponent<TransformComponent>(occupant).ParentUid, Is.EqualTo(groundMap));
            holdingMaps = decks.Select(deck => entities.GetComponent<TransformComponent>(deck).MapUid!.Value).ToArray();
        });
        await pair.RunTicksSync(10);
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var uid = entities.GetEntity(occupantNet);
            Assert.That(entities.GetComponent<TransformComponent>(uid).ParentUid.IsValid(), Is.False);
            Assert.That(entities.GetComponent<FixturesComponent>(uid).Fixtures.Values.Sum(f => f.ProxyCount),
                Is.Zero, "PVS departure must release the occupant's old deck proxies before re-entry.");
        });
        var returnedPosition = moveOnReturn ? position + new Vector2(0.25f, 0) : position;
        await pair.Server.WaitPost(() =>
        {
            var transform = pair.Server.EntMan.System<SharedTransformSystem>();
            transform.SetCoordinates(viewer, new EntityCoordinates(groundMap, position));
            if (moveOnReturn)
                transform.SetCoordinates(occupant, new EntityCoordinates(groundMap, returnedPosition));
        });
        await pair.RunTicksSync(10);
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var uid = entities.GetEntity(occupantNet);
            var xform = entities.GetComponent<TransformComponent>(uid);
            Assert.That(xform.ParentUid, Is.EqualTo(entities.GetEntity(groundNet)));
            Assert.That(xform.LocalPosition, Is.EqualTo(returnedPosition));
            Assert.That(entities.GetComponent<MetaDataComponent>(uid).EntityPaused, Is.False);
            var tree = entities.GetComponent<BroadphaseComponent>(entities.GetEntity(groundNet)).DynamicTree;
            var fixtures = entities.GetComponent<FixturesComponent>(uid);
            Assert.That(fixtures.Fixtures.Values.Sum(f => f.ProxyCount), Is.GreaterThan(0));
            foreach (var proxy in fixtures.Fixtures.Values.SelectMany(f => f.Proxies))
                Assert.That(tree.GetProxy(proxy.ProxyId), Is.SameAs(proxy),
                    "Each occupant proxy must belong to the destination map's tree.");
        });
        if (!moveOnReturn)
        {
            await pair.Server.WaitAssertion(() =>
            {
                var entities = pair.Server.EntMan;
                entities.DeleteEntity(ship);
                foreach (var deck in decks)
                    Assert.That(entities.Deleted(deck), Is.False,
                        "Retired decks must outlive the cabin so late client states still have a valid broadphase.");
                // GameTicker uses this same entity flush when resetting a round.
                entities.FlushEntities();
                foreach (var uid in holdingMaps.Concat(decks))
                    Assert.That(entities.Deleted(uid), Is.True);
            });
        }
        await pair.CleanReturnAsync();
    }
}
