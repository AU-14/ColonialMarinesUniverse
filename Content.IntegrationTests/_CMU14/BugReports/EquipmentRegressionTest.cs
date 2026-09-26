#pragma warning disable RA0002 // Regression fixture arranges prototypes, access and payload state.

using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.util;
using Content.Shared.Explosion.Components;
using Content.Shared.Light.Components;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Rules;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class EquipmentRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: job
          id: BugReportOpforSpecialist
          name: generic-unknown
          roundSide: Opfor
          playTimeTracker: CMJobWeaponsSpecialist
          roundRole: WeaponsSpecialist
          accessGroups: [AU14GovforWeaponsSpecialist]
        - type: job
          id: BugReportPilotBase
          name: generic-unknown
          playTimeTracker: CMJobPilotDropship
        - type: job
          parent: BugReportPilotBase
          id: BugReportFighterPilot
        - type: loadout
          id: BugReportPilotChoice
        - type: loadoutGroup
          id: BugReportPilotGroup
          name: generic-unknown
          minLimit: 0
          maxLimit: 1
          loadouts: [BugReportPilotChoice]
        - type: roleLoadout
          id: JobBugReportPilotBase
          groups: [BugReportPilotGroup]
        - type: entity
          id: BugReportCASImpact
        """;

    [TestCase(true)]
    [TestCase(false)]
    public async Task CASPayloadRespectsBosenmoriCeilingAtImpact(bool roofed)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var area = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid.Owner);
            SEntMan.System<AreaSystem>().ReplaceArea(area, map.Tile.GridIndices,
                roofed ? "RMCAreaBosenmoriBashoUnknownCaves" : "RMCAreaBosenmoriBashoMountainClearing");
            Assert.That(SEntMan.System<AreaSystem>().CanCAS(map.GridCoords), Is.EqualTo(!roofed));
            var projectile = SEntMan.SpawnEntity(null, map.GridCoords);
            var flight = SEntMan.EnsureComponent<AmmoInFlightComponent>(projectile);
            flight.Target = map.GridCoords;
            flight.SpawnedMarker = true;
            flight.ShotsLeft = 1;
            flight.ShotsPerVolley = 1;
            flight.BulletSpread = 0;
            flight.ImpactEffects.Add("BugReportCASImpact");
            SEntMan.System<SharedDropshipWeaponSystem>().Update(0);
            var impacts = SEntMan.EntityQuery<MetaDataComponent>()
                .Where(meta => meta.EntityPrototype?.ID == "BugReportCASImpact").ToArray();
            Assert.That(impacts.Length, Is.EqualTo(roofed ? 0 : 1));
            foreach (var impact in impacts)
                SEntMan.DeleteEntity(impact.Owner);
            SEntMan.DeleteEntity(projectile);
        });
    }

    [Test]
    public async Task CASRecognizesUpperPlanetFloorsButRejectsUnrelatedMaps()
    {
        EntityUid lower = default, upper = default, unrelated = default, network = default;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            lower = maps.CreateMap(runMapInit: true);
            upper = maps.CreateMap(runMapInit: true);
            unrelated = maps.CreateMap(runMapInit: true);
            SEntMan.EnsureComponent<RMCPlanetComponent>(lower);
            var z = SEntMan.System<CMUZLevelsSystem>();
            var net = z.CreateZNetwork();
            network = net;
            Assert.That(z.TryAddMapsIntoZNetwork(net, new() { [lower] = 0, [upper] = 1 }), Is.True);
            var weapons = SEntMan.System<SharedDropshipWeaponSystem>();
            var validate = typeof(SharedDropshipWeaponSystem).GetMethod("IsValidTarget", BindingFlags.Instance | BindingFlags.NonPublic)!;
            foreach (var map in new[] { lower, upper, unrelated })
            {
                var target = SEntMan.SpawnEntity(null, new EntityCoordinates(map, Vector2.Zero));
                var component = SEntMan.EnsureComponent<DropshipTargetComponent>(target);
                Assert.That(validate.Invoke(weapons, [new Entity<DropshipTargetComponent?>(target, component)]),
                    Is.EqualTo(map != unrelated));
                SEntMan.DeleteEntity(target);
            }
        });
        foreach (var map in new[] { upper, lower, unrelated, network })
            await Pair.DeleteEntityTreeLeafFirst(map);
    }

    [Test]
    public async Task SpecialistAccessUsesOriginalSideDespiteInheritedEquipmentJob()
    {
        await Server.WaitAssertion(() =>
        {
            var card = SEntMan.Spawn();
            var access = SEntMan.EnsureComponent<AccessComponent>(card);
            access.Tags.Add("AU14AccessGovforSquadWeaponsSpecialist");
            var job = SProtoMan.Index<JobPrototype>("BugReportOpforSpecialist");
            typeof(StationSpawningSystem).GetMethod("SetWeaponsSpecialistAccess", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(SEntMan.System<StationSpawningSystem>(), [card, job, job]);
            Assert.That(access.Tags.Select(t => t.Id), Does.Contain("AU14AccessOpforSquadWeaponsSpecialist"));
            Assert.That(access.Tags.Select(t => t.Id), Does.Not.Contain("AU14AccessGovforSquadWeaponsSpecialist"));
            var vendor = SEntMan.Spawn();
            var reader = SEntMan.EnsureComponent<AccessReaderComponent>(vendor);
            typeof(PlatoonSpawnRuleSystem).GetMethod("SetRequisitionsVendorAccess", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(SEntMan.System<PlatoonSpawnRuleSystem>(), [vendor, PlatoonMarkerClass.SWeapons, "opfor"]);
            Assert.That(reader.AccessLists.SelectMany(list => list).Select(t => t.Id),
                Is.EqualTo(new[] { "AU14AccessOpforSquadWeaponsSpecialist" }));
            SEntMan.DeleteEntity(vendor);
            SEntMan.DeleteEntity(card);
        });
    }

    [Test]
    public async Task SavingConcretePilotRoleRetainsSelectionsFromParentLoadout()
    {
        await Server.WaitAssertion(() =>
        {
            var role = new RoleLoadout("JobBugReportFighterPilot");
            role.SelectedLoadouts["BugReportPilotGroup"] = [new Loadout { Prototype = "BugReportPilotChoice" }];
            var profile = new HumanoidCharacterProfile().WithLoadout("JobBugReportFighterPilot", role);
            profile.EnsureValid(ServerSession!, IoCManager.Instance!);
            Assert.That(profile.Loadouts.ContainsKey("JobBugReportFighterPilot"), Is.True);
            var saved = profile.Loadouts["JobBugReportFighterPilot"];
            Assert.That(saved.Role, Is.EqualTo("JobBugReportPilotBase"));
            Assert.That(saved.SelectedLoadouts["BugReportPilotGroup"].Select(l => l.Prototype.Id),
                Is.EqualTo(new[] { "BugReportPilotChoice" }));
        });
    }

    [Test]
    public async Task FiredFlarePayloadIsLitBeforeItRegistersAsCASSignal()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var projectile = SEntMan.SpawnEntity("RMCFlareCASBullet", map.GridCoords);
            var scatter = SEntMan.GetComponent<ScatteringGrenadeComponent>(projectile);
            scatter.IsTriggered = true;
            SEntMan.System<ScatteringGrenadeSystem>().Update(0);
            var flares = SEntMan.EntityQuery<ExpendableLightComponent, FlareSignalComponent>().ToArray();
            Assert.That(flares, Is.Not.Empty);
            foreach (var (light, _) in flares)
            {
                Assert.That(light.Activated, Is.True);
                Assert.That(SEntMan.HasComponent<DropshipTargetComponent>(light.Owner), Is.True);
                SEntMan.DeleteEntity(light.Owner);
            }
        });
    }
}
