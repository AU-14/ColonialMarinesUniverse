using Content.Server.CMU14.Round;
using Content.Server.GameTicking.Presets;
using Content.Shared._RMC14.CCVar;
using Content.Shared.CMU14.Dropship.Fabricator;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class CMUDropshipFabricatorPresetPointsTest
{
    [TestCase("Insurgency", 7500)]
    [TestCase("ForceOnForce", 25000)]
    [TestCase("DistressSignal", 20000)]
    [TestCase("CMDistressSignal", 20000)]
    [TestCase("ColonyFall", null)]
    public async Task StartingPointsFollowSelectedPreset(string presetId, int? expected)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.ResolveDependency<IEntityManager>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var defaultPoints = server.ResolveDependency<IConfigurationManager>()
                .GetCVar(RMCCVars.RMCDropshipFabricatorStartingPoints);

            entities.System<AuRoundSystem>().SetPreset(prototypes.Index<GamePresetPrototype>(presetId));

            var ev = new CMUGetDropshipFabricatorStartingPointsEvent(defaultPoints);
            entities.EventBus.RaiseEvent(EventSource.Local, ref ev);

            Assert.That(ev.Points, Is.EqualTo(expected ?? defaultPoints));
        });

        await pair.CleanReturnAsync();
    }
}
