using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Threats;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-501: Bad Blood is on the ballot wherever Xenomorphs are, except the planets in <see cref="Excluded"/>.
/// Prototype-only, no map is loaded.
/// </summary>
[TestFixture]
public sealed class BadBloodPlanetThreatTest : GameTest
{
    private static readonly ProtoId<ThreatPrototype> XenoThreat = "XenoThreat";
    private static readonly ProtoId<ThreatPrototype> BadBloodThreat = "BadBloodClanThreat";

    // no sensor tower, so the Bad Blood objective chain cannot run there (maintainer, 2026-11-15)
    private static readonly HashSet<string> Excluded = new() { "AUPlanetFlight", "AuPlanetChances" };

    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task EveryXenoPlanetAllowsBadBlood()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            var xenoPlanets = new List<string>();
            var missing = new List<string>();
            var excludedSeen = new List<string>();
            var wronglyAllowed = new List<string>();

            foreach (var proto in SProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract ||
                    !proto.TryComp<RMCPlanetMapPrototypeComponent>(out var planet, factory))
                {
                    continue;
                }

                // AllowedThreats is read-only outside RMCPlanetSystem, so read it by iterating
                var allowsXeno = false;
                var allowsBadBlood = false;
                foreach (var threat in planet.AllowedThreats)
                {
                    allowsXeno |= threat == XenoThreat;
                    allowsBadBlood |= threat == BadBloodThreat;
                }

                if (Excluded.Contains(proto.ID))
                {
                    excludedSeen.Add(proto.ID);
                    if (allowsBadBlood)
                        wronglyAllowed.Add(proto.ID);
                    continue;
                }

                if (!allowsXeno)
                    continue;

                xenoPlanets.Add(proto.ID);
                if (!allowsBadBlood)
                    missing.Add(proto.ID);
            }

            Assert.That(xenoPlanets, Is.Not.Empty, "no planet allows XenoThreat; the test would pass vacuously");
            Assert.That(excludedSeen, Is.EquivalentTo(Excluded), "an excluded planet id no longer exists");
            Assert.That(wronglyAllowed, Is.Empty,
                $"excluded planets that allow {BadBloodThreat}: {string.Join(", ", wronglyAllowed)}");
            Assert.That(missing, Is.Empty,
                $"planets that allow {XenoThreat} but not {BadBloodThreat}: {string.Join(", ", missing)}");
        });
    }
}
