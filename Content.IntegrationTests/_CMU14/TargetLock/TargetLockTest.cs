using Content.IntegrationTests.Tests.Interaction;
using Content.Server.CMU14.TargetLock;
using Content.Shared.CMU14.TargetLock;

namespace Content.IntegrationTests._CMU14.TargetLock;

public sealed class TargetLockTest : InteractionTest
{
    [Test]
    public async Task ScannerLocksOnForThreeSecondsAndReadsAnUnarmedPerson()
    {
        await SpawnTarget("CMUTargetLockVisor");
        var scanner = STarget!.Value;

        await Server.WaitAssertion(() =>
        {
            var comp = SEntMan.GetComponent<CMUTargetLockComponent>(scanner);
            Assert.That(comp.LockTime, Is.EqualTo(TimeSpan.FromSeconds(3)));

            var person = SEntMan.SpawnEntity("CMMobHuman", SEntMan.GetCoordinates(PlayerCoords));
            var report = SEntMan.System<CMUTargetLockSystem>().BuildReport((scanner, comp), person);

            Assert.That(report.Weapons, Is.Empty);
            Assert.That(report.Health, Is.EqualTo(CMUTargetLockHealth.Healthy));
            Assert.That(report.HealthPercent, Is.EqualTo(100));
            Assert.That(report.CriminalRecord, Is.Not.Empty);
            Assert.That(report.MedicalRecord, Is.Not.Empty);
        });
    }
}
