using System.Reflection;
using Content.Server.CMU14.Round;
using Content.Shared._RMC14.Rules;
using Content.Shared.Maps;
using Content.Tests;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.CMU.Tests.Round;

[TestFixture]
public sealed class CMUPlanetPopulationLockTest : ContentUnitTest
{
    private static readonly ProtoId<GameMapPrototype> LowpopMap = "CMUTestLowpopMap";
    private static readonly ProtoId<GameMapPrototype> OpenMap = "CMUTestOpenMap";

    private GameMapPrototype _lowpopMap = default!;
    private GameMapPrototype _openMap = default!;

    [OneTimeSetUp]
    public void InitializePrototypes()
    {
        IoCManager.Resolve<ISerializationManager>().Initialize();
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        prototypes.Initialize();
        prototypes.LoadString("""
            - type: gameMap
              id: CMUTestLowpopMap
              mapName: Lowpop
              mapPath: /Maps/Test/empty.yml
              minPlayers: 0
              maxPlayers: 80
              stations: {}
            - type: gameMap
              id: CMUTestOpenMap
              mapName: Open
              mapPath: /Maps/Test/empty.yml
              minPlayers: 0
              stations: {}
            """);
        prototypes.ResolveResults();
        _lowpopMap = prototypes.Index(LowpopMap);
        _openMap = prototypes.Index(OpenMap);
    }

    [TestCase(80, true)]
    [TestCase(81, false)]
    public void GameMapCapLocksThePlanetVote(int players, bool votable)
    {
        Assert.That(AuRoundSelectionRules.IsPlanetInPopulationRange(new RMCPlanetMapPrototypeComponent(), _lowpopMap, players),
            Is.EqualTo(votable));
    }

    [Test]
    public void UncappedGameMapStaysVotableAtAnyPopulation()
    {
        Assert.That(AuRoundSelectionRules.IsPlanetInPopulationRange(new RMCPlanetMapPrototypeComponent(), _openMap, 500),
            Is.True);
    }

    [Test]
    public void PlanetLimitsStillApply()
    {
        var planet = new RMCPlanetMapPrototypeComponent();
        typeof(RMCPlanetMapPrototypeComponent).GetField(nameof(planet.MinPlayers), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(planet, 30);

        Assert.Multiple(() =>
        {
            Assert.That(AuRoundSelectionRules.IsPlanetInPopulationRange(planet, _openMap, 29), Is.False);
            Assert.That(AuRoundSelectionRules.IsPlanetInPopulationRange(planet, _openMap, 30), Is.True);
        });
    }
}
