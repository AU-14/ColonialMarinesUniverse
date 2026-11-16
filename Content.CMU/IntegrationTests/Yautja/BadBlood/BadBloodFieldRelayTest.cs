using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Hands.EntitySystems;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-508: the field uplink relay. It is planted only under open sky, a planted relay is a working uplink site until
// its battery runs out, and requisitions sell it only while the Bad Blood rule runs
[TestFixture]
public sealed class BadBloodFieldRelayTest : GameTest
{
    private const string Kit = "CMUBadBloodFieldRelayKit";
    private const string Crate = "CMUCrateBadBloodFieldRelay";
    private const string OpenArea = "BadBloodRelayTestAreaOpen";
    private const string RoofedArea = "BadBloodRelayTestAreaRoofed";
    private const string Console = "BadBloodRelayTestRequisitions";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        # open sky. CMU's ceiling tiers (AreaSystem.CeilingTiers.cs) rebuild every flag from the strongest
        # restriction, so the area needs all the open-sky flags, as RMCAreaProtectionZero has
        - type: entity
          parent: RMCAreaProtectionZero
          id: BadBloodRelayTestAreaOpen

        - type: entity
          parent: RMCAreaBase
          id: BadBloodRelayTestAreaRoofed
          components:
          - type: Area
            supplyDrop: false

        - type: entity
          id: BadBloodRelayTestRequisitions
          components:
          - type: ActivatableUI
            key: enum.RequisitionsUIKey.Key
          - type: UserInterface
            interfaces:
              enum.RequisitionsUIKey.Key:
                type: RequisitionsBui
          - type: RequisitionsComputer
            faction: govfor
            categories:
            - name: Test
              entries: []
        """;

    [Test]
    public async Task PlantedOnlyUnderOpenSkyAndWorksUntilTheBatteryDies()
    {
        var map = await Pair.CreateTestMap();
        var spawned = new List<EntityUid>();
        EntityUid marine = default;
        EntityUid kit = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var relays = Server.System<BadBloodFieldRelaySystem>();
                var hands = Server.System<SharedHandsSystem>();
                var areas = Server.System<AreaSystem>();
                var areaGrid = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid);

                marine = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, Vector2.Zero);
                spawned.Add(marine);
                kit = SEntMan.SpawnEntity(Kit, map.GridCoords);
                spawned.Add(kit);
                Assert.That(hands.TryPickupAnyHand(marine, kit), Is.True);

                var kitComp = SComp<BadBloodFieldRelayKitComponent>(kit);
                Assert.That(kitComp.PlantTime, Is.EqualTo(TimeSpan.FromSeconds(20)));
                kitComp.PlantTime = TimeSpan.FromSeconds(1);

                // under a roof: the area stops supply drops
                areas.ReplaceArea(areaGrid, map.Tile.GridIndices, RoofedArea);
                Assert.That(relays.CanPlant(marine, kit, out var reason), Is.False);
                Assert.That(reason, Is.EqualTo("cmu-bad-blood-obj-relay-roofed"));
                Assert.That(relays.TryStartPlant(marine, kit), Is.False);

                areas.ReplaceArea(areaGrid, map.Tile.GridIndices, OpenArea);
                Assert.That(relays.CanPlant(marine, kit, out var openReason), Is.True, $"refused on open ground: {openReason}");
                Assert.That(relays.TryStartPlant(marine, kit), Is.True);
            });

            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var chains = Server.System<BadBloodObjectiveChainSystem>();
                Assert.That(SEntMan.Deleted(kit), Is.True, "the kit is used up");

                var query = SEntMan.EntityQueryEnumerator<BadBloodFieldRelayComponent>();
                Assert.That(query.MoveNext(out var relay, out var relayComp), Is.True, "the relay is planted");
                spawned.Add(relay);

                var site = SComp<BadBloodUplinkSiteComponent>(relay);
                Assert.Multiple(() =>
                {
                    Assert.That(SComp<TransformComponent>(relay).Anchored, Is.True);
                    Assert.That(site.Kind, Is.EqualTo(BadBloodUplinkSiteKind.Relay));
                    Assert.That(site.UplinkTime, Is.EqualTo(TimeSpan.FromSeconds(150)));
                    Assert.That(relayComp!.ChargeDuration, Is.EqualTo(TimeSpan.FromSeconds(3600)));
                    Assert.That(SEntMan.HasComponent<BadBloodSabotageTargetComponent>(relay), Is.True);
                    Assert.That(chains.IsSiteWorking(relay), Is.True, "a planted relay is a working uplink site");
                });

                relayComp!.ChargeUsed = relayComp.ChargeDuration;
                Assert.That(chains.IsSiteWorking(relay), Is.False, "an empty relay is not");
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                for (var i = spawned.Count - 1; i >= 0; i--)
                {
                    if (SEntMan.EntityExists(spawned[i]))
                        SEntMan.DeleteEntity(spawned[i]);
                }
            });
        }
    }

    [Test]
    public async Task RequisitionsSellTheRelayOnlyOnBadBloodRounds()
    {
        var map = await Pair.CreateTestMap();
        var spawned = new List<EntityUid>();
        BadBloodTestParty? party = null;
        EntityUid before = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().IsRuleActive(), Is.False);
                before = SEntMan.SpawnEntity(Console, map.GridCoords);
                spawned.Add(before);
                Assert.That(Sells(before), Is.False, "no Bad Blood rule, no relay");
            });

            party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);

            await Server.WaitAssertion(() =>
            {
                Assert.That(Sells(before), Is.True, "the rule start adds it to the consoles already there");

                var after = SEntMan.SpawnEntity(Console, map.GridCoords.Offset(new Vector2(2, 0)));
                spawned.Add(after);
                Assert.That(Sells(after), Is.True, "a catalog built while the rule runs has it");

                var entries = SComp<RequisitionsComputerComponent>(after).Categories
                    .SelectMany(c => c.Entries)
                    .Where(e => e.Crate.Id == Crate)
                    .ToList();
                Assert.That(entries, Has.Count.EqualTo(1));
                Assert.That(entries[0].Cost, Is.EqualTo(1500));
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                for (var i = spawned.Count - 1; i >= 0; i--)
                {
                    if (SEntMan.EntityExists(spawned[i]))
                        SEntMan.DeleteEntity(spawned[i]);
                }
            });

            if (party != null)
            {
                await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
                await Server.WaitPost(() =>
                {
                    foreach (var uid in party.All)
                    {
                        if (SEntMan.EntityExists(uid))
                            SEntMan.DeleteEntity(uid);
                    }
                });
            }
        }
    }

    // call on the server thread
    private bool Sells(EntityUid console)
    {
        return Server.System<BadBloodFieldRelaySystem>()
            .HasCatalogEntry((console, SComp<RequisitionsComputerComponent>(console)), Crate);
    }
}
