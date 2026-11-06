using System.Linq;
using System.Numerics;
using Content.Server._RMC14.Sentry;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.VendorMarker;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared._RMC14.Dropship.Utility.Components;
using Content.Shared._RMC14.Dropship.Utility.Systems;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14;
using Content.Shared.CMU14.util;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Content.Shared.NPC.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class CMUBushSentryValidationTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUBushSentryValidationPlanet
          components:
          - type: RMCPlanetMapPrototype
            mapId: USSBushRedux
            inRotation: false
            govforinship: true
            opforinship: false
            govfordropships: 0
            opfordropships: 0
            govforfighters: 0
            opforfighters: 0
        """;

    [Test]
    public async Task BushMappedDeployersInheritGovforAndProtectItsPersonnel()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        EntityUid turret = default;
        EntityUid friendlyNpc = default;
        EntityUid friendlyIff = default;
        EntityUid hostile = default;
        EntityCoordinates firingPosition = default;
        var initialAmmo = 0;

        await server.WaitAssertion(() =>
        {
            var ticker = entities.System<GameTicker>();
            var round = entities.System<AuRoundSystem>();
            round.SetGovfor("USSBushRedux");
            entities.System<PlatoonSpawnRuleSystem>().SelectedGovforPlatoon = server.ProtoMan.Index<PlatoonPrototype>("USCM");
            Assert.That(round.SetPlanet("CMUBushSentryValidationPlanet"), Is.True);
            var grids = ticker.LoadGameMap(server.ProtoMan.Index<GameMapPrototype>("USSBushRedux"),
                out _, DeserializationOptions.Default with { InitializeMaps = true });

            var markers = entities.EntityQueryEnumerator<VendorMarkerComponent, TransformComponent>();
            var positions = new List<EntityCoordinates>();
            while (markers.MoveNext(out var marker, out var transform))
            {
                if (marker.Ship && marker.Class == PlatoonMarkerClass.SentryDeployer)
                    positions.Add(transform.Coordinates);
            }

            Assert.That(positions, Is.Not.Empty, "The real Bush map must supply ship sentry markers.");
            Assert.That(ticker.StartGameRule("PlatoonSpawn"), Is.True);
            var targeting = entities.System<SentryTargetingSystem>();
            var deployerSystem = entities.System<SharedRMCEquipmentDeployerSystem>();
            var deployed = 0;
            var deployers = entities.EntityQueryEnumerator<RMCEquipmentDeployerComponent, TransformComponent>();
            while (deployers.MoveNext(out var uid, out var deployer, out var transform))
            {
                if (!positions.Contains(transform.Coordinates))
                    continue;

                Assert.That(transform.GridUid, Is.Not.Null);
                Assert.That(entities.GetComponent<ShipFactionComponent>(transform.GridUid!.Value).Faction,
                    Is.EqualTo("govfor"), "Round setup itself must tag the deck; this test does not inject ShipFaction.");
                Assert.That(deployerSystem.TryDeploy(uid, true), Is.True);
                Assert.That(deployer.DeployEntity, Is.Not.Null);
                var gun = entities.GetEntity(deployer.DeployEntity!.Value);
                var iff = entities.GetComponent<SentryTargetingComponent>(gun);
                Assert.That(iff.FriendlyFactions, Does.Contain("GOVFOR"));
                deployed++;
                turret = gun;
            }

            Assert.That(deployed, Is.EqualTo(positions.Count), "Every mapped Bush sentry marker must produce a configured turret.");
            // Keep the actual deployed turret and deck, with an isolated corridor to avoid unrelated map obstructions.
            var grid = entities.GetComponent<TransformComponent>(turret).GridUid!.Value;
            var gridComponent = entities.GetComponent<MapGridComponent>(grid);
            var maps = entities.System<SharedMapSystem>();
            var floor = new Tile(server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            for (var x = 999; x <= 1007; x++)
            for (var y = 998; y <= 1003; y++)
                maps.SetTile(grid, gridComponent, new Vector2i(x, y), floor);
            firingPosition = new EntityCoordinates(grid, new Vector2(1000.5f, 1000.5f));
            entities.System<SharedTransformSystem>().SetCoordinates(turret, firingPosition);
            friendlyNpc = entities.SpawnEntity("CMMobHuman", firingPosition.Offset(new Vector2(3, 1.5f)));
            friendlyIff = entities.SpawnEntity("CMMobHuman", firingPosition.Offset(new Vector2(3, -1.5f)));
            var factions = entities.System<NpcFactionSystem>();
            factions.ClearFactions(friendlyNpc);
            factions.AddFaction(friendlyNpc, "GOVFOR");
            entities.System<GunIFFSystem>().ClearUserFactions(friendlyNpc);
            factions.ClearFactions(friendlyIff);
            entities.System<GunIFFSystem>().SetUserFaction(friendlyIff, "GOVFOR");
            var comp = entities.GetComponent<SentryTargetingComponent>(turret);
            Assert.That(targeting.IsValidTarget((turret, comp), friendlyNpc), Is.False);
            Assert.That(targeting.IsValidTarget((turret, comp), friendlyIff), Is.False);
            initialAmmo = Ammo();
        });

        await pair.RunSeconds(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(Ammo(), Is.EqualTo(initialAmmo), "A deployed Bush turret must hold fire around GOVFOR personnel without wearable IDs.");
            hostile = entities.SpawnEntity("CMMobHuman", firingPosition.Offset(new Vector2(5, 0)));
            var factions = entities.System<NpcFactionSystem>();
            factions.ClearFactions(hostile);
            factions.AddFaction(hostile, "OPFOR");
            entities.System<GunIFFSystem>().SetUserFaction(hostile, "OPFOR");
        });
        await pair.RunSeconds(6);
        await server.WaitAssertion(() =>
        {
            Assert.That(Ammo(), Is.LessThan(initialAmmo), "Friendly protection must not disable the actual Bush turret.");
            var damage = entities.System<DamageableSystem>();
            // The isolated deck corridor is exposed to vacuum; the gauss round itself deals piercing damage.
            Assert.That(damage.GetAllDamage(hostile).DamageDict.GetValueOrDefault("Piercing"), Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(damage.GetAllDamage(friendlyNpc).DamageDict.GetValueOrDefault("Piercing"), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(damage.GetAllDamage(friendlyIff).DamageDict.GetValueOrDefault("Piercing"), Is.EqualTo(FixedPoint2.Zero));
        });
        await pair.CleanReturnAsync();

        int Ammo()
        {
            var count = new GetAmmoCountEvent();
            entities.EventBus.RaiseLocalEvent(turret, ref count);
            return count.Count;
        }
    }
}
