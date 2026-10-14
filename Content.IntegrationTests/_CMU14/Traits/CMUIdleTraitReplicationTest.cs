using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Traits.Asthmatic;
using Content.Shared.CMU14.Traits.NicotineAddiction;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Traits;

[TestFixture]
public sealed class CMUIdleTraitReplicationTest : GameTest
{
    [Test]
    public async Task IdleFiveHundredEntitiesDoNotAdvanceNetworkVersions()
    {
        var entities = new List<EntityUid>();
        var versions = new List<(GameTick Nicotine, GameTick Strain)>();
        try
        {
            await Server.WaitPost(() =>
            {
                for (var i = 0; i < 500; i++)
                {
                    var uid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                    entities.Add(uid);
                    var nicotine = SEntMan.AddComponent<NicotineAddictionComponent>(uid);
                    var strain = SEntMan.AddComponent<RespiratoryStrainComponent>(uid);
                    versions.Add((nicotine.LastModifiedTick, strain.LastModifiedTick));
                }
            });
            await Pair.RunTicksSync(Pair.SecondsToTicks(3));
            await Server.WaitAssertion(() =>
            {
                var nicotineChanged = 0;
                var strainChanged = 0;
                for (var i = 0; i < entities.Count; i++)
                {
                    var nicotine = SEntMan.GetComponent<NicotineAddictionComponent>(entities[i]);
                    var strain = SEntMan.GetComponent<RespiratoryStrainComponent>(entities[i]);
                    nicotineChanged += nicotine.LastModifiedTick != versions[i].Nicotine ? 1 : 0;
                    strainChanged += strain.LastModifiedTick != versions[i].Strain ? 1 : 0;
                    Assert.That(nicotine.NextCheck, Is.GreaterThan(nicotine.TimeBetweenChecks));
                    Assert.That(strain.NextCheck, Is.GreaterThan(strain.TimeBetweenChecks));
                    Assert.That(nicotine.Craving, Is.False);
                    Assert.That(strain.Current, Is.Zero);
                    Assert.That(strain.Peaked, Is.False);
                }
                TestContext.Out.WriteLine($"CMU_IDLE_TRAITS population=500 nicotineVersionsChanged={nicotineChanged} strainVersionsChanged={strainChanged}");
                Assert.That(nicotineChanged, Is.Zero);
                Assert.That(strainChanged, Is.Zero);
            });
        }
        finally
        {
            await Server.WaitPost(() => entities.ForEach(uid => SEntMan.DeleteEntity(uid)));
        }
    }

    [Test]
    public async Task ActiveChangesStillAdvanceNetworkVersions()
    {
        EntityUid uid = default;
        GameTick created = default, cravingVersion = default;
        try
        {
            await Server.WaitPost(() =>
            {
                uid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                var nicotine = SEntMan.AddComponent<NicotineAddictionComponent>(uid);
                nicotine.CravingThreshold = TimeSpan.Zero;
                nicotine.NextCheck = SGameTiming.CurTime + TimeSpan.FromSeconds(1);
                var strain = SEntMan.AddComponent<RespiratoryStrainComponent>(uid);
                strain.Current = 2;
                strain.NextCheck = nicotine.NextCheck;
                created = SGameTiming.CurTick;
            });
            await Pair.RunTicksSync(Pair.SecondsToTicks(2));
            await Server.WaitAssertion(() =>
            {
                var nicotine = SEntMan.GetComponent<NicotineAddictionComponent>(uid);
                var strain = SEntMan.GetComponent<RespiratoryStrainComponent>(uid);
                Assert.That(nicotine.Craving, Is.True);
                Assert.That(nicotine.LastModifiedTick, Is.GreaterThan(created));
                Assert.That(strain.Current, Is.LessThan(2));
                Assert.That(strain.LastModifiedTick, Is.GreaterThan(created));
                cravingVersion = nicotine.LastModifiedTick;
                nicotine.NextCheck = SGameTiming.CurTime + TimeSpan.FromMinutes(1);
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                SEntMan.System<NicotineAddictionSystem>().Smoked(uid);
                var nicotine = SEntMan.GetComponent<NicotineAddictionComponent>(uid);
                Assert.That(nicotine.Craving, Is.False);
                Assert.That(nicotine.LastModifiedTick, Is.GreaterThan(cravingVersion));
            });
        }
        finally
        {
            await Server.WaitPost(() => SEntMan.DeleteEntity(uid));
        }
    }
}
