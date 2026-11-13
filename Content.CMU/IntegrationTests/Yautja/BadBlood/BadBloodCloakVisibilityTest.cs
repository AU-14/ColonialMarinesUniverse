#pragma warning disable RA0002 // tests read component data directly
using Content.Shared._RMC14.Armor.ThermalCloak;
using Content.Shared.CMU14.Yautja;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ECS-201: the scavenged cloak hides as well as the marine scout cloak, and clearly worse than the honourable one
[TestFixture]
public sealed class BadBloodCloakVisibilityTest
{
    private const string HunterBracer = "CMUYautjaBracer";
    private const string BadBloodBracer = "CMUYautjaBadBloodBracer";
    private const string LeaderBracer = "CMUYautjaBadBloodLeaderBracer";
    private const string ScoutCloak = "RMCBackpackScout";

    [Test]
    public async Task BadBloodCloakMatchesTheScoutCloakAndTrailsTheHonourableOne()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var protos = server.ProtoMan;
            var factory = server.EntMan.ComponentFactory;

            T Comp<T>(string id) where T : IComponent, new()
            {
                Assert.That(protos.Index<EntityPrototype>(id).TryComp<T>(out var comp, factory), Is.True,
                    $"{id} has no {typeof(T).Name}");
                return comp!;
            }

            var hunter = Comp<YautjaBracerComponent>(HunterBracer);
            var scout = Comp<ThermalCloakComponent>(ScoutCloak);

            Assert.Multiple(() =>
            {
                // the honourable cloak is out of scope and must stay where it was
                Assert.That(hunter.CloakOpacity, Is.EqualTo(0f).Within(0.0001f)); // devices.yml: fully hidden standing
                Assert.That(hunter.CloakMovingOpacity, Is.EqualTo(0.035f).Within(0.0001f));
                Assert.That(hunter.CloakHideNightVision, Is.True);

                // the scout cloak this is measured against
                Assert.That(scout.Opacity, Is.EqualTo(0.025f).Within(0.0001f));
                Assert.That(scout.MovingOpacity, Is.EqualTo(0.25f).Within(0.0001f));
            });

            foreach (var id in new[] { BadBloodBracer, LeaderBracer })
            {
                var badBlood = Comp<YautjaBracerComponent>(id);
                Assert.Multiple(() =>
                {
                    Assert.That(badBlood.CloakOpacity, Is.GreaterThanOrEqualTo(scout.Opacity), $"{id}: standing");
                    Assert.That(badBlood.CloakMovingOpacity, Is.GreaterThanOrEqualTo(scout.MovingOpacity), $"{id}: moving");
                    Assert.That(badBlood.CloakOpacity, Is.GreaterThan(hunter.CloakOpacity), $"{id}: standing");
                    Assert.That(badBlood.CloakMovingOpacity, Is.GreaterThan(hunter.CloakMovingOpacity), $"{id}: moving");

                    // the scout cloak hides from night vision too, so the Bad Blood one is no more exposed there
                    Assert.That(badBlood.CloakHideNightVision, Is.EqualTo(scout.HideNightVision), $"{id}: night vision");
                });
            }
        });

        await pair.CleanReturnAsync();
    }
}
