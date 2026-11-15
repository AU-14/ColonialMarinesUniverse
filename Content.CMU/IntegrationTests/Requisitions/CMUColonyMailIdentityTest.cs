using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU14.Threats.Mobs.CLF;
using Content.Shared.Paper;
using Content.Shared.Roles;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.CMU.Requisitions;

public sealed class CMUColonyMailIdentityTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task MailAndReceiptsUseTheInsurgentsPublicOccupation()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var recipient = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            SEntMan.AddComponent<CLFMemberComponent>(recipient);
            var minds = Server.System<SharedMindSystem>();
            var mind = minds.CreateMind(null);
            minds.TransferTo(mind, recipient);
            Server.System<SharedRoleSystem>().MindAddJobRole(mind, jobPrototype: "AU14JobCLFSapper");
            Server.PlayerMan.SetAttachedEntity(ServerSession!, recipient);
            var lift = SEntMan.SpawnEntity(null, map.GridCoords);
            var elevator = SEntMan.AddComponent<RequisitionsElevatorComponent>(lift);
            var crate = SSpawnAtPosition("CMUColonyMailCrateRoundStart", map.GridCoords);
            var delivered = new CMURequisitionsCrateDeliveredEvent((lift, elevator), 0, crate, "mail");
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref delivered);
            var contents = Server.System<SharedContainerSystem>().GetContainer(crate, "entity_storage").ContainedEntities;
            var receipt = contents.Single(uid => SEntMan.HasComponent<PaperComponent>(uid));
            var parcel = contents.Single(uid => uid != receipt);
            Assert.That(SComp<MetaDataComponent>(parcel).EntityDescription, Does.Contain("Colonist"));
            Assert.That(SComp<PaperComponent>(receipt).Content, Does.Contain("Colonist"));
            Assert.That(SComp<MetaDataComponent>(parcel).EntityDescription, Does.Not.Contain("Sapper"));
            Assert.That(SComp<PaperComponent>(receipt).Content, Does.Not.Contain("Sapper"));
        });
    }
}
