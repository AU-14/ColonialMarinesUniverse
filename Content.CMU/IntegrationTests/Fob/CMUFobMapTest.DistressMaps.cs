#pragma warning disable RA0002 // Exercise authored maps through landing, assembly and relay transitions.

using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Fob;
using Content.Shared._RMC14.Construction;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CMU14.Fob;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Fob;

public sealed partial class CMUFobMapTest
{
    [NonParallelizable] // Full colony worlds are large; avoid loading several copies concurrently.
    [TestCase("HopesRetreat/HopesRetreatMultiZ0.yml", "HopesRetreat")]
    [TestCase("HopesRetreat/HopesRetreatMultiZ0.yml", "Octanis")]
    [TestCase("BosenmoriBasho/BosenmoriBashoMultiZ0.yml", "WesternPad")]
    [TestCase("BosenmoriBasho/BosenmoriBashoMultiZ0.yml", "EasternPad")]
    [TestCase("PortNereid/portneroidmultiz0.yml", "Spaceport")]
    [TestCase("PortNereid/portneroidmultiz0.yml", "CargoDepot")]
    [TestCase("SorokyneStrata/sorokyne0.yml", "Sorokyne")]
    [TestCase("SolarisRidge/SolarisRidgeMultiZ0.yml", "Communications")]
    [TestCase("SolarisRidge/SolarisRidgeMultiZ0.yml", "Engineering")]
    [TestCase("FortMcNeil/FortMcNeilMultiZ0.yml", "FortMcNeil")]
    [TestCase("Trijent/trijent.yml", "Airstrip")]
    [TestCase("Trijent/trijent.yml", "Aerodrome")]
    [TestCase("ShivasSnowball/ShivasSnowballMultiZ0.yml", "Research")]
    [TestCase("ShivasSnowball/ShivasSnowballMultiZ0.yml", "Valley")]
    [TestCase("LV624/LV624MultiZ0.yml", "Robotics")]
    [TestCase("LV624/LV624MultiZ0.yml", "Nexus")]
    public async Task DistressSiteSupportsPhysicalDeploymentAndRelayProgression(string surface, string selected)
    {
        Entity<CMUFobSiteComponent> site = default;
        Entity<CMUFobNetworkComponent> bank = default;
        await Server.WaitAssertion(() =>
        {
            StartRound();
            LoadSurface("/Maps/CMU14/Planets/" + surface);
            site = Site(selected);
            Land(site);
            Assert.That(Reserve.TryGetNetwork("govfor", out bank), Is.True, "A valid primary LZ must select its authored FOB.");
            Assert.That(Withdrawal.IsProtectedWithdrawalAllowed(), Is.False);
            AssertPair(site, bank, CMUFobRelayTier.Near, CMUFobRelayTier.Near);

            // Find usable terrain near the outer boundary, without hardcoding map offsets or changing the map.
            var grid = SEntMan.GetComponent<MapGridComponent>(_map);
            var center = Transform.GetWorldPosition(bank);
            var construction = Server.System<RMCConstructionSystem>();
            var mapSystem = Server.System<CMUFobMapSystem>();
            var spots = Maps.GetAllTiles(_map, grid)
                .Select(tile => new EntityCoordinates(_map, tile.GridIndices.X + 0.5f, tile.GridIndices.Y + 0.5f))
                .Where(at => Vector2.Distance(at.Position, center) > bank.Comp.DeploymentRange - 8
                    && Vector2.Distance(at.Position, center) < bank.Comp.DeploymentRange - 2
                    && new[] { Vector2.Zero, Vector2.UnitX, Vector2.UnitY, Vector2.One }.All(offset =>
                        !Maps.GetTileRef(_map, grid, at.Offset(offset)).Tile.IsEmpty
                        && mapSystem.CanPlaceUtility(bank, at.Offset(offset).Position)
                        && construction.CanBuildAt(at.Offset(offset), "FOB equipment", out _, anchoring: true,
                            collision: CollisionGroup.Impassable | CollisionGroup.MidImpassable)))
                .Take(1).ToArray();
            Assert.That(spots, Is.Not.Empty, "The enlarged deployment area must have usable terrain outside the landing footprint.");
            var ground = spots.Single();
            var user = SEntMan.SpawnAtPosition("CMMobHuman", ground.Offset(Vector2.UnitX));
            SEntMan.EnsureComponent<MarineComponent>(user).Faction = "govfor";
            Server.System<SkillsSystem>().SetSkill(user, "RMCSkillConstruction", 3);
            var wrench = SEntMan.SpawnAtPosition("CMWrench", ground);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, wrench), Is.True);
            var control = SEntMan.SpawnAtPosition("CMUFobControlModule", ground);
            SEntMan.EventBus.RaiseLocalEvent(control, new InteractUsingEvent(user, wrench, control, ground));
            Assert.That(Server.System<CMUFobInteractionSystem>().TryBindControl((control,
                SEntMan.GetComponent<CMUFobControlComponent>(control)), out _), Is.True);
            var platform = SEntMan.SpawnAtPosition("CMUFobPlatformModule", ground.Offset(Vector2.UnitY));
            Transform.SetCoordinates(user, ground.Offset(Vector2.UnitX + Vector2.UnitY));
            var mount = SEntMan.GetComponent<CMUFobGunMountComponent>(platform);
            Assert.That(Server.System<CMUFobDefenseSystem>().TryDeployPlatform((platform, mount), user), Is.True,
                $"Platform at {ground.Offset(Vector2.UnitY)} with operator at {ground.Offset(Vector2.One)} must be deployable.");

            // Existing landing exclusions still apply even though the deployment circle is much larger.
            Assert.That(mapSystem.CanPlaceUtility(bank, Transform.GetWorldPosition(site)), Is.False);
            var remaining = Reserve.GetRemaining(bank);
            foreach (var destination in _config.Sites)
                Land(destination);
            Assert.That(Reserve.GetRemaining(bank), Is.EqualTo(remaining));
            Assert.That(OnSurface<CMUFobNetworkComponent>(), Is.EquivalentTo(new[] { bank.Owner }));
            Assert.That(SEntMan.GetComponent<CMUFobDefenseNetworkComponent>(bank).Mounts, Is.EquivalentTo(new[] { platform }));

            bank.Comp.StoredReserve = TimeSpan.FromSeconds(1);
            bank.Comp.RelayInterval = TimeSpan.FromSeconds(0.1);
            Assert.That(Reserve.TryGetRelayJob(bank.Comp.FirstRelay!.Value, out var job, out _), Is.True);
            Assert.That(Reserve.TryCompleteRelay(bank.Comp.FirstRelay.Value, job, out _), Is.True);
            Assert.That(Reserve.GetRemaining(bank), Is.GreaterThan(TimeSpan.FromSeconds(1)));
            SetElapsed(_config.IntermediateAfter + TimeSpan.FromSeconds(1));
        });
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            AssertPair(site, bank, CMUFobRelayTier.Near, CMUFobRelayTier.Intermediate);
            bank.Comp.StoredReserve = TimeSpan.FromSeconds(1);
            Assert.That(Reserve.TryGetRelayJob(bank.Comp.FirstRelay!.Value, out var job, out _), Is.True);
            Assert.That(Reserve.TryCompleteRelay(bank.Comp.FirstRelay.Value, job, out _), Is.True);
            SetElapsed(_config.FarAfter + TimeSpan.FromSeconds(1));
        });
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() => AssertPair(site, bank, CMUFobRelayTier.Intermediate, CMUFobRelayTier.Far));
    }

    [Test]
    public async Task RelayInsideDeploymentAreaRejectsLayoutAndRetainsLegacy()
    {
        EntityUid legacy = default;
        await Server.WaitAssertion(() =>
        {
            LoadSurface();
            var site = Site("Nexus");
            var relay = site.Comp.Relays[0].Anchor;
            var center = Transform.GetWorldPosition(site);
            var tile = Maps.GetAllTiles(_map, SEntMan.GetComponent<MapGridComponent>(_map)).First(tile =>
                Vector2.Distance(new Vector2(tile.GridIndices.X + 0.5f, tile.GridIndices.Y + 0.5f), center)
                    is > CMUFobNetworkComponent.DefaultDeploymentRange - 8 and < CMUFobNetworkComponent.DefaultDeploymentRange - 2);
            Transform.Unanchor(relay);
            Transform.SetCoordinates(relay, new EntityCoordinates(_map, tile.GridIndices.X + 0.5f, tile.GridIndices.Y + 0.5f));
            Assert.That(Transform.AnchorEntity(relay), Is.True);
            legacy = Spawn("RMCTurretGaussSpaceborne", site);
            StartRound();
            Land(site);
        });
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reserve.TryGetNetwork("govfor", out _), Is.False);
            Assert.That(SEntMan.EntityExists(legacy), Is.True);
            Assert.That(Withdrawal.IsProtectedWithdrawalAllowed(), Is.True);
        });
    }
}
