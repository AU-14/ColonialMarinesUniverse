using System.Numerics;
using System.Linq;
using Content.Client.UserInterface.Systems.Chat;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Server._CMU14.Yautja;
using Content.Client.Popups;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaHuntdataCleanupTest
{
    [TestCase(YautjaMarkKind.Honored, "spared civilians")]
    [TestCase(YautjaMarkKind.Dishonored, "stole hunter gear")]
    [TestCase(YautjaMarkKind.GearCarrier, null)]
    public async Task OneOwnerMarksCannotBeOverwrittenLikeCmss13Huntdata(YautjaMarkKind kind, string? reason)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var marks = entMan.System<YautjaMarkSystem>();

            var firstHunter = SpawnHunterWithBracer(entMan, map.GridCoords, out var firstBracer);
            var secondHunter = SpawnHunterWithBracer(entMan, map.GridCoords.Offset(new Vector2(1, 0)), out var secondBracer);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));

            try
            {
                Assert.That(
                    marks.TryMark((firstBracer, entMan.GetComponent<YautjaBracerComponent>(firstBracer)), firstHunter, target, kind, reason),
                    Is.True);

                Assert.That(
                    marks.TryMark((secondBracer, entMan.GetComponent<YautjaBracerComponent>(secondBracer)), secondHunter, target, kind, "second hunter"),
                    Is.False,
                    $"CMSS13 huntdata only has one {kind} owner field and rejects a second hunter instead of overwriting it.");

                Assert.That(marks.IsMarkedBy(target, kind, firstHunter), Is.True,
                    "The original hunter_data owner link must remain intact.");
                Assert.That(marks.IsMarkedBy(target, kind, secondHunter), Is.False,
                    "A rejected repeat mark must not replace the original hunter_data owner link.");
            }
            finally
            {
                DeleteAll(entMan, firstHunter, secondHunter, target, firstBracer, secondBracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GenericMarksClearOnRoundRestartLikeCmss13CleanData()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var marks = entMan.System<YautjaMarkSystem>();

            var hunter = SpawnHunterWithBracer(entMan, map.GridCoords, out var bracer);
            var prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var honored = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var dishonored = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));
            var gearCarrier = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4, 0)));
            var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

            try
            {
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, prey, YautjaMarkKind.Prey, null), Is.True);
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, honored, YautjaMarkKind.Honored, "worthy"), Is.True);
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, dishonored, YautjaMarkKind.Dishonored, "thief"), Is.True);
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, gearCarrier, YautjaMarkKind.GearCarrier, null), Is.True);

                entMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

                Assert.That(marks.IsMarkedBy(prey, YautjaMarkKind.Prey, hunter), Is.False,
                    "CMSS13 huntdata.clean_data() clears hunted/prey links during round-scoped cleanup.");
                Assert.That(marks.IsMarkedBy(honored, YautjaMarkKind.Honored, hunter), Is.False,
                    "CMSS13 huntdata.clean_data() clears honored links during round-scoped cleanup.");
                Assert.That(marks.IsMarkedBy(dishonored, YautjaMarkKind.Dishonored, hunter), Is.False,
                    "CMSS13 huntdata.clean_data() clears dishonored links during round-scoped cleanup.");
                Assert.That(marks.IsMarkedBy(gearCarrier, YautjaMarkKind.GearCarrier, hunter), Is.False,
                    "CMSS13 huntdata.clean_data() clears gear-carrier links during round-scoped cleanup.");
            }
            finally
            {
                DeleteAll(entMan, hunter, prey, honored, dishonored, gearCarrier, bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MarkForHuntTargetsLivingHumansAndXenosButNotYautjaLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var marks = entMan.System<YautjaMarkSystem>();
            var mobState = entMan.System<MobStateSystem>();

            var hunter = SpawnHunterWithBracer(entMan, map.GridCoords, out var bracer);
            var humanPrey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var xenoPrey = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(2, 0)));
            var yautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(3, 0)));
            var deadHuman = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4, 0)));
            var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

            try
            {
                mobState.ChangeMobState(deadHuman, MobState.Dead);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        marks.TryMark((bracer, bracerComp), hunter, humanPrey, YautjaMarkKind.Prey, null),
                        Is.True,
                        "CMSS13 mark_for_hunt() includes living ishuman_strict() prey.");
                    Assert.That(marks.TryClearMark(humanPrey, YautjaMarkKind.Prey, hunter), Is.True);

                    Assert.That(
                        marks.TryMark((bracer, bracerComp), hunter, xenoPrey, YautjaMarkKind.Prey, null),
                        Is.True,
                        "CMSS13 mark_for_hunt() includes living isxeno() prey.");
                    Assert.That(marks.TryClearMark(xenoPrey, YautjaMarkKind.Prey, hunter), Is.True);

                    Assert.That(
                        marks.TryMark((bracer, bracerComp), hunter, yautja, YautjaMarkKind.Prey, null),
                        Is.False,
                        "CMSS13 mark_for_hunt() target list is living humans and xenos, not Yautja.");
                    Assert.That(marks.TryGetMarkOwner(yautja, YautjaMarkKind.Prey, out _), Is.False);

                    Assert.That(
                        marks.TryMark((bracer, bracerComp), hunter, deadHuman, YautjaMarkKind.Prey, null),
                        Is.False,
                        "CMSS13 mark_for_hunt() filters out dead prey.");
                });
            }
            finally
            {
                DeleteAll(entMan, hunter, humanPrey, xenoPrey, yautja, deadHuman, bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HonorAndDishonorTargetFamiliesMatchCmss13MarkHudprocs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var marks = entMan.System<YautjaMarkSystem>();
            var mobState = entMan.System<MobStateSystem>();

            var hunter = SpawnHunterWithBracer(entMan, map.GridCoords, out var bracer);
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(2, 0)));
            var yautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(3, 0)));
            var deadHuman = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4, 0)));
            var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

            try
            {
                mobState.ChangeMobState(deadHuman, MobState.Dead);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        marks.TryMark((bracer, bracerComp), hunter, human, YautjaMarkKind.Honored, "spared civilians"),
                        Is.True,
                        "CMSS13 mark_honored() includes living ishuman_strict() targets.");
                    Assert.That(marks.TryClearMark(human, YautjaMarkKind.Honored, hunter), Is.True);

                    Assert.That(
                        marks.TryMark((bracer, bracerComp), hunter, xeno, YautjaMarkKind.Honored, "worthy serpent"),
                        Is.False,
                        "CMSS13 mark_honored() does not list xenos.");
                    Assert.That(
                        marks.TryMark((bracer, bracerComp), hunter, yautja, YautjaMarkKind.Honored, "worthy brother"),
                        Is.False,
                        "CMSS13 mark_honored() uses ishuman_strict(), not Yautja.");
                    Assert.That(
                        marks.TryMark((bracer, bracerComp), hunter, deadHuman, YautjaMarkKind.Honored, "dead"),
                        Is.False,
                        "CMSS13 mark_honored() filters out dead targets.");

                    Assert.That(
                        marks.TryMark((bracer, bracerComp), hunter, human, YautjaMarkKind.Dishonored, "thief"),
                        Is.True,
                        "CMSS13 mark_dishonored() includes living ishuman_strict() targets.");
                    Assert.That(marks.TryClearMark(human, YautjaMarkKind.Dishonored, hunter), Is.True);

                    Assert.That(
                        marks.TryMark((bracer, bracerComp), hunter, xeno, YautjaMarkKind.Dishonored, "serpent"),
                        Is.True,
                        "CMSS13 mark_dishonored() includes living isxeno() targets.");
                    Assert.That(marks.TryClearMark(xeno, YautjaMarkKind.Dishonored, hunter), Is.True);

                    Assert.That(
                        marks.TryMark((bracer, bracerComp), hunter, yautja, YautjaMarkKind.Dishonored, "brother"),
                        Is.False,
                        "CMSS13 mark_dishonored() target list is living humans and xenos, not Yautja.");
                    Assert.That(
                        marks.TryMark((bracer, bracerComp), hunter, deadHuman, YautjaMarkKind.Dishonored, "dead"),
                        Is.False,
                        "CMSS13 mark_dishonored() filters out dead targets.");
                });
            }
            finally
            {
                DeleteAll(entMan, hunter, human, xeno, yautja, deadHuman, bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterDeletionClearsOwnedGenericMarksLikeCmss13CleanData()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var marks = entMan.System<YautjaMarkSystem>();

            var hunter = SpawnHunterWithBracer(entMan, map.GridCoords, out var bracer);
            var prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var honored = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var dishonored = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));
            var gearCarrier = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4, 0)));
            var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

            try
            {
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, prey, YautjaMarkKind.Prey, null), Is.True);
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, honored, YautjaMarkKind.Honored, "worthy"), Is.True);
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, dishonored, YautjaMarkKind.Dishonored, "thief"), Is.True);
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, gearCarrier, YautjaMarkKind.GearCarrier, null), Is.True);

                entMan.DeleteEntity(hunter);

                Assert.That(marks.TryGetMarkOwner(prey, YautjaMarkKind.Prey, out _), Is.False,
                    "CMSS13 hunter_data.clean_data() clears prey.hunter when the hunter owner is deleted.");
                Assert.That(marks.TryGetMarkOwner(honored, YautjaMarkKind.Honored, out _), Is.False,
                    "CMSS13 hunter_data.clean_data() clears target honored_set links from the deleted hunter's honored_targets list.");
                Assert.That(marks.TryGetMarkOwner(dishonored, YautjaMarkKind.Dishonored, out _), Is.False,
                    "CMSS13 hunter_data.clean_data() clears target dishonored_set links from the deleted hunter's dishonored_targets list.");
                Assert.That(marks.TryGetMarkOwner(gearCarrier, YautjaMarkKind.GearCarrier, out _), Is.False,
                    "CMSS13 hunter_data.clean_data() clears target gear_set links from the deleted hunter's gear_targets list.");
            }
            finally
            {
                DeleteAll(entMan, hunter, prey, honored, dishonored, gearCarrier, bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(YautjaMarkKind.Honored, "spared civilians", "honored")]
    [TestCase(YautjaMarkKind.Dishonored, "stole hunter gear", "dishonorable")]
    public async Task HonorMarkRepeatDenialKeepsCmss13Reason(YautjaMarkKind kind, string reason, string expectedKindText)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid firstHunter = default;
        EntityUid secondHunter = default;
        EntityUid target = default;
        EntityUid firstBracer = default;
        EntityUid secondBracer = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var marks = entMan.System<YautjaMarkSystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                firstHunter = SpawnHunterWithBracer(entMan, map.GridCoords, out firstBracer);
                secondHunter = SpawnHunterWithBracer(entMan, map.GridCoords.Offset(new Vector2(1, 0)), out secondBracer);
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));

                metadata.SetEntityName(firstHunter, "A'ke Ret");
                metadata.SetEntityName(secondHunter, "Ki'cte Pa");
                metadata.SetEntityName(target, "Guan Thwei");
                server.PlayerMan.SetAttachedEntity(session, secondHunter);

                Assert.That(marks.TryMark((firstBracer, entMan.GetComponent<YautjaBracerComponent>(firstBracer)), firstHunter, target, kind, reason), Is.True);
                Assert.That(marks.TryMark((secondBracer, entMan.GetComponent<YautjaBracerComponent>(secondBracer)), secondHunter, target, kind, "second hunter"), Is.False);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels.Any(label =>
                        label.Contains("Guan Thwei", StringComparison.OrdinalIgnoreCase) &&
                        label.Contains(expectedKindText, StringComparison.OrdinalIgnoreCase) &&
                        label.Contains("A'ke Ret", StringComparison.OrdinalIgnoreCase) &&
                        label.Contains(reason, StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"CMSS13 repeat-denial text includes target, original hunter and original reason.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, firstHunter, secondHunter, target, firstBracer, secondBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GearCarrierRepeatDenialUsesCmss13SourceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid firstHunter = default;
        EntityUid secondHunter = default;
        EntityUid target = default;
        EntityUid firstBracer = default;
        EntityUid secondBracer = default;
        EntityUid? previousAttached = null;

        const string FirstHunterName = "A'ke Ret";
        const string TargetName = "Guan Thwei";

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var marks = entMan.System<YautjaMarkSystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                firstHunter = SpawnHunterWithBracer(entMan, map.GridCoords, out firstBracer);
                secondHunter = SpawnHunterWithBracer(entMan, map.GridCoords.Offset(new Vector2(1, 0)), out secondBracer);
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));

                metadata.SetEntityName(firstHunter, FirstHunterName);
                metadata.SetEntityName(target, TargetName);
                server.PlayerMan.SetAttachedEntity(session, secondHunter);

                Assert.That(
                    marks.TryMark((firstBracer, entMan.GetComponent<YautjaBracerComponent>(firstBracer)), firstHunter, target, YautjaMarkKind.GearCarrier, null),
                    Is.True);
                Assert.That(
                    marks.TryMark((secondBracer, entMan.GetComponent<YautjaBracerComponent>(secondBracer)), secondHunter, target, YautjaMarkKind.GearCarrier, null),
                    Is.False);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels.Any(label => label.Contains($"{TargetName} has already been marked as a gear carrier by {FirstHunterName}!", StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"CMSS13 mark_gear() repeat denial includes the target, original gear marker and exclamation.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, firstHunter, secondHunter, target, firstBracer, secondBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [TestCase(YautjaMarkKind.Honored)]
    [TestCase(YautjaMarkKind.Dishonored)]
    public async Task OtherHunterCannotUnmarkHonorOrDishonorLikeCmss13(YautjaMarkKind kind)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid ownerHunter = default;
        EntityUid otherHunter = default;
        EntityUid target = default;
        EntityUid ownerBracer = default;
        EntityUid otherBracer = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var marks = entMan.System<YautjaMarkSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                ownerHunter = SpawnHunterWithBracer(entMan, map.GridCoords, out ownerBracer);
                otherHunter = SpawnHunterWithBracer(entMan, map.GridCoords.Offset(new Vector2(1, 0)), out otherBracer);
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                server.PlayerMan.SetAttachedEntity(session, otherHunter);

                Assert.That(
                    marks.TryMark((ownerBracer, entMan.GetComponent<YautjaBracerComponent>(ownerBracer)), ownerHunter, target, kind, "source owner"),
                    Is.True);

                Assert.That(marks.TryOpenMarkPanel((otherBracer, entMan.GetComponent<YautjaBracerComponent>(otherBracer)), otherHunter), Is.True);
                ui.RaiseUiMessage(
                    otherBracer,
                    YautjaMarkUIKey.Key,
                    new YautjaMarkPanelUnmarkMsg(entMan.GetNetEntity(target), kind)
                    {
                        Actor = otherHunter,
                    });

                Assert.That(marks.IsMarkedBy(target, kind, ownerHunter), Is.True,
                    "CMSS13 only lets the original living hunter undo their honored/dishonored mark.");
                Assert.That(marks.IsMarkedBy(target, kind, otherHunter), Is.False,
                    "A failed unmark attempt must not transfer ownership to the acting hunter.");
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels.Any(label => label.Contains("You cannot undo the actions of a living brother or sister!", StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"CMSS13 unmark_honored()/unmark_dishonored() denies non-owner living hunters with the source text.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, ownerHunter, otherHunter, target, ownerBracer, otherBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GearCarrierTransitionsBroadcastAndOtherHunterCanUnmarkLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid ownerHunter = default;
        EntityUid otherHunter = default;
        EntityUid target = default;
        EntityUid ownerBracer = default;
        EntityUid otherBracer = default;
        EntityUid? previousAttached = null;

        const string OwnerName = "A'ke Ret";
        const string OtherName = "Ki'cte Pa";
        const string TargetName = "Guan Thwei";

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var marks = entMan.System<YautjaMarkSystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                ownerHunter = SpawnHunterWithBracer(entMan, map.GridCoords, out ownerBracer);
                otherHunter = SpawnHunterWithBracer(entMan, map.GridCoords.Offset(new Vector2(1, 0)), out otherBracer);
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                metadata.SetEntityName(ownerHunter, OwnerName);
                metadata.SetEntityName(otherHunter, OtherName);
                metadata.SetEntityName(target, TargetName);
                server.PlayerMan.SetAttachedEntity(session, otherHunter);

                Assert.That(
                    marks.TryMark((ownerBracer, entMan.GetComponent<YautjaBracerComponent>(ownerBracer)), ownerHunter, target, YautjaMarkKind.GearCarrier, null),
                    Is.True);

                Assert.That(marks.TryOpenMarkPanel((otherBracer, entMan.GetComponent<YautjaBracerComponent>(otherBracer)), otherHunter), Is.True);
                ui.RaiseUiMessage(
                    otherBracer,
                    YautjaMarkUIKey.Key,
                    new YautjaMarkPanelUnmarkMsg(entMan.GetNetEntity(target), YautjaMarkKind.GearCarrier)
                    {
                        Actor = otherHunter,
                    });

                Assert.That(marks.TryGetMarkOwner(target, YautjaMarkKind.GearCarrier, out _), Is.False,
                    "CMSS13 unmark_gear() lets a living Yautja unmark an existing gear carrier; it does not require the original marker.");
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        labels.Any(label => label.Contains($"{OwnerName} has marked {TargetName} as a Gear Carrier!", StringComparison.OrdinalIgnoreCase)),
                        Is.True,
                        $"CMSS13 mark_gear() broadcasts the gear-carrier transition.\nActual labels:\n{joinedLabels}");

                    Assert.That(
                        labels.Any(label => label.Contains($"{OtherName} has un-marked {TargetName} as a Gear Carrier!", StringComparison.OrdinalIgnoreCase)),
                        Is.True,
                        $"CMSS13 unmark_gear() broadcasts the acting hunter, not necessarily the original marker.\nActual labels:\n{joinedLabels}");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, ownerHunter, otherHunter, target, ownerBracer, otherBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [TestCase(YautjaMarkKind.Honored, "Honored", "honored")]
    [TestCase(YautjaMarkKind.Dishonored, "Dishonorable", "dishonorable")]
    public async Task HonorAndDishonorMarkTransitionsBroadcastCmss13Text(YautjaMarkKind kind, string markText, string unmarkText)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid target = default;
        EntityUid bracer = default;
        EntityUid? previousAttached = null;

        const string HunterName = "A'ke Ret";
        const string TargetName = "Guan Thwei";
        const string Reason = "source transition";

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var marks = entMan.System<YautjaMarkSystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = SpawnHunterWithBracer(entMan, map.GridCoords, out bracer);
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                metadata.SetEntityName(hunter, HunterName);
                metadata.SetEntityName(target, TargetName);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                Assert.That(
                    marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), hunter, target, kind, Reason),
                    Is.True);

                Assert.That(marks.TryOpenMarkPanel((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), hunter), Is.True);
                ui.RaiseUiMessage(
                    bracer,
                    YautjaMarkUIKey.Key,
                    new YautjaMarkPanelUnmarkMsg(entMan.GetNetEntity(target), kind)
                    {
                        Actor = hunter,
                    });

                Assert.That(marks.TryGetMarkOwner(target, kind, out _), Is.False,
                    "The original marker can undo their honored/dishonored mark in CMSS13.");
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        labels.Any(label =>
                            label.Contains($"{HunterName} has marked {TargetName} as {markText} for '{Reason}'.", StringComparison.OrdinalIgnoreCase)),
                        Is.True,
                        $"CMSS13 mark_honored()/mark_dishonored() broadcasts the mark transition and reason.\nActual labels:\n{joinedLabels}");

                    Assert.That(
                        labels.Any(label =>
                            label.Contains($"{HunterName} has un-marked {TargetName} as {unmarkText}!", StringComparison.OrdinalIgnoreCase)),
                        Is.True,
                        $"CMSS13 unmark_honored()/unmark_dishonored() broadcasts the unmark transition.\nActual labels:\n{joinedLabels}");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, hunter, target, bracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovingOwnPreyShowsCmss13HuntRemovalText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid prey = default;
        EntityUid bracer = default;
        EntityUid action = default;
        EntityUid? previousAttached = null;

        const string PreyName = "Guan Thwei";

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
                action = entMan.SpawnEntity("CMUActionYautjaMarkForHunt", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                metadata.SetEntityName(prey, PreyName);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                entMan.EventBus.RaiseLocalEvent(hunter, new YautjaMarkForHuntActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                    Target = prey,
                });

                entMan.EventBus.RaiseLocalEvent(hunter, new YautjaMarkForHuntActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                    Target = prey,
                });

                Assert.That(entMan.System<YautjaMarkSystem>().TryGetMarkOwner(prey, YautjaMarkKind.Prey, out _), Is.False,
                    "CMSS13 remove_from_hunt() clears both the hunter's prey link and the prey's hunter link.");
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels.Any(label => label.Contains($"You have removed {PreyName} from your hunt.", StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"CMSS13 remove_from_hunt() tells the hunter the abandoned prey name.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, hunter, prey, bracer, action);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TrophyClaimOfHuntedPreyUsesCmss13ClaimTextAndClearsPrey()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid prey = default;
        EntityUid bracer = default;
        EntityUid? previousAttached = null;

        const string HunterName = "A'ke Ret";
        const string PreyName = "Guan Thwei";

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var marks = entMan.System<YautjaMarkSystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var mobState = entMan.System<MobStateSystem>();
                var trophies = entMan.System<YautjaTrophySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                metadata.SetEntityName(hunter, HunterName);
                metadata.SetEntityName(prey, PreyName);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                Assert.That(
                    marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), hunter, prey, YautjaMarkKind.Prey, null),
                    Is.True);
                mobState.ChangeMobState(prey, MobState.Dead);

                Assert.That(trophies.TryHarvestTrophy(hunter, prey, YautjaTrophyKind.HumanSkull, out _), Is.True);
                Assert.That(marks.TryGetMarkOwner(prey, YautjaMarkKind.Prey, out _), Is.False,
                    "CMSS13 trophy claim clears hunter_data.prey and the prey's hunter/hunted fields.");
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        labels.Any(label => label.Contains($"You have claimed {PreyName} as your trophy.", StringComparison.OrdinalIgnoreCase)),
                        Is.True,
                        $"CMSS13 butcher/trophy prey completion tells the hunter with the source text.\nActual labels:\n{joinedLabels}");
                    Assert.That(
                        labels.Any(label => label.Contains($"{HunterName} has claimed {PreyName} as their trophy.", StringComparison.OrdinalIgnoreCase)),
                        Is.True,
                        $"CMSS13 butcher/trophy prey completion broadcasts the claim to Yautja with the source text.\nActual labels:\n{joinedLabels}");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, hunter, prey, bracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ScalpClaimOfHuntedPreyUsesCmss13ClaimTextAndClearsPrey()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid prey = default;
        EntityUid bracer = default;
        EntityUid scalp = default;
        EntityUid? previousAttached = null;

        const string HunterName = "A'ke Ret";
        const string PreyName = "Guan Thwei";

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var marks = entMan.System<YautjaMarkSystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var trophies = entMan.System<YautjaTrophySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                metadata.SetEntityName(hunter, HunterName);
                metadata.SetEntityName(prey, PreyName);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                Assert.That(
                    marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), hunter, prey, YautjaMarkKind.Prey, null),
                    Is.True);

                scalp = trophies.SpawnRuntimeScalp(prey, hunter);
                Assert.That(entMan.Deleted(scalp), Is.False);
                Assert.That(marks.TryGetMarkOwner(prey, YautjaMarkKind.Prey, out _), Is.False,
                    "CMSS13 ceremonial-dagger scalp claim clears hunter_data.prey after claiming the scalp.");
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        labels.Any(label => label.Contains($"You have claimed the scalp of {PreyName} as your trophy.", StringComparison.OrdinalIgnoreCase)),
                        Is.True,
                        $"CMSS13 scalp prey completion tells the hunter with the scalp-specific source text.\nActual labels:\n{joinedLabels}");
                    Assert.That(
                        labels.Any(label => label.Contains($"{HunterName} has claimed the scalp of {PreyName} as their trophy.", StringComparison.OrdinalIgnoreCase)),
                        Is.True,
                        $"CMSS13 scalp prey completion broadcasts the scalp claim to Yautja with the source text.\nActual labels:\n{joinedLabels}");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, hunter, prey, bracer, scalp);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DestroyedPreyNotifiesHunterLikeCmss13CleanData()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid prey = default;
        EntityUid bracer = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var marks = entMan.System<YautjaMarkSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = SpawnHunterWithBracer(entMan, map.GridCoords, out bracer);
                prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                server.PlayerMan.SetAttachedEntity(session, hunter);

                Assert.That(
                    marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), hunter, prey, YautjaMarkKind.Prey, null),
                    Is.True);

                entMan.DeleteEntity(prey);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels.Any(label => label.Contains("Your Prey has been utterly destroyed!", StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"CMSS13 huntdata.clean_data() tells the hunter when their prey target is destroyed.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, hunter, prey, bracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DestroyedThrallNotifiesMasterAndYautjaLikeCmss13CleanData()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid master = default;
        EntityUid thrall = default;
        EntityUid bracer = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var marks = entMan.System<YautjaMarkSystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                master = SpawnHunterWithBracer(entMan, map.GridCoords, out bracer);
                thrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                metadata.SetEntityName(master, "A'ke Ret");
                metadata.SetEntityName(thrall, "Dachande Ooman");
                server.PlayerMan.SetAttachedEntity(session, master);

                Assert.That(
                    marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), master, thrall, YautjaMarkKind.Thrall, "claimed as a thrall"),
                    Is.True);

                entMan.DeleteEntity(thrall);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels.Any(label => label.Contains("Your Thrall has been utterly destroyed!", StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"CMSS13 huntdata.clean_data() tells the master when their thrall target is destroyed.\nActual labels:\n{joinedLabels}");

                var history = client.ResolveDependency<IUserInterfaceManager>()
                    .GetUIController<ChatUIController>()
                    .History
                    .Select(entry => entry.Msg)
                    .ToList();
                var joinedHistory = string.Join("\n", history.Select(message => $"{message.Channel}: {message.Message}"));

                Assert.That(
                    history.Any(message =>
                        message.Channel == ChatChannel.Radio &&
                        message.Message.Contains("A'ke Ret's Thrall, Dachande Ooman, has been utterly destroyed!", StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"CMSS13 huntdata.clean_data() broadcasts destroyed thralls to Yautja with master and thrall names.\nActual chat history:\n{joinedHistory}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, master, thrall, bracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    private static EntityUid SpawnHunterWithBracer(IEntityManager entMan, EntityCoordinates coordinates, out EntityUid bracer)
    {
        var hunter = entMan.SpawnEntity("CMMobHuman", coordinates);
        bracer = entMan.SpawnEntity("CMUYautjaBracer", coordinates);
        entMan.EnsureComponent<YautjaComponent>(hunter);
        Assert.That(entMan.System<InventorySystem>().TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
        return hunter;
    }

    private static void DeleteAll(IEntityManager entMan, params EntityUid[] uids)
    {
        foreach (var uid in uids)
        {
            if (uid != default && !entMan.Deleted(uid))
                entMan.DeleteEntity(uid);
        }
    }
}
