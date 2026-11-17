using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

// a vehicle sitting on a mob knocks it down again every tick it overlaps the hull, pinning it. a Yautja, honourable
// or Bad Blood, keeps the hit's knockdown (applied at the collision, not here) but is not pinned; anyone else still is
[TestFixture]
public sealed class YautjaVehicleRunoverTest : GameTest
{
    private const string Hunter = "CMUMobYautja";
    private const string Marine = "CMMobHuman";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task YautjaAreNotPinnedUnderAVehicle()
    {
        var map = await Pair.CreateTestMap();
        var spawned = new List<EntityUid>();
        EntityUid hunter = default, badBlood = default, marine = default;

        try
        {
            await Server.WaitPost(() =>
            {
                var entMan = Server.EntMan;
                // anything with a body overlapping the mob stands in for the hull: the pin only checks the overlap
                var hull = entMan.SpawnEntity(Marine, map.GridCoords);
                hunter = entMan.SpawnEntity(Hunter, map.GridCoords);
                badBlood = entMan.SpawnEntity(BadBloodTestHelpers.GruntPrototype, map.GridCoords);
                marine = entMan.SpawnEntity(Marine, map.GridCoords);
                spawned.AddRange(new[] { hull, hunter, badBlood, marine });

                Assert.Multiple(() =>
                {
                    Assert.That(CMUVehicleRunover.IgnoresPin(entMan, hunter), Is.True);
                    Assert.That(CMUVehicleRunover.IgnoresPin(entMan, badBlood), Is.True);
                    Assert.That(CMUVehicleRunover.IgnoresPin(entMan, marine), Is.False);
                });

                foreach (var mob in new[] { hunter, badBlood, marine })
                {
                    var runover = entMan.EnsureComponent<VehicleRunoverComponent>(mob);
                    runover.Vehicle = hull;
                    runover.Duration = TimeSpan.FromSeconds(1.5);
                    runover.ExpiresAt = Server.Timing.CurTime + TimeSpan.FromSeconds(10);
                }
            });

            await Pair.RunTicksSync(5);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                Assert.Multiple(() =>
                {
                    foreach (var (mob, name) in new[] { (hunter, "a hunter"), (badBlood, "a Bad Blood") })
                    {
                        Assert.That(entMan.HasComponent<KnockedDownComponent>(mob), Is.False,
                            $"{name} under the hull is not knocked down again");
                        Assert.That(entMan.HasComponent<VehicleRunoverComponent>(mob), Is.True,
                            $"{name} still passes through the hull so they can walk out");
                    }

                    Assert.That(entMan.HasComponent<VehicleRunoverComponent>(marine), Is.True,
                        "a marine under the hull is still pinned");
                    Assert.That(entMan.HasComponent<KnockedDownComponent>(marine), Is.True);
                });
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                foreach (var uid in spawned)
                {
                    if (Server.EntMan.EntityExists(uid))
                        Server.EntMan.DeleteEntity(uid);
                }
            });
        }
    }
}
