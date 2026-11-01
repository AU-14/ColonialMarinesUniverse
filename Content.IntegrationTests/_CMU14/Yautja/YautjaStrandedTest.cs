using System.Linq;
using Content.Server.CMU14.Yautja;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.TacticalMap;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Inventory;
using Content.Shared.NPC.Components;
using Content.Shared.Preferences;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaStrandedTest
{
    [Test]
    public async Task StrandedJobSpawnsWithStrandedFactionAndUnlockedUntrackedBracer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var inventory = entMan.System<InventorySystem>();
            var containers = entMan.System<SharedContainerSystem>();
            var stationSpawning = entMan.System<StationSpawningSystem>();

            var job = prototypes.Index<JobPrototype>(YautjaStrandedSystem.StrandedJob);
            var threat = prototypes.Index<DepartmentPrototype>("AU14DepartmentThreat");
            Assert.Multiple(() =>
            {
                Assert.That(job.Whitelisted, Is.True);
                Assert.That(job.WhitelistParent?.Id, Is.EqualTo("CMUYautjaHunter"),
                    "stranded rides on the normal pred whitelist like cmss13 WHITELIST_YAUTJA");
                Assert.That(job.JobEntity, Is.EqualTo("CMUMobYautjaStranded"));
                Assert.That(threat.Roles, Does.Contain("CMUYautjaStranded"),
                    "the hunt department is what the lobby hunt filter lists");
            });

            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                .WithYautjaProfile(YautjaCharacterProfile.Default.WithName("Stranded Hunter"));
            var stranded = stationSpawning.SpawnPlayerMob(map.GridCoords, YautjaStrandedSystem.StrandedJob, profile, station: null);

            try
            {
                Assert.That(entMan.HasComponent<YautjaStrandedComponent>(stranded), Is.True);
                Assert.That(entMan.GetComponent<NpcFactionMemberComponent>(stranded).Factions.Select(id => id.Id),
                    Is.EquivalentTo(new[] { "CMUYautjaStranded" }));

                // cmss13 stranded skips the minimap setup entirely
                Assert.That(entMan.HasComponent<YautjaMapTrackedComponent>(stranded), Is.False);
                Assert.That(entMan.HasComponent<TacticalMapTrackedComponent>(stranded), Is.False);
                Assert.That(entMan.HasComponent<TacticalMapUserComponent>(stranded), Is.False);

                Assert.That(inventory.TryGetSlotEntity(stranded, "ears", out var communicator), Is.True);
                Assert.That(containers.TryGetContainer(communicator!.Value, EncryptionKeyHolderComponent.KeyContainerName, out var keys), Is.True);
                Assert.That(entMan.GetComponent<EncryptionKeyComponent>(keys!.ContainedEntities.Single()).Channels,
                    Is.EquivalentTo(new[] { "CMUYautjaStranded" }));

                Assert.That(inventory.TryGetSlotEntity(stranded, "gloves", out var bracer), Is.True);
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer!.Value);
                Assert.That(bracerComp.Locked, Is.False, "cmss13 equipped() returns before toggle_lock_internal for stranded");
                Assert.That(entMan.HasComponent<YautjaBracerTacticalMapMarkerComponent>(stranded), Is.False);

                var action = entMan.SpawnEntity("CMUActionYautjaToggleBracerIdChip", MapCoordinates.Nullspace);
                try
                {
                    var deploy = new YautjaToggleBracerIdChipActionEvent
                    {
                        Performer = stranded,
                        Action = (action, entMan.GetComponent<ActionComponent>(action)),
                    };
                    entMan.EventBus.RaiseLocalEvent(bracer.Value, deploy);

                    Assert.That(bracerComp.IdChip, Is.Not.Null);
                    Assert.That(entMan.GetComponent<AccessComponent>(bracerComp.IdChip!.Value).Tags.Select(tag => tag.Id),
                        Is.EquivalentTo(new[] { "CMUAccessYautjaSecure" }),
                        "cmss13 bracer id returns early for stranded, secure access only");
                }
                finally
                {
                    entMan.DeleteEntity(action);
                }
            }
            finally
            {
                if (!entMan.Deleted(stranded))
                    entMan.DeleteEntity(stranded);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StrandedHutHasRackAndOneWayPadToGroundRelay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var stranded = entMan.System<YautjaStrandedSystem>();
            var transform = entMan.System<SharedTransformSystem>();

            var hut = stranded.EnsureHut();
            Assert.That(hut, Is.Not.Null);
            Assert.That(stranded.EnsureHut(), Is.EqualTo(hut), "the hut is built once and reused");
            Assert.That(entMan.HasComponent<MapGridComponent>(hut!.Value), Is.True);

            EntityUid? pad = null;
            var hasRack = false;
            var hutChildren = entMan.GetComponent<TransformComponent>(hut.Value).ChildEnumerator;
            while (hutChildren.MoveNext(out var child))
            {
                var id = entMan.GetComponent<MetaDataComponent>(child).EntityPrototype?.ID;
                if (id == "CMUYautjaStrandedLoadoutVendor")
                    hasRack = true;
                if (entMan.HasComponent<YautjaStrandedTeleporterComponent>(child))
                    pad = child;
            }

            Assert.That(hasRack, Is.True, "cmss13 predsurv_base has the stranded gear vendors");
            Assert.That(pad, Is.Not.Null);

            var destination = entMan.SpawnEntity("CMUYautjaGroundRelayDestination", map.GridCoords);
            entMan.GetComponent<YautjaRelayDestinationComponent>(destination).Id = "test.ground";

            var hunter = entMan.SpawnEntity("CMUMobYautjaStranded", entMan.GetComponent<TransformComponent>(pad!.Value).Coordinates);
            try
            {
                var ev = new YautjaStrandedTeleportChosenEvent(entMan.GetNetEntity(hunter), entMan.GetNetEntity(destination));
                entMan.EventBus.RaiseLocalEvent(pad.Value, ev);

                Assert.That(transform.GetMapId(hunter), Is.EqualTo(map.MapId), "the pad drops them at the chosen ground relay");
            }
            finally
            {
                entMan.DeleteEntity(hunter);
                entMan.DeleteEntity(destination);
            }
        });

        await pair.CleanReturnAsync();
    }
}
