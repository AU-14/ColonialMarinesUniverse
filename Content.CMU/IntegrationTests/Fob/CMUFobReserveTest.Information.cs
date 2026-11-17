#pragma warning disable RA0002 // Arrange reserve deadlines and observe private map publication.
using System.Linq;
using Content.Server._RMC14.TacticalMap;
using Content.Server.CMU14.Fob;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.Fob;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Fob;

public sealed partial class CMUFobReserveTest
{

    [Test]
    public async Task InformationRelayMarkersStayPrivateAndBothDisappearOnResolution()
    {
        await SetupNetwork();
        await Server.WaitAssertion(() =>
        {
            var grid = SEntMan.GetComponent<TransformComponent>(_site).GridUid!.Value;
            var map = SEntMan.EnsureComponent<TacticalMapComponent>(grid);
            var maps = Server.System<TacticalMapSystem>();
            Entity<TacticalMapUserComponent> Viewer(string faction)
            {
                var uid = SEntMan.SpawnEntity(null, _coords);
                SEntMan.AddComponent<MarineComponent>(uid).Faction = faction;
                var user = SEntMan.AddComponent<TacticalMapUserComponent>(uid);
                user.Map = grid; user.Govfor = true; user.Opfor = true; user.Xenos = true;
                return (uid, user);
            }
            var friendly = Viewer("GOVFOR");
            var enemy = Viewer("opfor");
            Assert.That(Reserve.TryOfferRelays(_network, _first, _second, out var job), Is.True);
            maps.UpdateUserData(friendly, map);
            maps.UpdateUserData(enemy, map);
            Assert.That(friendly.Comp.GovforBlips.Keys, Is.EquivalentTo(new[] { _first.Id, _second.Id }));
            Assert.That(friendly.Comp.GovforBlips.Values.All(blip => blip.CMUFobLabel != null), Is.True);
            Assert.That(enemy.Comp.GovforBlips, Is.Empty, "A viewer flag alone cannot grant a different faction's missions.");
            Assert.That(friendly.Comp.XenoBlips, Is.Empty);
            Assert.That(maps.GetFobMarkers(enemy), Is.Empty);
            Assert.That(maps.GetFobMarkers(friendly), Has.Count.EqualTo(2));
            var console = SEntMan.SpawnEntity(null, _coords);
            var computer = SEntMan.AddComponent<TacticalMapComputerComponent>(console);
            computer.Faction = "GOVFOR";
            maps.RefreshReconstructionContacts((console, computer));
            Assert.That(computer.Blips.Values.Any(blip => blip.CMUFobLabel != null), Is.False,
                "Private markers must not leak through PVS-replicated console components.");
            Assert.That(map.GovforBlips, Is.Empty, "Sensor source buckets must never contain private relay missions.");
            Assert.That(map.LastUpdateGovforBlips, Is.Empty);
            Assert.That(Reserve.TryCompleteRelay(_second, job, out _), Is.True);
            Assert.That(friendly.Comp.GovforBlips, Is.Empty, "Both alternatives must vanish immediately, without another publication.");
            _network.Comp.NextJobAt = SGameTiming.CurTime;
            var fullBankRelay = Relay();
            Assert.That(Reserve.TryOfferRelays(_network, fullBankRelay, Relay(), out _), Is.True);
            Assert.That(Reserve.TryGetRelayAssignment(fullBankRelay, out _), Is.True);
            Assert.That(Reserve.TryGetRelayJob(fullBankRelay, out _, out _), Is.False,
                "A full bank preserves the visible mission without allowing a wasteful redemption.");
            Assert.That(friendly.Comp.GovforBlips, Has.Count.EqualTo(2));
            _network.Comp.JobExpiresAt = SGameTiming.CurTime;
            Reserve.IsActive(_network);
            Assert.That(friendly.Comp.GovforBlips, Is.Empty, "Expiry must revoke both alternatives immediately.");
            _network.Comp.NextJobAt = SGameTiming.CurTime;
            Assert.That(Reserve.TryOfferRelays(_network, Relay(), Relay(), out _), Is.True);
            Reserve.CloseNetwork(_network);
            Assert.That(friendly.Comp.GovforBlips, Is.Empty);
            CleanupRound(); StartRound(false);
            maps.UpdateUserData(friendly, map);
            Assert.That(friendly.Comp.GovforBlips, Is.Empty);
        });
    }
}
