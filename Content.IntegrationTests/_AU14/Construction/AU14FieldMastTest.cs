using System.Linq;
using Content.Server.Construction;
using Content.Shared._RMC14.Construction;
using Content.Shared.CMU14.Construction;
using Content.Shared.CMU14.Radio;
using Content.Shared.Radio;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Construction;

// a field mast relays only the sides keyed into it, the keys have to survive the feed being opened
// and sealed again (the two are separate prototypes, so construction swaps the entity), and a footing
// laid somewhere a mast cannot stand is taken straight back down with the metal returned
[TestFixture]
public sealed class AU14FieldMastTest
{
    private static readonly EntProtoId Unkeyed = "AU14CommsMastField";
    private static readonly EntProtoId Govfor = "AU14CommsMastFieldGovfor";
    private static readonly EntProtoId Footing = "AU14CommsMastFooting";

    private static readonly ProtoId<RadioChannelPrototype> GovforNet = "radioGovforAlpha";
    private static readonly ProtoId<RadioChannelPrototype> OpforNet = "radioOpforAlpha";

    [Test]
    public async Task MastRelaysOnlyItsKeys()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var keyed = entities.SpawnEntity(Govfor, testMap.GridCoords);
            var blank = entities.SpawnEntity(Unkeyed, testMap.GridCoords);

            var keyedAnchor = entities.GetComponent<ANPRCRelayAnchorComponent>(keyed);

            Assert.Multiple(() =>
            {
                Assert.That(keyedAnchor.Channels, Does.Contain(GovforNet));
                Assert.That(keyedAnchor.Channels, Does.Not.Contain(OpforNet), "a GOVFOR key never carries OPFOR nets");
                Assert.That(entities.GetComponent<ANPRCRelayAnchorComponent>(blank).Channels, Is.Empty,
                    "an unkeyed mast relays nothing");
            });

            entities.DeleteEntity(keyed);
            entities.DeleteEntity(blank);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task KeysSurviveOpeningAndSealingTheFeed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid mast = default;

        await server.WaitAssertion(() =>
        {
            mast = server.EntMan.SpawnEntity(Govfor, testMap.GridCoords);
            Assert.That(server.System<ConstructionSystem>().ChangeNode(mast, null, "feedOpen"), Is.True);
        });

        await pair.RunTicksSync(5);

        EntityUid open = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            open = FindMast(entities);

            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<MetaDataComponent>(open).EntityPrototype?.ID,
                    Is.EqualTo("AU14CommsMastRigged"), "opening the feed swaps to the untuned mast");
                Assert.That(entities.GetComponent<AU14MastKeyComponent>(open).Keys, Does.Contain("govfor"),
                    "the key rides across the swap");
                Assert.That(entities.HasComponent<ANPRCRelayAnchorComponent>(open), Is.False,
                    "an open feed carries no traffic");
            });

            // the other side loads its card while the feed is open, then seals it
            entities.GetComponent<AU14MastKeyComponent>(open).Keys.Add("opfor");
            Assert.That(server.System<ConstructionSystem>().ChangeNode(open, null, "mast"), Is.True);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var sealedMast = FindMast(entities);
            var anchor = entities.GetComponent<ANPRCRelayAnchorComponent>(sealedMast);

            Assert.Multiple(() =>
            {
                Assert.That(anchor.Channels, Does.Contain(GovforNet), "the old key is still loaded");
                Assert.That(anchor.Channels, Does.Contain(OpforNet), "the added key is on the air after sealing");
            });

            entities.DeleteEntity(sealedMast);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadSiteReturnsTheFooting()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = server.System<SharedMapSystem>();
            var grid = testMap.Grid;

            // floor out a patch big enough for two sites, walls nowhere
            for (var x = -2; x <= 20; x++)
            {
                for (var y = -2; y <= 2; y++)
                {
                    maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), testMap.Tile.Tile);
                }
            }

            // a good site stays
            var good = entities.SpawnEntity(Footing, new EntityCoordinates(grid.Owner, 0.5f, 0.5f));
            RaiseBuilt(entities, good);
            Assert.That(entities.Deleted(good) || entities.IsQueuedForDeletion(good), Is.False,
                "a footing on open, clear ground is kept");

            // a second one inside the first's spacing is refused and the metal comes back
            var crowded = entities.SpawnEntity(Footing, new EntityCoordinates(grid.Owner, 8.5f, 0.5f));
            RaiseBuilt(entities, crowded);
            Assert.That(entities.IsQueuedForDeletion(crowded), Is.True, "too close to another mast");

            var refunded = entities.EntityQuery<StackComponent>()
                .Where(s => s.StackTypeId == "CMSteel")
                .Sum(s => s.Count);
            Assert.That(refunded, Is.EqualTo(40), "the full footing cost is returned");

            entities.DeleteEntity(good);
        });

        await pair.CleanReturnAsync();
    }

    private static void RaiseBuilt(IEntityManager entities, EntityUid built)
    {
        var ev = new RMCConstructionTransactionCompletedEvent(built, built, "CMSteel", 0);
        entities.EventBus.RaiseLocalEvent(built, ref ev, broadcast: true);
    }

    private static EntityUid FindMast(IEntityManager entities)
    {
        var query = entities.EntityQueryEnumerator<AU14MastKeyComponent>();
        EntityUid found = default;
        var count = 0;

        while (query.MoveNext(out var uid, out _))
        {
            if (entities.IsQueuedForDeletion(uid))
                continue;

            found = uid;
            count++;
        }

        Assert.That(count, Is.EqualTo(1), "exactly one live mast");
        return found;
    }
}
