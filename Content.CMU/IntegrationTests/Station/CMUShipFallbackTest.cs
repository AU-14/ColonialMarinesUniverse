using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking.Presets;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Station;

/// <summary>
///     A mode that keeps GOVFOR shipside still loads their ship when no ship vote ran, like a force-started round.
/// </summary>
[TestFixture]
public sealed class CMUShipFallbackTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task HopesRetreatFallsBackToTheBush()
    {
        await Server.WaitAssertion(() =>
        {
            var round = SEntMan.System<AuRoundSystem>();
            var protos = Server.ResolveDependency<IPrototypeManager>();
            Assert.That(round.SetPlanet("CMUPlanetHopesRetreat"), Is.True);
            round.SetGovforShip(string.Empty);
            round.SetOpforShip(string.Empty);

            Assert.That(round.ResolveGovforShip(protos.Index<GamePresetPrototype>("Insurgency")), Is.EqualTo("USSBushRedux"));
            Assert.That(round.ResolveGovforShip(protos.Index<GamePresetPrototype>("DistressSignal")), Is.EqualTo("USSBushRedux"));
            Assert.That(round.ResolveOpforShip(protos.Index<GamePresetPrototype>("Insurgency")), Is.Null, "Insurgency's OPFOR is the CLF, on the planet.");
            Assert.That(round.ResolveGovforShip(protos.Index<GamePresetPrototype>("ColonyFall")), Is.Null, "Colony Fall has no GOVFOR.");
        });
    }
}
