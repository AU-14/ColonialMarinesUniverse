#pragma warning disable RA0002 // Inspect real damage, gunfire and replacement transitions.
using Content.Server.CMU14.Fob;
using Content.Shared.CMU14.Fob;

namespace Content.IntegrationTests.CMU14.Fob;

public sealed partial class CMUFobDefenseTest
{
    [Test]
    public async Task InformationGunStatusAndTrialReportFollowShotsBreakageAndReplacement()
    {
        await SetupGuns(ammo: 3);
        await Server.WaitAssertion(() =>
        {
            Hurt(_gun, 20);
            Assert.That(Defense.GetGunStatus(_network)[0].Damage, Is.EqualTo(20));
            Assert.That(Reserve.TryActivate(_network), Is.True);
            Assert.That(Defense.GetGunStatus(_network)[0].Condition, Is.EqualTo(CMUFobGunCondition.Starting));
        });
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            var status = Defense.GetGunStatus(_network)[0];
            var trial = SEntMan.GetComponent<CMUFobTrialComponent>(_network);
            Assert.That(status.Condition, Is.EqualTo(CMUFobGunCondition.Empty));
            Assert.That(status.Ammunition, Is.Zero);
            Assert.That(trial.Shots, Is.EqualTo(3), "Count only ammunition actually fired.");
            Assert.That(trial.GunDamage, Is.EqualTo(20));
            Hurt(_gun, 500);
            Assert.That(Defense.GetGunStatus(_network)[0].Condition, Is.EqualTo(CMUFobGunCondition.Broken));
            Assert.That(trial.GunBreaks, Is.EqualTo(1));
            SEntMan.DeleteEntity(_gun);
            Assert.That(Defense.GetGunStatus(_network)[0].Condition, Is.EqualTo(CMUFobGunCondition.Missing));
        });
        await RunSeconds(0.2f);
        await Server.WaitAssertion(() =>
        {
            var replacement = Defense.GetGunStatus(_network)[0];
            Assert.That(replacement.Condition, Is.EqualTo(CMUFobGunCondition.Broken));
            Assert.That(replacement.Ammunition, Is.Zero);
            Assert.That(SEntMan.GetComponent<CMUFobTrialComponent>(_network).Shots, Is.EqualTo(3));
            Assert.That(SEntMan.GetComponent<CMUFobTrialComponent>(_network).GunBreaks, Is.EqualTo(1),
                "An empty replacement wreck is not another combat break.");
        });
    }
}
