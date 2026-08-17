using System.Collections.Generic;
using Content.Server._RMC14.Sentry;
using Content.Shared._RMC14.Dropship.Utility.Components;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._AU14.Sentry;

[TestFixture]
public sealed class WeylandYutaniSentryTest
{
    private static readonly EntProtoId CorporateSentry = "AU14SentryOmniWeYu";
    private static readonly EntProtoId CorporateSentryDeployer = "AU14DeployerSentryWeYu";
    private static readonly EntProtoId CorporateAlmayerTurret = "AU14TurretAlmayerWeYu";
    private static readonly EntProtoId BaseSentryDeployer = "RMCDeployerSentry";
    private static readonly EntProtoId CorporateNpc = "AU14JobWYPMCPartyContractor";
    private static readonly EntProtoId<IFFFactionComponent> CorporateIff = "FactionWEYU";
    private static readonly EntProtoId<IFFFactionComponent> GovforIff = "GOVFOR";

    [Test]
    public async Task CorporateSentryWhitelistIsLockedAndRecognizesCorporateAffiliates()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var targetingSystem = server.System<SentryTargetingSystem>();
            var iffSystem = server.System<GunIFFSystem>();
            var sentry = entities.SpawnEntity(CorporateSentry, testMap.GridCoords);
            var corporateNpc = entities.SpawnEntity(CorporateNpc, testMap.GridCoords);
            var corporateIff = entities.SpawnEntity(null, testMap.GridCoords);
            var outsider = entities.SpawnEntity(null, testMap.GridCoords);

            try
            {
                iffSystem.SetUserFaction(corporateIff, CorporateIff);
                iffSystem.SetUserFaction(outsider, GovforIff);

                var targeting = entities.GetComponent<SentryTargetingComponent>(sentry);
                var expectedCorporateFactions = new[] { "AUWeYu", "WeYa" };

                Assert.Multiple(() =>
                {
                    Assert.That(targeting.LockedFriendlyFactions, Is.EquivalentTo(expectedCorporateFactions));
                    Assert.That(targeting.FriendlyFactions, Is.EquivalentTo(expectedCorporateFactions));
                    Assert.That(targetingSystem.IsValidTarget((sentry, targeting), corporateNpc), Is.False);
                    Assert.That(targetingSystem.IsValidTarget((sentry, targeting), corporateIff), Is.False);
                    Assert.That(targetingSystem.IsValidTarget((sentry, targeting), outsider), Is.True);
                });

                targetingSystem.SetFriendlyFactions((sentry, targeting), new HashSet<string> { "GOVFOR" });
                targetingSystem.ToggleFaction((sentry, targeting), "AUWeYu", false);
                targetingSystem.ClearFactionAssignment((sentry, targeting));
                targetingSystem.ApplyDeployerFactions(sentry, outsider);

                Assert.Multiple(() =>
                {
                    Assert.That(targeting.FriendlyFactions, Is.EquivalentTo(expectedCorporateFactions));
                    Assert.That(targeting.DeployedFriendlyFactions, Is.EquivalentTo(expectedCorporateFactions));
                    Assert.That(targeting.AllianceFriendlyNpcFactions, Is.Empty);
                });
            }
            finally
            {
                entities.DeleteEntity(sentry);
                entities.DeleteEntity(corporateNpc);
                entities.DeleteEntity(corporateIff);
                entities.DeleteEntity(outsider);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CorporateRmcDeployerSentryIsSeparateFromBaseDeployer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var deployer = entities.SpawnEntity(CorporateSentryDeployer, testMap.GridCoords);
            var baseDeployer = entities.SpawnEntity(BaseSentryDeployer, testMap.GridCoords);

            try
            {
                var deployerComponent = entities.GetComponent<RMCEquipmentDeployerComponent>(deployer);
                var baseDeployerComponent = entities.GetComponent<RMCEquipmentDeployerComponent>(baseDeployer);
                Assert.That(deployerComponent.DeployEntity, Is.Not.Null);
                Assert.That(baseDeployerComponent.DeployEntity, Is.Not.Null);

                var sentry = entities.GetEntity(deployerComponent.DeployEntity!.Value);
                var targeting = entities.GetComponent<SentryTargetingComponent>(sentry);
                var baseSentry = entities.GetEntity(baseDeployerComponent.DeployEntity!.Value);
                var baseTargeting = entities.GetComponent<SentryTargetingComponent>(baseSentry);
                var expectedCorporateFactions = new[] { "AUWeYu", "WeYa" };

                Assert.Multiple(() =>
                {
                    Assert.That(deployerComponent.DeployPrototype, Is.EqualTo(CorporateAlmayerTurret));
                    Assert.That(targeting.LockedFriendlyFactions, Is.EquivalentTo(expectedCorporateFactions));
                    Assert.That(targeting.FriendlyFactions, Is.EquivalentTo(expectedCorporateFactions));
                    Assert.That(baseDeployerComponent.DeployPrototype, Is.EqualTo((EntProtoId) "RMCTurretAlmayer"));
                    Assert.That(baseTargeting.LockedFriendlyFactions, Is.Empty);
                });
            }
            finally
            {
                entities.DeleteEntity(deployer);
                entities.DeleteEntity(baseDeployer);
            }
        });

        await pair.CleanReturnAsync();
    }
}
