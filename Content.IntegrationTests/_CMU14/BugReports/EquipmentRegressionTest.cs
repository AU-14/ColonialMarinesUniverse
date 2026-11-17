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
            var impacts = SEntMan.QueryEntities<MetaDataComponent>()
                .Where(meta => meta.Comp.EntityPrototype?.ID == "BugReportCASImpact").ToArray();
            Assert.That(impacts.Length, Is.EqualTo(roofed ? 0 : 1));
            foreach (var impact in impacts)
                SEntMan.DeleteEntity(impact.Owner);
            SEntMan.DeleteEntity(projectile);
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
            var flares = SEntMan.QueryEntities<ExpendableLightComponent, FlareSignalComponent>().ToArray();
            Assert.That(flares, Is.Not.Empty);
            foreach (var (light, _) in flares)
            {
                Assert.That(light.Comp.Activated, Is.True);
                // The flare rework (RMC-14#9465) tracks a fired signal flare first and only makes it a CAS target
                // once its activation delay has passed and it has settled.
                Assert.That(SEntMan.TryGetComponent(light.Owner, out ActiveFlareSignalComponent? active), Is.True);
                Assert.That(active!.Abbreviation, Is.Not.Null);
                Assert.That(SEntMan.HasComponent<DropshipTargetComponent>(light.Owner), Is.False);
                SEntMan.DeleteEntity(light.Owner);
            }
        });
    }
}
