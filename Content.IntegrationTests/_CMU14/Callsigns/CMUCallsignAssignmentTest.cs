using System.Diagnostics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Callsigns;
using Content.Shared.CMU14.Callsigns;

namespace Content.IntegrationTests.CMU14.Callsigns;

[TestFixture]
public sealed class CMUCallsignAssignmentTest : GameTest
{
    [Test]
    public async Task NumberSelectionPreservesElementPrecedenceAndExcludesTheMember()
    {
        await Server.WaitAssertion(() =>
        {
            var entities = new List<EntityUid>();
            try
            {
                var squadA = Spawn();
                var squadB = Spawn();
                var member = Callsign("govfor", squadA, null, null, "1-2");
                Callsign("govfor", squadA, null, null, "1-1");
                Callsign("govfor", squadA, null, null, "1-3");
                Callsign("opfor", squadA, null, null, "1-2");
                Callsign("GOVFOR", squadA, null, null, "1-2");
                Callsign("govfor", squadB, null, null, "1-2");
                Callsign("govfor", squadA, null, "AIR", "1-2");
                Callsign("govfor", squadA, "EAGLE", "MEDICAL", "1-2");
                var system = SEntMan.System<AU14CallsignSystem>();
                Assert.That(system.NextFreeNumber("govfor", squadA, null, null, 1, member), Is.EqualTo("1-2"));
                Assert.That(system.NextFreeNumber("govfor", squadA, null, null, 2, member), Is.EqualTo("2-1"));

                // Categories span squads; a group spans both squads and categories.
                Callsign("govfor", squadB, null, "air", "1-1");
                Callsign("govfor", squadB, "eagle", "AIR", "1-1");
                Callsign("govfor", squadB, "HAWK", "AIR", "1-3");
                Assert.That(system.NextFreeNumber("govfor", squadA, null, "aIr", 1, member), Is.EqualTo("1-3"));
                Assert.That(system.NextFreeNumber("govfor", squadB, "Eagle", "INTEL", 1, member), Is.EqualTo("1-3"));
            }
            finally
            {
                foreach (var uid in entities)
                    SEntMan.DeleteEntity(uid);
            }

            EntityUid Spawn()
            {
                var uid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                entities.Add(uid);
                return uid;
            }

            EntityUid Callsign(string faction, EntityUid squad, string group, string category, string suffix)
            {
                var uid = Spawn();
                var callsign = SEntMan.AddComponent<AU14CallsignComponent>(uid);
                callsign.Faction = faction;
                callsign.Squad = squad;
                callsign.Group = group;
                callsign.Category = category;
                callsign.Suffix = suffix;
                return uid;
            }
        });
    }

    [Test]
    public async Task NamedSuffixSelectionIsCaseInsensitiveAndRechecksAfterMembershipChanges()
    {
        await Server.WaitAssertion(() =>
        {
            var entities = new List<EntityUid>();
            try
            {
                var member = Add("ROMEO 3");
                var leader = Add("romeo");
                Add("Romeo 2");
                Add("ROMEO 4");
                var system = SEntMan.System<AU14CallsignSystem>();
                Assert.That(system.MakeUniqueSuffix("govfor", null, null, null, "ROMEO", member), Is.EqualTo("ROMEO 3"));

                // No persistent occupancy cache: moving a member immediately frees its suffix.
                SEntMan.GetComponent<AU14CallsignComponent>(leader).Group = "EAGLE";
                Assert.That(system.MakeUniqueSuffix("govfor", null, null, null, "ROMEO", member), Is.EqualTo("ROMEO"));
                SEntMan.GetComponent<AU14CallsignComponent>(leader).Group = null;
                Assert.That(system.MakeUniqueSuffix("govfor", null, null, null, "ROMEO", member), Is.EqualTo("ROMEO 3"));
            }
            finally
            {
                foreach (var uid in entities)
                    SEntMan.DeleteEntity(uid);
            }

            EntityUid Add(string suffix)
            {
                var uid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                entities.Add(uid);
                var callsign = SEntMan.AddComponent<AU14CallsignComponent>(uid);
                callsign.Faction = "govfor";
                callsign.Suffix = suffix;
                return uid;
            }
        });
    }

    [Test]
    public async Task CrowdedElementKeepsTheFirstFreeNumberAfterRemoval()
    {
        await Server.WaitAssertion(() =>
        {
            var entities = new List<EntityUid>();
            try
            {
                for (var n = 1; n <= 500; n++)
                {
                    var uid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                    entities.Add(uid);
                    var callsign = SEntMan.AddComponent<AU14CallsignComponent>(uid);
                    callsign.Faction = "govfor";
                    callsign.Suffix = $"1-{n}";
                }

                var system = SEntMan.System<AU14CallsignSystem>();
                Assert.That(system.NextFreeNumber("govfor", null, null, null, 1, EntityUid.Invalid), Is.EqualTo("1-501"));
                SEntMan.RemoveComponent<AU14CallsignComponent>(entities[236]);
                Assert.That(system.NextFreeNumber("govfor", null, null, null, 1, EntityUid.Invalid), Is.EqualTo("1-237"));
            }
            finally
            {
                foreach (var uid in entities)
                    SEntMan.DeleteEntity(uid);
            }
        });
    }

    [TestCase(-3)]
    [TestCase(0)]
    [TestCase(1)]
    public async Task NumericSelectionRequiresCanonicalSuffixesAndRechecksEveryAssignment(int fireteam)
    {
        await Server.WaitAssertion(() =>
        {
            var entities = new List<EntityUid>();
            try
            {
                var prefix = $"{fireteam}-";
                var member = Add(prefix + "1");
                Add(prefix + "2");

                foreach (var suffix in new[]
                         {
                             "01", "+1", "-1", " 1", "1 ", "1\0", "\u0661",
                             "0", "2147483648", "1.0", "1e0",
                         })
                {
                    Add(prefix + suffix);
                }

                Add($"{fireteam + 1}-1");
                var system = SEntMan.System<AU14CallsignSystem>();

                Assert.That(
                    system.NextFreeNumber("govfor", null, null, null, fireteam, member),
                    Is.EqualTo(prefix + "1"),
                    "noncanonical suffixes and the excluded member do not occupy number 1");

                Assert.That(
                    system.NextFreeNumber("govfor", null, null, null, fireteam, EntityUid.Invalid),
                    Is.EqualTo(prefix + "3"),
                    "including the member occupies its canonical number");

                SEntMan.GetComponent<AU14CallsignComponent>(member).Suffix = prefix + "4";
                Assert.That(
                    system.NextFreeNumber("govfor", null, null, null, fireteam, EntityUid.Invalid),
                    Is.EqualTo(prefix + "1"),
                    "changing a suffix frees its old number immediately");

                var replacement = Add(prefix + "1");
                Assert.That(
                    system.NextFreeNumber("govfor", null, null, null, fireteam, EntityUid.Invalid),
                    Is.EqualTo(prefix + "3"));

                SEntMan.RemoveComponent<AU14CallsignComponent>(replacement);
                Assert.That(
                    system.NextFreeNumber("govfor", null, null, null, fireteam, EntityUid.Invalid),
                    Is.EqualTo(prefix + "1"),
                    "removed components leave no stale occupancy");
            }
            finally
            {
                foreach (var uid in entities)
                    SEntMan.DeleteEntity(uid);
            }

            EntityUid Add(string suffix)
            {
                var uid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                entities.Add(uid);
                var callsign = SEntMan.AddComponent<AU14CallsignComponent>(uid);
                callsign.Faction = "govfor";
                callsign.Suffix = suffix;
                return uid;
            }
        });
    }

    [Test, Explicit]
    public async Task MeasureCrowdedElementAssignments()
    {
        await Server.WaitAssertion(() =>
        {
            const int population = 500;
            const int iterations = 100;
            var entities = new List<EntityUid>();
            try
            {
                for (var n = 1; n <= population; n++)
                {
                    var uid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                    entities.Add(uid);
                    var callsign = SEntMan.AddComponent<AU14CallsignComponent>(uid);
                    callsign.Faction = "cmu-benchmark";
                    callsign.Suffix = $"1-{n}";
                }

                var system = SEntMan.System<AU14CallsignSystem>();
                Func<string> before = OriginalSelection;
                Func<string> after = () => system.NextFreeNumber(
                    "cmu-benchmark", null, null, null, 1, EntityUid.Invalid);
                for (var n = 0; n < 10; n++)
                {
                    Assert.That(before(), Is.EqualTo("1-501"));
                    Assert.That(after(), Is.EqualTo("1-501"));
                }

                var baseline = Measure(before);
                var optimized = Measure(after);
                TestContext.Progress.WriteLine(
                    $"CMU callsigns: population={population}, assignments={iterations}, " +
                    $"before={baseline.Milliseconds:F3} ms/{baseline.Bytes} bytes, " +
                    $"after={optimized.Milliseconds:F3} ms/{optimized.Bytes} bytes");
            }
            finally
            {
                foreach (var uid in entities)
                    SEntMan.DeleteEntity(uid);
            }

            (double Milliseconds, long Bytes) Measure(Func<string> selection)
            {
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                var started = Stopwatch.GetTimestamp();
                for (var n = 0; n < iterations; n++)
                    _ = selection();
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                return (elapsed, GC.GetAllocatedBytesForCurrentThread() - allocated);
            }

            // Pre-change algorithm, restricted to the one populated command element.
            string OriginalSelection()
            {
                for (var n = 1;; n++)
                {
                    var candidate = $"1-{n}";
                    var taken = false;
                    var query = SEntMan.EntityQueryEnumerator<AU14CallsignComponent>();
                    while (query.MoveNext(out _, out var callsign))
                    {
                        if (callsign.Faction != "cmu-benchmark" || callsign.Squad != null ||
                            callsign.Group != null || callsign.Category != null ||
                            !string.Equals(callsign.Suffix, candidate, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        taken = true;
                        break;
                    }

                    if (!taken)
                        return candidate;
                }
            }
        });
    }
}
