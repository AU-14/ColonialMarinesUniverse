using System.Linq;
using System.Numerics;
using Content.Server._RMC14.Sentry;
using Content.Server.Humanoid.Systems;
using Content.Shared._RMC14.Commendations;
using Content.Shared._RMC14.Marines.ControlComputer;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Gibbing;
using Content.Shared.NPC.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaMarineEligibilityTest
{
    [TestCase("CMUMobYautja")]
    [TestCase("CMUMobYautjaBadBloodGrunt")]
    [TestCase("CMUMobYautjaBadBloodLeader")]
    [TestCase("CMUYautjaHunter")]
    [TestCase("CMMobHuman")]
    public async Task YautjaAreHostileToHumanSentriesAndCannotReceiveMedals(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var targetingSystem = entities.System<SentryTargetingSystem>();
            var iff = entities.System<GunIFFSystem>();
            var hunterCoords = map.GridCoords.Offset(new Vector2(2, 0));
            var hunter = prototype == "CMUYautjaHunter"
                ? entities.System<RandomHumanoidSystem>().SpawnRandomHumanoid(prototype, hunterCoords, string.Empty)
                : entities.SpawnEntity(prototype, hunterCoords);
            var marine = entities.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 2)));
            var sentry = entities.SpawnEntity("RMCSentryDropship", map.GridCoords);
            var computer = entities.SpawnEntity(null, map.GridCoords);

            try
            {
                // Cover adding Yautja to an existing human as well as all normal spawn paths.
                if (prototype == "CMMobHuman")
                    entities.EnsureComponent<YautjaComponent>(hunter);

                var targeting = entities.GetComponent<SentryTargetingComponent>(sentry);
                Assert.That(targetingSystem.TryApplyDefaultFaction(sentry, "GOVFOR"), Is.True);
                targetingSystem.ToggleHumanoid((sentry, targeting), true);
                targetingSystem.ApplyAllianceFactions(sentry, targeting, new[] { "AUColonist" });
                iff.SetUserFaction(marine, "GOVFOR");

                var control = entities.AddComponent<MarineControlComputerComponent>(computer);
                var player = server.PlayerMan.Sessions.Single();
                var playerId = player.UserId.UserId.ToString();
                entities.EventBus.RaiseLocalEvent(marine, new PlayerAttachedEvent(marine, player));
                entities.EventBus.RaiseLocalEvent(hunter, new PlayerAttachedEvent(hunter, player));

                var hunterGibbed = new BeingGibbedEvent([]);
                entities.EventBus.RaiseLocalEvent(hunter, ref hunterGibbed);
                var marineGibbed = new BeingGibbedEvent([]);
                entities.EventBus.RaiseLocalEvent(marine, ref marineGibbed);

                Assert.Multiple(() =>
                {
                    Assert.That(entities.GetComponent<NpcFactionMemberComponent>(hunter).Factions.Select(faction => faction.Id),
                        Is.EquivalentTo(new[] { "CMUYautja" }));
                    Assert.That(iff.IsInFaction(hunter, "FactionYautja"), Is.True);
                    Assert.That(targetingSystem.IsValidTarget((sentry, targeting), hunter), Is.True);
                    Assert.That(targetingSystem.GetNearbyIffHostiles((sentry, targeting), 7).ToArray(),
                        Does.Contain(hunter).And.Not.Contain(marine));
                    Assert.That(targetingSystem.IsValidTarget((sentry, targeting), marine), Is.False);
                    Assert.That(entities.HasComponent<CommendationReceiverComponent>(hunter), Is.False,
                        "Yautja must not enter the headset recommendation or direct medal recipient queries.");
                    Assert.That(control.GibbedMarines.Select(info => info.LastPlayerId),
                        Is.EquivalentTo(new[] { playerId }),
                        "Only the human should remain eligible for posthumous medals.");
                });
            }
            finally
            {
                entities.DeleteEntity(hunter);
                entities.DeleteEntity(marine);
                entities.DeleteEntity(sentry);
                entities.DeleteEntity(computer);
            }
        });

        await pair.CleanReturnAsync();
    }
}
