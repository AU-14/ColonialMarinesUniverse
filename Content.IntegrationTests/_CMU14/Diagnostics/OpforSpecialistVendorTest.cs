#pragma warning disable RA0002 // Arrange the vendor identity assigned by job spawning.
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.util;
using Content.Shared.UserInterface;
using Content.Shared.Hands.EntitySystems;
using Content.Shared._RMC14.Marines.Squads;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class OpforSpecialistVendorTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };
    [Test]
    public async Task EveryPlatoonSpecialistRackAcceptsOpforSpecialistsButRejectsOtherJobs()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var spawning = Server.System<PlatoonSpawnRuleSystem>();
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var identity = SEntMan.EnsureComponent<CMVendorUserComponent>(user);
            var card = SEntMan.SpawnEntity("CMIDCardStandardDogtag", map.GridCoords);
            SEntMan.EnsureComponent<AccessComponent>(card).Tags.UnionWith(new ProtoId<AccessLevelPrototype>[]
                { "AU14AccessOpforSquadWeaponsSpecialist", "AU14AccessOpforSquad", "AU14AccessOpfor" });
            SEntMan.EnsureComponent<IdCardOwnerComponent>(card).Id = user;
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, card), Is.True);
            var checkedRacks = new HashSet<string>();
            var checkedPlatoons = new List<string>();
            foreach (var platoon in SProtoMan.EnumeratePrototypes<PlatoonPrototype>())
            {
                if (!spawning.TryResolvePlatoonVendor(platoon, PlatoonMarkerClass.SWeapons, out var rack))
                    continue;
                checkedPlatoons.Add(platoon.ID);
                if (!checkedRacks.Add(rack.Id)) continue;
                var vendor = SEntMan.SpawnEntity(rack, map.GridCoords);
                var reader = SEntMan.GetComponent<AccessReaderComponent>(vendor);
                Assert.That(Server.System<AccessReaderSystem>().IsAllowed(user, vendor), Is.True, rack.Id);
                Assert.That(Server.System<AccessReaderSystem>().IsAllowed(
                    new List<ProtoId<AccessLevelPrototype>> { "AU14AccessOpforSquadWeaponsSpecialist", "AU14AccessOpforSquad", "AU14AccessOpfor" },
                    [], vendor, reader), Is.True, rack.Id);
                identity.Id = "AU14JobOPFORWeaponsSpecialist";
                var open = new ActivatableUIOpenAttemptEvent(user, true);
                SEntMan.EventBus.RaiseLocalEvent(vendor, open);
                Assert.That(open.Cancelled, Is.False, $"OPFOR specialist must open {rack}");
                identity.Id = "AU14JobGOVFORWeaponsSpecialist";
                open = new ActivatableUIOpenAttemptEvent(user, true);
                SEntMan.EventBus.RaiseLocalEvent(vendor, open);
                Assert.That(open.Cancelled, Is.False, $"GOVFOR specialist must retain access to {rack}");
                identity.Id = null;
                open = new ActivatableUIOpenAttemptEvent(user, true);
                SEntMan.EventBus.RaiseLocalEvent(vendor, open);
                Assert.That(open.Cancelled, Is.True, $"access tags alone must not bypass the specialist job restriction on {rack}");
                SEntMan.DeleteEntity(vendor);
            }
            Assert.That(checkedRacks, Does.Contain("AU14SpecVend"));
            Assert.That(checkedRacks.Count, Is.GreaterThanOrEqualTo(7));
            TestContext.Out.WriteLine($"Verified {checkedRacks.Count} specialist racks across {checkedPlatoons.Count} platoons: {string.Join(", ", checkedPlatoons.Order())}");
        });
    }
}
