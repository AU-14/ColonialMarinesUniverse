using Content.IntegrationTests.Fixtures;
using Content.Server.Fax;
using Content.Server.Prayer;
using Content.Shared.Fax.Components;
using Content.Shared.Storage;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class ServerLogRegressionTest : GameTest
{
    [Test]
    public async Task SubtleMessageWithDetachedTargetSessionDoesNotThrow()
    {
        await Server.WaitAssertion(() =>
            Assert.DoesNotThrow(() => SEntMan.System<PrayerSystem>()
                .SendSubtleMessage(null!, "Regression test", "Message", "Popup")));
    }

    [Test]
    public async Task FaxRejectsDestinationRemovedByRefresh()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var fax = SEntMan.SpawnEntity("FaxMachineBase", map.GridCoords);
            var component = SEntMan.GetComponent<FaxMachineComponent>(fax);
            var system = SEntMan.System<FaxSystem>();
            component.KnownFaxes["38A8-ABCF"] = "Old destination";
            system.SetDestination(fax, "38A8-ABCF", component);
            system.Refresh(fax, component);

            Assert.DoesNotThrow(() => system.SetDestination(fax, "38A8-ABCF", component));
            Assert.That(component.DestinationFaxAddress, Is.Null);
            SEntMan.DeleteEntity(fax);
        });
    }

    [TestCase("AU14GunCasePistolL45", "RMCAttachmentRailFlashlight")]
    [TestCase("RMCGunCaseRifleM54CE2", "RMCWeaponRifleM54CE2")]
    public async Task GunCasesRetainTheirAdvertisedContents(string prototype, string itemPrototype)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var gunCase = SEntMan.SpawnEntity(prototype, map.GridCoords);
            var storage = SEntMan.GetComponent<StorageComponent>(gunCase);
            Assert.That(storage.Container.ContainedEntities.Any(item =>
                SEntMan.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID == itemPrototype), Is.True);
            if (prototype == "RMCGunCaseRifleM54CE2")
            {
                Assert.That(storage.Container.ContainedEntities.Select(item =>
                    SEntMan.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID),
                    Is.EquivalentTo(new[] { "RMCWeaponRifleM54CE2", "CMMagazineRifleM54CE2", "CMMagazineRifleM54CE2HT" }));
            }
            SEntMan.DeleteEntity(gunCase);
        });
    }
}
