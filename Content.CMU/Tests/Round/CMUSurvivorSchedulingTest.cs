using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Threats;
using Content.Shared.GameTicking;
using Content.Tests;
using Moq;
using NUnit.Framework;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;

namespace Content.CMU.Tests.Round;

[TestFixture]
[NonParallelizable]
public sealed class CMUSurvivorSchedulingTest : ContentUnitTest
{
    private static readonly ProtoId<GamePresetPrototype> Preset = "DistressSignal";
    private IPrototypeManager _prototypes = default!;
    private AuRoundSystem _round = default!;
    private Dictionary<NetUserId, PlayerGameStatus> _statuses = default!;

    [OneTimeSetUp]
    public void InitializePrototypes()
    {
        IoCManager.Resolve<ISerializationManager>().Initialize();
        _prototypes = IoCManager.Resolve<IPrototypeManager>();
        _prototypes.Initialize();
        _prototypes.LoadString("""
            - type: gamePreset
              id: DistressSignal
            - type: threat
              id: CMUTestThreat
              roundstartspawns: CMUTestThreatSpawn
              threatratio: 0.1
              thirdpartyratio: 0.5
            - type: partySpawn
              id: CMUTestThreatSpawn
              leadersToSpawn:
                Queen: 1
              gruntsToSpawn:
                Larva: 19
            - type: thirdParty
              id: CMUTestLargeSurvivors
              partyspawn: CMUTestLargeSurvivorSpawn
              roundstart: true
              announceAsSurvivors: true
            - type: partySpawn
              id: CMUTestLargeSurvivorSpawn
              leadersToSpawn:
                Survivor: 10
            - type: thirdParty
              id: CMUTestSmallSurvivors
              partyspawn: CMUTestSmallSurvivorSpawn
              roundstart: true
              announceAsSurvivors: true
            - type: partySpawn
              id: CMUTestSmallSurvivorSpawn
              leadersToSpawn:
                Survivor: 4
            """);
        _prototypes.ResolveResults();
    }

    [SetUp]
    public void SetUpScheduler()
    {
        var players = new Mock<IPlayerManager>();
        players.SetupGet(manager => manager.PlayerCount).Returns(100);
        ICommonSession session = Mock.Of<ICommonSession>();
        players.Setup(manager => manager.TryGetSessionById(It.IsAny<NetUserId>(), out session)).Returns(true);

        var ticker = new GameTicker();
        SetField(ticker, "_playerManager", players.Object);
        typeof(GameTicker).GetProperty(nameof(GameTicker.LobbyEnabled))!.SetValue(ticker, true);
        _statuses = (Dictionary<NetUserId, PlayerGameStatus>) typeof(GameTicker)
            .GetField("_playerGameStatuses", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ticker)!;
        for (var i = 0; i < 100; i++)
            _statuses.Add(new NetUserId(Guid.NewGuid()), PlayerGameStatus.NotReadyToPlay);

        var systems = new Mock<IEntitySystemManager>();
        systems.Setup(manager => manager.GetEntitySystem<PlatoonSpawnRuleSystem>())
            .Returns(new PlatoonSpawnRuleSystem());
        var entities = new Mock<IEntityManager>();
        entities.SetupGet(manager => manager.EntitySysManager).Returns(systems.Object);
        var random = new Mock<IRobustRandom>();
        random.Setup(value => value.Next(It.IsAny<int>())).Returns(0);

        _round = new AuRoundSystem();
        SetField(_round, "_prototypeManager", _prototypes);
        SetField(_round, "_playerManager", players.Object);
        SetField(_round, "_gameTicker", ticker);
        SetField(_round, "_entityManager", entities.Object);
        SetField(_round, "_random", random.Object);
        _round.SetPreset(_prototypes.Index(Preset));
        var state = (AuRoundSelectionState) typeof(AuRoundSystem)
            .GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_round)!;
        var planet = new RMCPlanetMapPrototypeComponent();
        SetField(planet, nameof(planet.AllowedThreats), new List<ProtoId<ThreatPrototype>> { "CMUTestThreat" });
        SetField(planet, nameof(planet.ThirdParties), new List<ProtoId<ThirdPartyPrototype>>
        {
            "CMUTestLargeSurvivors", "CMUTestSmallSurvivors",
        });
        state.SetPlanet("CMUTestPlanet", planet);
    }

    [Test]
    public void SurvivorRosterUsesReadyPopulationAndCappedThreatBodies()
    {
        SetReadyPlayers(40);

        Assert.That(_round.TryLockDistressSignalThirdParties(out var survivors), Is.True);

        // 100 connected players and a 20-body template must not override the four planned threat bodies.
        Assert.That(survivors, Is.EqualTo(4));
        Assert.That(_round.SelectedThirdParties.Select(party => party.ID),
            Is.EqualTo(new[] { "CMUTestSmallSurvivors" }));
    }

    [Test]
    public void RoundStartRevalidationReplacesOversizedRosterAndClearsItWithoutReadyPlayers()
    {
        SetReadyPlayers(100);
        Assert.That(_round.TryLockDistressSignalThirdParties(out var announced), Is.True);
        Assert.That(announced, Is.EqualTo(10));

        SetReadyPlayers(40);
        Assert.That(_round.TryLockDistressSignalThirdParties(out var survivors, revalidate: true), Is.True);
        Assert.That(survivors, Is.EqualTo(4));
        Assert.That(_round.SelectedThirdParties.Select(party => party.ID),
            Is.EqualTo(new[] { "CMUTestSmallSurvivors" }));

        SetReadyPlayers(0);
        Assert.That(_round.TryLockDistressSignalThirdParties(out survivors, revalidate: true), Is.True);
        Assert.That(survivors, Is.Zero);
        Assert.That(_round.SelectedThirdParties, Is.Empty);
    }

    [Test]
    public void RoundStartRevalidationFillsEmptyRosterButPreservesAGroupThatStillFits()
    {
        SetReadyPlayers(0);
        Assert.That(_round.TryLockDistressSignalThirdParties(out var survivors), Is.True);
        Assert.That(survivors, Is.Zero);

        SetReadyPlayers(40);
        Assert.That(_round.TryLockDistressSignalThirdParties(out survivors, revalidate: true), Is.True);
        Assert.That(survivors, Is.EqualTo(4));
        var announcedParty = _round.SelectedThirdParties.Single();

        SetReadyPlayers(100);
        Assert.That(_round.TryLockDistressSignalThirdParties(out survivors, revalidate: true), Is.True);
        Assert.That(survivors, Is.EqualTo(4));
        Assert.That(_round.SelectedThirdParties.Single(), Is.SameAs(announcedParty));
    }

    private void SetReadyPlayers(int count)
    {
        var index = 0;
        foreach (var user in _statuses.Keys.ToArray())
            _statuses[user] = index++ < count ? PlayerGameStatus.ReadyToPlay : PlayerGameStatus.NotReadyToPlay;
    }

    private static void SetField(object target, string name, object value)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
            .SetValue(target, value);
}
