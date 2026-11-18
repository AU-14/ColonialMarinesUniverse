using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Marines.Access;
using Content.Shared.Access.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Roles;

[TestFixture]
public sealed class CMUColonyReplacementIdTest : GameTest
{
    [Test]
    public async Task DeputyCanIssueNamedReplacementWithColonyJobAccess()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var deputy = Server.System<StationSpawningSystem>().SpawnPlayerMob(map.GridCoords,
                new ProtoId<JobPrototype>("AU14JobCivilianColonyAdminAssistant"), new HumanoidCharacterProfile(), station: null);
            var inventory = Server.System<InventorySystem>();
            Assert.That(inventory.TryGetSlotEntity(deputy, "back", out var bag), Is.True);
            var supplies = SEntMan.GetComponent<StorageComponent>(bag!.Value).Container.ContainedEntities;
            string? Id(EntityUid item) => SEntMan.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID;
            Assert.That(supplies.Any(item => Id(item) == "CMUColonyIDConsoleFlatpack"), Is.True);
            var box = supplies.Single(item => Id(item) == "CMUReplacementColonyIDs");
            var replacement = SEntMan.GetComponent<StorageComponent>(box).Container.ContainedEntities.First();
            var employee = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(replacement, new UseInHandEvent(employee));
            var id = SEntMan.GetComponent<IdCardComponent>(replacement);
            Assert.That(id.FullName, Is.EqualTo(SEntMan.GetComponent<MetaDataComponent>(employee).EntityName));
            Assert.That(id.OriginalOwner, Is.EqualTo(employee));

            var terminal = SEntMan.SpawnEntity("CMUIDComputerColony", map.GridCoords);
            var hands = Server.System<SharedHandsSystem>();
            hands.TryDrop(deputy);
            Assert.That(inventory.TryGetSlotEntity(deputy, "id", out var adminId), Is.True);
            Assert.That(inventory.TryUnequip(deputy, "id", force: true), Is.True);
            Assert.That(hands.TryPickup(deputy, adminId!.Value), Is.True);
            void Send<T>(T msg) where T : BoundUserInterfaceMessage
            {
                msg.Actor = deputy;
                msg.UiKey = IdModificationConsoleUIKey.Key;
                SEntMan.EventBus.RaiseLocalEvent(terminal, msg);
            }
            Send(new IdModificationConsoleSignInBuiMsg());
            Assert.That(SEntMan.GetComponent<IdModificationConsoleComponent>(terminal).Authenticated, Is.True);
            Assert.That(hands.TryPickup(deputy, replacement), Is.True);
            Send(new IdModificationConsoleSignInTargetBuiMsg());
            Send(new IdModificationConsoleJobChangeBuiMsg("CMUIdJobColonyEngi"));
            Assert.That(SEntMan.GetComponent<AccessComponent>(replacement).Tags.Any(tag => tag.Id == "CMAccessColonyEngineering"), Is.True);
        });
    }
}
