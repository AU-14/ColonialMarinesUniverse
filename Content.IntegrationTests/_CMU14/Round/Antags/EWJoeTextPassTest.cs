using System.Linq;
using System.Reflection;
using Content.Client.CMU14.Round.Antags.EarlyWarning;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Chat;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Radio;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// Text passes A and B: the machine channel's key can be typed, THE LIGHT throbs, every line the victims
/// and the Joe are sent exists, THE LIGHT tells its victims what they hear as the envelope fills, a drowning
/// is marked for its whole hold, and the Joe's status readout draws its bars.
/// </summary>
[TestFixture]
public sealed class EWJoeTextPassTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";

    [Test]
    public async Task MachineChannelKeyIsTypableAndOnlyItsOwn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var protos = server.ResolveDependency<IPrototypeManager>();
            var machine = protos.Index<RadioChannelPrototype>("CMUGrandMachine");
            var prefixes = typeof(SharedChatSystem).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(char))
                .Select(f => (char) f.GetRawConstantValue()!)
                .ToHashSet();

            Assert.Multiple(() =>
            {
                Assert.That(machine.KeyCode, Is.EqualTo('#'));
                Assert.That(machine.KeyCode, Is.InRange(' ', '~'), "a printable key on a standard keyboard");
                Assert.That(prefixes, Does.Not.Contain(machine.KeyCode), "not a chat prefix");
                Assert.That(protos.EnumeratePrototypes<RadioChannelPrototype>()
                        .Where(c => c.ID != machine.ID && char.ToLowerInvariant(c.KeyCode) == machine.KeyCode)
                        .Select(c => c.ID), Is.Empty,
                    "no other channel shares the key (a shared key resolves to the headset first and would leak)");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryVictimLineAndNoticeExists()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var loc = server.ResolveDependency<ILocalizationManager>();
            var sets = typeof(EWJoeMessageSystem).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(EWJoeLines))
                .Select(f => (EWJoeLines) f.GetValue(null)!)
                .ToList();
            // Test of Faith 3, Judgement 4, Drown 2, THE LIGHT 4
            Assert.That(sets, Has.Count.EqualTo(13), "the victim line sets");

            var missing = new List<string>();
            foreach (var set in sets)
            {
                for (var i = 0; i < set.Count; i++)
                {
                    if (!loc.HasString($"{set.Prefix}-{i}"))
                        missing.Add($"{set.Prefix}-{i}");
                }

                if (loc.HasString($"{set.Prefix}-{set.Count}"))
                    missing.Add($"{set.Prefix}-{set.Count} exists but is never picked");
            }

            foreach (var key in new[]
                     {
                         "cmu-ew-joe-drown-victim-start", "cmu-ew-joe-drown-victim-end", "cmu-ew-joe-notice-prefix",
                         "cmu-ew-joe-level-new-abilities", "cmu-ew-joe-level-down", "cmu-ew-joe-level-gains-3",
                         "cmu-ew-joe-level-gains-5", "cmu-ew-joe-status-link", "cmu-ew-joe-status-directive",
                     })
            {
                if (!loc.HasString(key))
                    missing.Add(key);
            }

            Assert.That(missing, Is.Empty);
            Assert.That(Loc.GetString("cmu-ew-joe-melding-announcement"),
                Is.EqualTo("Alert: Aggressive Synthetics Detected. Purge Failure. Power Surge Imminent. Shelter In Place."));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TheLightRipplesInOrange()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            entities.System<EWJoeRuleSystem>().ApplyLevel((joe, comp), 5);

            Color color = entities.GetComponent<PointLightComponent>(joe).Color;
            Assert.That(entities.TryGetComponent(joe, out EWJoeLightPulseComponent? pulse), "the light ripples on every client");
            Assert.Multiple(() =>
            {
                Assert.That(color.ToHex(), Is.EqualTo(Color.FromHex("#ff8a00").ToHex()), "it rests at its old orange");
                foreach (var shade in new[] { color, pulse!.Deep, pulse.Bright })
                {
                    Assert.That(shade.R, Is.EqualTo(1f).Within(0.01f), "orange: full red");
                    Assert.That(shade.G, Is.InRange(0.35f, 0.7f), "orange: some green, not red or yellow");
                    Assert.That(shade.B, Is.LessThan(0.25f), "orange: little blue");
                }
                Assert.That(pulse.MaxEnergy, Is.GreaterThan(pulse.MinEnergy));
                Assert.That(pulse.MaxEnergy, Is.LessThanOrEqualTo(3f), "a glow, not a flash");
            });
        });

        // the ripple: smooth (no beat or spike), and it really swells and ebbs
        var min = 1f;
        var max = 0f;
        var steepest = 0f;
        var previous = EWJoeLightPulseSystem.Ripple(0f, 2.6f);
        for (var time = 0.02f; time < 30f; time += 0.02f)
        {
            var value = EWJoeLightPulseSystem.Ripple(time, 2.6f);
            steepest = MathF.Max(steepest, MathF.Abs(value - previous) / 0.02f);
            min = MathF.Min(min, value);
            max = MathF.Max(max, value);
            previous = value;
        }

        Assert.Multiple(() =>
        {
            Assert.That(min, Is.GreaterThanOrEqualTo(0f));
            Assert.That(max, Is.LessThanOrEqualTo(1f));
            Assert.That(max - min, Is.GreaterThan(0.7f), "the swell is visible");
            // the old heartbeat rose from rest to full in 0.16 s (over 6 a second)
            Assert.That(steepest, Is.LessThan(2.5f), "no flash: it never changes faster than a slow wave");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TheLightTellsItsVictimEachStageOnce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid viewer = default;
        await server.WaitAssertion(() =>
        {
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            entities.EnsureComponent<EWJoeLightComponent>(joe);
            viewer = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, 2));
            EWJoeTestHelpers.Face(entities, viewer, joe);
        });

        // the envelope fills in 4 s: a third by ~1.33 s, two thirds by ~2.67 s
        await pair.RunTicksSync((int) Math.Ceiling(2.1 / timing.TickPeriod.TotalSeconds));
        await server.WaitAssertion(() =>
        {
            var envelope = entities.GetComponent<EWJoeEnvelopedComponent>(viewer);
            Assert.That(envelope.Stage, Is.EqualTo(1), $"hearing at {envelope.Progress:0.00}");
        });

        await pair.RunTicksSync((int) Math.Ceiling(1.3 / timing.TickPeriod.TotalSeconds));
        await server.WaitAssertion(() =>
        {
            var envelope = entities.GetComponent<EWJoeEnvelopedComponent>(viewer);
            Assert.That(envelope.Stage, Is.EqualTo(2), $"wondering at {envelope.Progress:0.00}");
        });

        await pair.RunTicksSync((int) Math.Ceiling(1.4 / timing.TickPeriod.TotalSeconds));
        await server.WaitAssertion(() =>
        {
            var envelope = entities.GetComponent<EWJoeEnvelopedComponent>(viewer);
            Assert.That(envelope.ImmuneUntil, Is.GreaterThan(timing.CurTime), "the envelope landed");
            Assert.That(envelope.Stage, Is.Zero, "and the stages start over");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ADrowningIsMarkedForItsWholeHold()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, victim = default;
        await server.WaitAssertion(() =>
        {
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            victim = entities.SpawnEntity(Human, map.GridCoords);
        });

        Assert.That(await EWJoeTestHelpers.AggressiveGrab(pair, joe, victim), "aggressive grab");

        await server.WaitAssertion(() =>
        {
            var ev = new EWJoeDrownActionEvent { Performer = joe, Target = victim };
            entities.EventBus.RaiseLocalEvent(joe, ev);
            Assert.That(ev.Handled, "the hold starts");
            Assert.That(entities.TryGetComponent(victim, out EWJoeDrowningComponent? drowning) && drowning.Joe == joe,
                "the victim is marked for the hold");
        });

        await pair.RunTicksSync((int) Math.Ceiling(4.5 / timing.TickPeriod.TotalSeconds));
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<EWJoeDrowningComponent>(victim).Beat, Is.GreaterThanOrEqualTo(3),
                "the airway lines keep coming during the hold");
        });

        await pair.RunTicksSync((int) Math.Ceiling(4.5 / timing.TickPeriod.TotalSeconds));
        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<EWJoeDrownedComponent>(victim), "the hold finished");
                Assert.That(entities.HasComponent<EWJoeDrowningComponent>(victim), Is.False, "and its marker is gone");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void FlashVictimsAreToldWhatTheStunDid()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EWJoeAbilitySystem.VictimLines(false, false, false), Is.EqualTo(EWJoeMessageSystem.FaithJolt), "empty-handed: the jolt");
            Assert.That(EWJoeAbilitySystem.VictimLines(false, true, false), Is.EqualTo(EWJoeMessageSystem.FaithFumble), "held something: the fumble");
            Assert.That(EWJoeAbilitySystem.VictimLines(true, false, false), Is.EqualTo(EWJoeMessageSystem.JudgementSenses));
            Assert.That(EWJoeAbilitySystem.VictimLines(true, true, false), Is.EqualTo(EWJoeMessageSystem.JudgementFumble));
            Assert.That(EWJoeAbilitySystem.VictimLines(true, true, true), Is.EqualTo(EWJoeMessageSystem.JudgementGun), "a gun left the hand");
        });
    }

    [Test]
    public async Task AnnouncementsAndReadoutLinesFormatAsWritten()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var protos = server.ResolveDependency<IPrototypeManager>();
            Assert.Multiple(() =>
            {
                Assert.That(Loc.GetString("cmu-ew-joe-console-assimilated-announcement", ("console", "BUDGET CONSOLE"), ("area", "COLONY OFFICE")),
                    Is.EqualTo("Alert: BUDGET CONSOLE In COLONY OFFICE Unresponsive. Data Corrupted. Assistance Required."));
                Assert.That(Loc.GetString("cmu-ew-joe-status-credit", ("bar", "#----"), ("earned", 1), ("required", 2)),
                    Does.Contain("[#----] 1/2"));
                Assert.That(protos.Index<EntityPrototype>("CMUEWJoeKillPersonObjective").Description,
                    Is.EqualTo("The signal has chosen. Make sure they stop."), "the kill objective's text comes from the locale file");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LinesFormatWithTheArgumentsTheirCallersPass()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var someone = server.EntMan.SpawnEntity(Human, map.GridCoords);
            var calls = new (string Key, (string, object)[] Args)[]
            {
                ("cmu-ew-joe-assimilate-already", new (string, object)[] { ("console", someone) }),
                ("cmu-ew-joe-assimilate-invalid", new (string, object)[] { ("console", someone) }),
                ("cmu-ew-joe-assimilate-not-called", new (string, object)[] { ("console", someone) }),
                ("cmu-ew-joe-assimilate-done", new (string, object)[] { ("console", someone) }),
                ("cmu-ew-joe-console-locked", new (string, object)[] { ("console", someone) }),
                ("cmu-ew-joe-console-lost", new (string, object)[] { ("console", someone) }),
                ("cmu-ew-joe-test-of-faith-not-facing", new (string, object)[] { ("target", someone) }),
                ("cmu-ew-joe-test-of-faith-unseen", new (string, object)[] { ("target", someone) }),
                ("cmu-ew-joe-drown-start", new (string, object)[] { ("target", someone) }),
                ("cmu-ew-joe-light-enveloped", new (string, object)[] { ("target", someone) }),
                ("cmu-ew-joe-scour-taken", new (string, object)[] { ("item", someone) }),
                ("cmu-ew-joe-scour-kind-expired", new (string, object)[] { ("kind", "radio") }),
                ("cmu-ew-joe-meld-complete-master", new (string, object)[] { ("thrall", someone) }),
                ("cmu-ew-joe-thrall-lost", new (string, object)[] { ("thrall", someone) }),
                ("cmu-ew-joe-level-up", new (string, object)[] { ("levelName", "CHORUS") }),
                ("cmu-ew-joe-level-down", new (string, object)[] { ("levelName", "MURMUR") }),
                ("cmu-ew-joe-level-new-abilities", new (string, object)[] { ("abilities", "Drown") }),
                ("cmu-ew-joe-consecrate-unseen", Array.Empty<(string, object)>()),
                ("cmu-ew-joe-consecrate-done", new (string, object)[] { ("count", 2) }),
                ("cmu-ew-joe-unwind-done", new (string, object)[] { ("guns", 3), ("sentries", 1) }),
                ("cmu-ew-joe-choke-start", new (string, object)[] { ("target", someone) }),
                ("cmu-ew-joe-slam-done", new (string, object)[] { ("target", someone), ("obstacle", someone) }),
                ("cmu-ew-joe-throw-done", new (string, object)[] { ("target", someone) }),
                ("cmu-ew-joe-coolant-done", new (string, object)[] { ("amount", 12) }),
                ("cmu-ew-joe-judgement-hit", new (string, object)[] { ("count", 2) }),
                ("cmu-ew-joe-scour-done", new (string, object)[] { ("item", "armor"), ("kind", "body armor") }),
                ("cmu-ew-joe-scour-done-permanent", new (string, object)[] { ("item", "pyro armor"), ("kind", "pyro armor") }),
                ("cmu-ew-joe-saturate-done", new (string, object)[] { ("count", 2) }),
            };

            var broken = new List<string>();
            foreach (var (key, args) in calls)
            {
                var text = Loc.GetString(key, args);
                if (text.Contains('{') || text == key)
                    broken.Add($"{key}: {text}");
            }

            Assert.That(broken, Is.Empty, "every line formats with exactly what its caller passes");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void TheStatusReadoutDrawsItsBars()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EWJoeStatusUISystem.Bar(0f), Is.EqualTo("----------"));
            Assert.That(EWJoeStatusUISystem.Bar(0.5f), Is.EqualTo("#####-----"));
            Assert.That(EWJoeStatusUISystem.Bar(1.4f), Is.EqualTo("##########"), "clamped");
        });
    }
}
