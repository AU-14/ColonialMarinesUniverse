using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Server._RMC14.TacticalMap;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
#pragma warning disable RA0002 // Configure tracked fixtures without injecting blips into the tactical feed.

    [TestCase(false)]
    [TestCase(true)]
    public async Task ZLevelContactsKeepFactionAndVehicleFeedsTogether(bool secondaryMap)
    {
        EntityUid friend = default, enemy = default, vehicle = default;
        await Server.WaitPost(() =>
        {
            SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
            if (secondaryMap) SEntMan.EnsureComponent<TacticalMapComponent>(_lower);
            var icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "rmc_commander");
            EntityUid Track(Vector2 position)
            {
                var uid = SEntMan.SpawnEntity(null, new EntityCoordinates(_lower, position));
                SEntMan.EnsureComponent<TacticalMapIconComponent>(uid).Icon = icon;
                SEntMan.EnsureComponent<TacticalMapTrackedComponent>(uid);
                return uid;
            }
            friend = Track(new Vector2(2.5f, 3.5f));
            SEntMan.EnsureComponent<GovforMapTrackedComponent>(friend);
            enemy = Track(new Vector2(6.5f));
            SEntMan.EnsureComponent<OpforMapTrackedComponent>(enemy);
            vehicle = Track(new Vector2(-3.5f));
            SEntMan.EnsureComponent<VehicleInteriorComponent>(vehicle);
        });
        await Pair.RunTicksSync(20);
        await Server.WaitAssertion(() =>
        {
            var root = SComp<TacticalMapComponent>(_upper);
            Assert.That(root.GovforBlips.ContainsKey(friend.Id), Is.True);
            Assert.That(root.OpforBlips.ContainsKey(enemy.Id), Is.True);
            Assert.That(root.MarineBlips.ContainsKey(vehicle.Id), Is.True, "Vehicles need the same floor resolution as personnel.");
            foreach (var uid in new[] { friend, enemy, vehicle })
                Assert.That(SComp<ActiveTacticalMapTrackedComponent>(uid).Map, Is.EqualTo(_upper));
            if (secondaryMap)
            {
                var lower = SComp<TacticalMapComponent>(_lower);
                Assert.That(lower.GovforBlips, Is.Empty);
                Assert.That(lower.OpforBlips, Is.Empty);
                Assert.That(lower.MarineBlips, Is.Empty);
            }
            else
            {
                var tactical = SEntMan.System<TacticalMapSystem>();
                var computer = SComp<TacticalMapComputerComponent>(_console);
                tactical.RefreshReconstructionContacts((_console, computer));
                Assert.That(computer.Blips.ContainsKey(friend.Id), Is.True);
                Assert.That(computer.Blips.ContainsKey(enemy.Id), Is.False, "Another floor must not bypass faction visibility.");
                Assert.That(computer.Blips[friend.Id].Image!.RsiState, Is.EqualTo("rmc_commander"));
            }
        });
        await Server.WaitPost(() =>
        {
            foreach (var uid in new[] { friend, enemy, vehicle })
                SEntMan.System<SharedTransformSystem>().SetCoordinates(uid, new EntityCoordinates(_upper, new Vector2(7.5f)));
        });
        await Pair.RunTicksSync(20);
        await Server.WaitAssertion(() =>
        {
            var root = SComp<TacticalMapComponent>(_upper);
            Assert.That(root.GovforBlips[friend.Id].Indices, Is.EqualTo(new Vector2i(7, 7)));
            Assert.That(root.OpforBlips[enemy.Id].Indices, Is.EqualTo(new Vector2i(7, 7)));
            Assert.That(root.MarineBlips[vehicle.Id].Indices, Is.EqualTo(new Vector2i(7, 7)));
        });
    }
#pragma warning restore RA0002
}
