using Content.Server.CMU14.Roles;
using Content.Shared.Access.Components;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Paper;
using Content.Shared.Preferences;

namespace Content.IntegrationTests._CMU14.Roles;

[TestFixture]
public sealed class CMUPersonalTodoTest
{
    [Test]
    public async Task HumanJobSpawnReceivesAccurateRankPinTodoListAndCashGrant()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var session = server.PlayerMan.Sessions.Single();
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human");
            var human = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            entities.System<MetaDataSystem>().SetEntityName(human, "Casey Test");
            var idCard = entities.SpawnEntity("CMIDCardIntelOfficer", map.GridCoords);
            Assert.That(entities.System<InventorySystem>().TryEquip(human, idCard, "id", silent: true, force: true),
                Is.True);
            var pin = entities.GetComponent<IdCardComponent>(idCard).AtmPin;

            var spawned = new PlayerSpawnCompleteEvent(
                human,
                session,
                "AU14JobGOVFOROfficerIntel",
                lateJoin: false,
                silent: true,
                joinOrder: 1,
                station: map.MapUid,
                profile: profile);

            entities.EventBus.RaiseLocalEvent(human, spawned, broadcast: true);

            Assert.That(entities.HasComponent<CMUPersonalTodoIssuedComponent>(human), Is.True);
            AssertIssuedItems(entities, human, 1, 1);

            var paper = FindInventoryPrototypes(entities, human, "CMUPaperGrey").Single();
            var content = entities.GetComponent<PaperComponent>(paper).Content;
            var checkboxCount = content.Split("[check]").Length - 1;

            Assert.Multiple(() =>
            {
                Assert.That(content, Does.Contain("Casey Test"));
                Assert.That(content, Does.Contain("(O1) Second Lieutenant"));
                Assert.That(content, Does.Contain($"Account PIN: {pin:D4}"));
                Assert.That(content, Does.Contain("Uniform of the Day:"));
                Assert.That(content, Does.Contain("Deposit money at an ATM."));
                Assert.That(checkboxCount, Is.InRange(5, 7));
            });

            entities.EventBus.RaiseLocalEvent(human, spawned, broadcast: true);
            AssertIssuedItems(entities, human, 1, 1);
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertIssuedItems(
        IEntityManager entities,
        EntityUid human,
        int expectedPapers,
        int expectedCash)
    {
        Assert.Multiple(() =>
        {
            Assert.That(FindInventoryPrototypes(entities, human, "CMUPaperGrey"),
                Has.Count.EqualTo(expectedPapers));
            Assert.That(FindInventoryPrototypes(entities, human, "RMCSpaceCash100"),
                Has.Count.EqualTo(expectedCash));
        });
    }

    private static List<EntityUid> FindInventoryPrototypes(
        IEntityManager entities,
        EntityUid human,
        string prototype)
    {
        var inventory = entities.System<InventorySystem>();
        return inventory.GetHandOrInventoryEntities(human)
            .Where(item => entities.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID == prototype)
            .ToList();
    }
}
