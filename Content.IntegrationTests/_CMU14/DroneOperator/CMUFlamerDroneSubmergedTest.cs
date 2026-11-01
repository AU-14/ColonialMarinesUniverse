using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.CMU14.DroneOperator;

// bug 685: the flamer drone kept lighting off while sitting in deep water
[TestFixture]
public sealed class CMUFlamerDroneSubmergedTest
{
    [Test]
    public async Task FlamerDroneWontFireFromDeepWater()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var submerged = server.ResolveDependency<ILocalizationManager>().GetString("cmu-flamer-fuel-submerged");

            var dry = entMan.SpawnEntity("CMUFlamerDrone", map.GridCoords.Offset(new(2, 0)));
            entMan.SpawnEntity("CMFloorDeepWaterEntity", map.GridCoords);
            var wet = entMan.SpawnEntity("CMUFlamerDrone", map.GridCoords);

            AttemptShootEvent Attempt(EntityUid drone)
            {
                var ev = new AttemptShootEvent(drone, null, entMan.GetComponent<TransformComponent>(drone).Coordinates, null);
                entMan.EventBus.RaiseLocalEvent(drone, ref ev);
                return ev;
            }

            var wetShot = Attempt(wet);
            var dryShot = Attempt(dry);
            Assert.Multiple(() =>
            {
                Assert.That(wetShot.Cancelled, Is.True);
                Assert.That(wetShot.Message, Is.EqualTo(submerged));
                Assert.That(dryShot.Message, Is.Not.EqualTo(submerged));
            });
        });

        await pair.CleanReturnAsync();
    }
}
