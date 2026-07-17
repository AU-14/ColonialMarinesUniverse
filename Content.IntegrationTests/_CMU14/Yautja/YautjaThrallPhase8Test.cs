using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client.Popups;
using Content.Client.UserInterface.Systems.Chat;
using Content.Server._CMU14.Yautja;
using Content.Server.Administration.Logs;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dialog;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Stunnable;
using Content.Shared.UserInterface;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaThrallPhase8Test
{
    [Test]
    public async Task BadBloodCannotUseHonorThrallOrBloodingMarksLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid badBlood = default;
        EntityUid setupHunter = default;
        var targets = new List<EntityUid>();
        EntityUid bracer = default;
        EntityUid setupBracer = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var marks = entMan.System<YautjaMarkSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                badBlood = SpawnBadBloodWithBracer(entMan, map.GridCoords, out bracer);
                setupHunter = SpawnHunterWithBracer(entMan, map.GridCoords.Offset(new Vector2(1, 0)), out setupBracer);
                server.PlayerMan.SetAttachedEntity(session, badBlood);

                var kinds = new[]
                {
                    YautjaMarkKind.Honored,
                    YautjaMarkKind.Dishonored,
                    YautjaMarkKind.Thrall,
                    YautjaMarkKind.Blooded,
                };

                for (var i = 0; i < kinds.Length; i++)
                {
                    var kind = kinds[i];
                    var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2 + i, 0)));
                    targets.Add(target);

                    if (kind == YautjaMarkKind.Blooded)
                        Assert.That(
                            marks.TryMark((setupBracer, entMan.GetComponent<YautjaBracerComponent>(setupBracer)), setupHunter, target, YautjaMarkKind.Thrall, "setup thrall"),
                            Is.True);

                    Assert.That(
                        marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), badBlood, target, kind, "bad blood attempt"),
                        Is.False,
                        "CMSS13 yaut_hudprocs.dm returns before honored/dishonored/thrall/blooded mark state changes when the actor is FACTION_YAUTJA_BADBLOOD.");

                    Assert.That(marks.IsMarkedBy(target, kind, badBlood), Is.False);
                }
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels.Count(label => label.Contains("You have no honor. You cannot do this.", StringComparison.OrdinalIgnoreCase)),
                    Is.GreaterThanOrEqualTo(1),
                    $"CMSS13 Bad Blood mark guard uses the source text before opening the target/reason flow.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, targets.Append(badBlood).Append(setupHunter).Append(bracer).Append(setupBracer).ToArray());
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodCannotUnmarkHonorDishonorOrThrallLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid ownerHunter = default;
        EntityUid badBlood = default;
        var targets = new List<EntityUid>();
        EntityUid ownerBracer = default;
        EntityUid badBloodBracer = default;
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
                badBlood = SpawnBadBloodWithBracer(entMan, map.GridCoords.Offset(new Vector2(1, 0)), out badBloodBracer);
                server.PlayerMan.SetAttachedEntity(session, badBlood);

                Assert.That(marks.TryOpenMarkPanel((badBloodBracer, entMan.GetComponent<YautjaBracerComponent>(badBloodBracer)), badBlood), Is.True);

                var kinds = new[]
                {
                    YautjaMarkKind.Honored,
                    YautjaMarkKind.Dishonored,
                    YautjaMarkKind.Thrall,
                };

                for (var i = 0; i < kinds.Length; i++)
                {
                    var kind = kinds[i];
                    var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2 + i, 0)));
                    targets.Add(target);

                    Assert.That(
                        marks.TryMark((ownerBracer, entMan.GetComponent<YautjaBracerComponent>(ownerBracer)), ownerHunter, target, kind, "source owner"),
                        Is.True);

                    ui.RaiseUiMessage(
                        badBloodBracer,
                        YautjaMarkUIKey.Key,
                        new YautjaMarkPanelUnmarkMsg(entMan.GetNetEntity(target), kind)
                        {
                            Actor = badBlood,
                        });

                    Assert.That(marks.IsMarkedBy(target, kind, ownerHunter), Is.True,
                        "CMSS13 Bad Blood unmark_honored()/unmark_dishonored()/unmark_thralled() returns before removing source state.");
                }
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels.Any(label => label.Contains("You have no honor. You cannot do this.", StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"CMSS13 Bad Blood unmark guard uses the source text before owner checks.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, targets.Append(ownerHunter).Append(badBlood).Append(ownerBracer).Append(badBloodBracer).ToArray());
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThrallRepeatDenialKeepsCmss13ReasonAndOwner()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid firstHunter = default;
        EntityUid secondHunter = default;
        EntityUid thrall = default;
        EntityUid firstBracer = default;
        EntityUid secondBracer = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var marks = entMan.System<YautjaMarkSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                firstHunter = SpawnHunterWithBracer(entMan, map.GridCoords, out firstBracer);
                secondHunter = SpawnHunterWithBracer(entMan, map.GridCoords.Offset(new Vector2(1, 0)), out secondBracer);
                thrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));

                server.PlayerMan.SetAttachedEntity(session, secondHunter);

                Assert.That(
                    marks.TryMark((firstBracer, entMan.GetComponent<YautjaBracerComponent>(firstBracer)), firstHunter, thrall, YautjaMarkKind.Thrall, "saved a hunter"),
                    Is.True);

                Assert.That(
                    marks.TryMark((secondBracer, entMan.GetComponent<YautjaBracerComponent>(secondBracer)), secondHunter, thrall, YautjaMarkKind.Thrall, "second claim"),
                    Is.False,
                    "CMSS13 mark_thralled() rejects already-thralled targets and preserves the original thralled_set/reason.");

                var comp = entMan.GetComponent<YautjaThrallComponent>(thrall);
                Assert.That(comp.Master, Is.EqualTo(firstHunter));
                Assert.That(comp.Reason, Is.EqualTo("saved a hunter"));
                Assert.That(marks.IsMarkedBy(thrall, YautjaMarkKind.Thrall, firstHunter), Is.True);
                Assert.That(marks.IsMarkedBy(thrall, YautjaMarkKind.Thrall, secondHunter), Is.False);
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
                        label.Contains("has already been thralled by", StringComparison.OrdinalIgnoreCase) &&
                        label.Contains("saved a hunter", StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"CMSS13 mark_thralled() repeat denial says the target has already been thralled by the first hunter for the first reason.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, firstHunter, secondHunter, thrall, firstBracer, secondBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThrallMarkBloodAndReleaseUseCmss13ReasonText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            AdminLogsEnabled = true,
            DummyTicker = false,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var adminLogs = server.ResolveDependency<IAdminLogManager>();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var marks = entMan.System<YautjaMarkSystem>();

            var hunter = SpawnHunterWithBracer(entMan, map.GridCoords, out var bracer);
            var thrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

            try
            {
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, thrall, YautjaMarkKind.Thrall, "spared after duel"), Is.True);
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, thrall, YautjaMarkKind.Blooded, "proved restraint"), Is.True);
                Assert.That(marks.TryClearMark(thrall, YautjaMarkKind.Thrall, hunter), Is.True);
            }
            finally
            {
                DeleteAll(entMan, hunter, thrall, bracer);
            }
        });

        var logs = await adminLogs.CurrentRoundLogs(new LogFilter
        {
            Types = new HashSet<LogType> { LogType.Action },
        });
        var messages = logs.Select(log => log.Message).ToList();
        var joinedMessages = string.Join("\n", messages);

        Assert.Multiple(() =>
        {
            Assert.That(
                messages.Any(message =>
                    message.Contains("has taken", StringComparison.OrdinalIgnoreCase) &&
                    message.Contains("as their Thrall", StringComparison.OrdinalIgnoreCase) &&
                    message.Contains("spared after duel", StringComparison.OrdinalIgnoreCase)),
                Is.True,
                $"CMSS13 mark_thralled() logs '[hunter] has taken [target] as their Thrall for [reason]'.\nActual logs:\n{joinedMessages}");

            Assert.That(
                messages.Any(message =>
                    message.Contains("has blooded", StringComparison.OrdinalIgnoreCase) &&
                    message.Contains("proved restraint", StringComparison.OrdinalIgnoreCase)),
                Is.True,
                $"CMSS13 mark_blooded() logs '[hunter] has blooded [target] for [reason]'.\nActual logs:\n{joinedMessages}");

            Assert.That(
                messages.Any(message =>
                    message.Contains("has released", StringComparison.OrdinalIgnoreCase) &&
                    message.Contains("from thralldom", StringComparison.OrdinalIgnoreCase)),
                Is.True,
                $"CMSS13 unmark_thralled() logs '[hunter] has released [target] from thralldom!'.\nActual logs:\n{joinedMessages}");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BloodedThrallCannotBeUnbloodedLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var marks = entMan.System<YautjaMarkSystem>();

            var hunter = SpawnHunterWithBracer(entMan, map.GridCoords, out var bracer);
            var thrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

            try
            {
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, thrall, YautjaMarkKind.Thrall, "claimed"), Is.True);
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, thrall, YautjaMarkKind.Blooded, "proved worthy"), Is.True);

                Assert.That(
                    marks.TryClearMark(thrall, YautjaMarkKind.Blooded, hunter),
                    Is.False,
                    "CMSS13 mark_blooded() has no mark_unblooded path; Blooded Thrall is one-way.");

                var comp = entMan.GetComponent<YautjaThrallComponent>(thrall);
                Assert.That(comp.Blooded, Is.True);
                Assert.That(comp.TechAuthorized, Is.True);
                Assert.That(entMan.HasComponent<YautjaTechAuthorizedComponent>(thrall), Is.True);
                Assert.That(marks.IsMarkedBy(thrall, YautjaMarkKind.Blooded, hunter), Is.True);
            }
            finally
            {
                DeleteAll(entMan, hunter, thrall, bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThrallAndBloodedMarkEmptyReasonCancelsLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var marks = entMan.System<YautjaMarkSystem>();

            var hunter = SpawnHunterWithBracer(entMan, map.GridCoords, out var bracer);
            var emptyThrallTarget = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var whitespaceThrallTarget = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var bloodedTarget = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));
            var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(
                        marks.TryMark((bracer, bracerComp), hunter, emptyThrallTarget, YautjaMarkKind.Thrall, string.Empty),
                        Is.False,
                        "CMSS13 mark_thralled() returns when the stripped reason input is empty.");
                    Assert.That(
                        marks.TryMark((bracer, bracerComp), hunter, whitespaceThrallTarget, YautjaMarkKind.Thrall, "   "),
                        Is.False,
                        "CMSS13 mark_thralled() strips whitespace before the empty reason guard.");
                });

                Assert.That(marks.TryMark((bracer, bracerComp), hunter, bloodedTarget, YautjaMarkKind.Thrall, "proved useful"), Is.True);
                Assert.That(
                    marks.TryMark((bracer, bracerComp), hunter, bloodedTarget, YautjaMarkKind.Blooded, string.Empty),
                    Is.False,
                    "CMSS13 mark_blooded() returns when the stripped reason input is empty.");

                Assert.Multiple(() =>
                {
                    Assert.That(marks.IsMarkedBy(emptyThrallTarget, YautjaMarkKind.Thrall, hunter), Is.False);
                    Assert.That(marks.IsMarkedBy(whitespaceThrallTarget, YautjaMarkKind.Thrall, hunter), Is.False);
                    Assert.That(marks.IsMarkedBy(bloodedTarget, YautjaMarkKind.Thrall, hunter), Is.True);
                    Assert.That(marks.IsMarkedBy(bloodedTarget, YautjaMarkKind.Blooded, hunter), Is.False);
                    Assert.That(entMan.GetComponent<YautjaThrallComponent>(bloodedTarget).Blooded, Is.False);
                    Assert.That(entMan.HasComponent<YautjaTechAuthorizedComponent>(bloodedTarget), Is.False);
                });
            }
            finally
            {
                DeleteAll(entMan, hunter, bracer, emptyThrallTarget, whitespaceThrallTarget, bloodedTarget);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BloodedThrallRepeatDenialAndGuidanceUseCmss13Text()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid otherHunter = default;
        EntityUid thrall = default;
        EntityUid bracer = default;
        EntityUid otherBracer = default;
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
                otherHunter = SpawnHunterWithBracer(entMan, map.GridCoords.Offset(new Vector2(1, 0)), out otherBracer);
                thrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));

                server.PlayerMan.SetAttachedEntity(session, thrall);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), hunter, thrall, YautjaMarkKind.Thrall, "rescued"), Is.True);
                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), hunter, thrall, YautjaMarkKind.Blooded, "earned a name"), Is.True);

                server.PlayerMan.SetAttachedEntity(session, otherHunter);
                Assert.That(
                    marks.TryMark((otherBracer, entMan.GetComponent<YautjaBracerComponent>(otherBracer)), otherHunter, thrall, YautjaMarkKind.Blooded, "second blooding"),
                    Is.False,
                    "CMSS13 mark_blooded() rejects an already blooded target and keeps blooded_set/blooded_reason.");
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
                            label.Contains("You are a Blooded Thrall", StringComparison.OrdinalIgnoreCase) &&
                            label.Contains("developing your reputation", StringComparison.OrdinalIgnoreCase) &&
                            label.Contains("Honor Code", StringComparison.OrdinalIgnoreCase) &&
                            label.Contains("LOOC", StringComparison.OrdinalIgnoreCase)),
                        Is.True,
                        $"CMSS13 mark_blooded() sends ordinary thralls the full Blooded guidance paragraph.\nActual labels:\n{joinedLabels}");

                    Assert.That(
                        labels.Any(label =>
                            label.Contains("has already been blooded by", StringComparison.OrdinalIgnoreCase) &&
                            label.Contains("earned a name", StringComparison.OrdinalIgnoreCase)),
                        Is.True,
                        $"CMSS13 mark_blooded() repeat denial says the target has already been blooded by the first blooding hunter for the first reason.\nActual labels:\n{joinedLabels}");
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
                DeleteAll(entMan, hunter, otherHunter, thrall, bracer, otherBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BloodedThrallNamePromptRenamesAndYautjaFactionsLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var marks = entMan.System<YautjaMarkSystem>();
            var metadata = entMan.System<MetaDataSystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();

            var hunter = SpawnHunterWithBracer(entMan, map.GridCoords, out var bracer);
            var thrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            metadata.SetEntityName(thrall, "Ordinary Thrall");
            entMan.EnsureComponent<NpcFactionMemberComponent>(thrall).Factions.Add("aucolonist");
            entMan.System<GunIFFSystem>().AddUserFaction(thrall, "FactionMarine");

            try
            {
                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), hunter, thrall, YautjaMarkKind.Thrall, "claimed"), Is.True);
                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), hunter, thrall, YautjaMarkKind.Blooded, "proved worthy"), Is.True);

                Assert.That(entMan.TryGetComponent(bracer, out DialogComponent? dialog), Is.True,
                    "CMSS13 mark_blooded() asks the Yautja to enter a new name for an ordinary thralled newblood.");
                Assert.Multiple(() =>
                {
                    Assert.That(dialog!.DialogType, Is.EqualTo(DialogType.Input));
                    Assert.That(dialog.Title, Is.EqualTo("Blooded Name"));
                    Assert.That(dialog.Message.Text, Is.EqualTo("Enter the newblood's new name."));
                    Assert.That(dialog.LargeInput, Is.False);
                    Assert.That(dialog.InputEvent, Is.TypeOf<YautjaBloodedThrallNameEvent>());
                });

                ui.RaiseUiMessage(bracer, DialogUiKey.Key, new DialogInputBuiMsg("A'ke Ret")
                {
                    Actor = hunter,
                });

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<MetaDataComponent>(thrall).EntityName, Is.EqualTo("A'ke Ret"),
                        "CMSS13 mark_blooded() calls change_real_name(newblood, html_decode(predtitle)) after the Blooded Name prompt.");
                    Assert.That(entMan.GetComponent<NpcFactionMemberComponent>(thrall).Factions, Is.EquivalentTo(new[] { "CMUYautja" }),
                        "CMSS13 moves the blooded thrall into FACTION_BLOODED_HUNTER; locally this maps to the regular Yautja NPC faction.");
                    Assert.That(entMan.GetComponent<UserIFFComponent>(thrall).Factions, Does.Contain("FactionYautja"),
                        "CMSS13 sets faction_group = FACTION_LIST_YAUTJA; locally this maps to Yautja IFF.");
                    Assert.That(entMan.GetComponent<UserIFFComponent>(thrall).Factions, Does.Not.Contain("FactionMarine"),
                        "Blooded Thrall faction-group assignment should not keep the old local marine IFF group.");
                });
            }
            finally
            {
                DeleteAll(entMan, hunter, bracer, thrall);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LinkThrallBracerRepeatUseKeepsCmss13AlreadyLinkedGuard()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid master = default;
        EntityUid thrall = default;
        EntityUid masterBracer = default;
        EntityUid thrallBracer = default;
        EntityUid thrallObservedMaster = default;
        EntityUid thrallObservedThrall = default;
        EntityUid thrallObservedMasterBracer = default;
        EntityUid thrallObservedThrallBracer = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                SpawnLinkedThrall(entMan, map.GridCoords, out master, out masterBracer, out thrall, out thrallBracer);
                server.PlayerMan.SetAttachedEntity(session, master);

                Assert.That(
                    entMan.System<YautjaThrallSystem>().TryLinkThrallBracer((masterBracer, entMan.GetComponent<YautjaBracerComponent>(masterBracer)), master),
                    Is.False,
                    "CMSS13 link_bracer() returns immediately when linked_bracer already exists.");

                var thrallComp = entMan.GetComponent<YautjaThrallComponent>(thrall);
                var thrallBracerComp = entMan.GetComponent<YautjaThrallBracerComponent>(thrallBracer);
                Assert.Multiple(() =>
                {
                    Assert.That(thrallComp.BracerLinked, Is.True);
                    Assert.That(thrallComp.MasterBracer, Is.EqualTo(masterBracer));
                    Assert.That(thrallComp.ThrallBracer, Is.EqualTo(thrallBracer));
                    Assert.That(thrallBracerComp.Linked, Is.True);
                    Assert.That(thrallBracerComp.Master, Is.EqualTo(master));
                    Assert.That(thrallBracerComp.MasterBracer, Is.EqualTo(masterBracer));
                });
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                var joinedLabels = string.Join("\n", labels);
                Assert.That(
                    labels,
                    Has.Some.Contains("Link is already established!").IgnoreCase,
                    $"CMSS13 link_bracer() uses the already-linked denial before relinking.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, master, thrall, masterBracer, thrallBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LinkThrallBracerFirstUseShowsCmss13SuccessAndLockText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid master = default;
        EntityUid thrall = default;
        EntityUid masterBracer = default;
        EntityUid thrallBracer = default;
        EntityUid thrallObservedMaster = default;
        EntityUid thrallObservedThrall = default;
        EntityUid thrallObservedMasterBracer = default;
        EntityUid thrallObservedThrallBracer = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                master = SpawnHunterWithBracer(entMan, map.GridCoords, out masterBracer);
                thrall = SpawnMarkedThrall(entMan, master, masterBracer, map.GridCoords.Offset(new Vector2(1, 0)));
                thrallBracer = entMan.SpawnEntity("CMUYautjaThrallBracer", map.GridCoords.Offset(new Vector2(1, 0)));
                Assert.That(inventory.TryEquip(thrall, thrallBracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, master);

                Assert.That(
                    entMan.System<YautjaThrallSystem>().TryLinkThrallBracer((masterBracer, entMan.GetComponent<YautjaBracerComponent>(masterBracer)), master),
                    Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels,
                    Has.Some.Contains("Your bracer is now linked to your thrall.").IgnoreCase,
                    $"CMSS13 link_bracer() tells the hunter their bracer is now linked to their thrall.\nActual labels:\n{joinedLabels}");
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var session = server.PlayerMan.Sessions.Single();

                thrallObservedMaster = SpawnHunterWithBracer(entMan, map.GridCoords.Offset(new Vector2(3, 0)), out thrallObservedMasterBracer);
                thrallObservedThrall = SpawnMarkedThrall(entMan, thrallObservedMaster, thrallObservedMasterBracer, map.GridCoords.Offset(new Vector2(4, 0)));
                thrallObservedThrallBracer = entMan.SpawnEntity("CMUYautjaThrallBracer", map.GridCoords.Offset(new Vector2(4, 0)));
                Assert.That(inventory.TryEquip(thrallObservedThrall, thrallObservedThrallBracer, "gloves", silent: true, force: true), Is.True);
                server.PlayerMan.SetAttachedEntity(session, thrallObservedThrall);

                Assert.That(
                    entMan.System<YautjaThrallSystem>().TryLinkThrallBracer((thrallObservedMasterBracer, entMan.GetComponent<YautjaBracerComponent>(thrallObservedMasterBracer)), thrallObservedMaster),
                    Is.True);
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
                        labels,
                        Has.Some.Contains("locks around your wrist with a sharp click.").IgnoreCase,
                        $"CMSS13 link_bracer() warns the thrall that the bracer locks around their wrist.\nActual labels:\n{joinedLabels}");
                    Assert.That(
                        labels,
                        Has.Some.Contains("Your master has linked their bracer to yours.").IgnoreCase,
                        $"CMSS13 link_bracer() tells the thrall their master linked the bracers.\nActual labels:\n{joinedLabels}");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(
                    entMan,
                    master,
                    thrall,
                    masterBracer,
                    thrallBracer,
                    thrallObservedMaster,
                    thrallObservedThrall,
                    thrallObservedMasterBracer,
                    thrallObservedThrallBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThrallBracerForcedUnequipUnlocksAndDisarmsLikeCmss13BaseDrop()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            SpawnLinkedThrall(entMan, map.GridCoords, out var master, out var masterBracer, out var thrall, out var thrallBracer);
            var bracer = entMan.GetComponent<YautjaThrallBracerComponent>(thrallBracer);

            try
            {
                bracer.SelfDestructArmed = true;
                bracer.SelfDestructAt = TimeSpan.FromSeconds(30);
                bracer.NextSelfDestructWarning = TimeSpan.FromSeconds(1);
                entMan.Dirty(thrallBracer, bracer);

                Assert.That(inventory.TryUnequip(thrall, "gloves", silent: true, force: true), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(bracer.User, Is.Null, "CMSS13 yautja bracer dropped() clears owner.");
                    Assert.That(bracer.Locked, Is.False, "CMSS13 yautja bracer dropped() unlocks the bracer so it cannot stay nodrop after forced removal.");
                    Assert.That(bracer.SelfDestructArmed, Is.False, "A forced drop must stop the local active thrall countdown tied to the bracer.");
                    Assert.That(bracer.SelfDestructAt, Is.EqualTo(TimeSpan.Zero));
                    Assert.That(bracer.NextSelfDestructWarning, Is.EqualTo(TimeSpan.Zero));
                });
            }
            finally
            {
                DeleteAll(entMan, master, thrall, masterBracer, thrallBracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThrallBracerDeletionClearsReciprocalLinkLikeCmss13Destroy()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid master = default;
        EntityUid thrall = default;
        EntityUid masterBracer = default;
        EntityUid thrallBracer = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                SpawnLinkedThrall(entMan, map.GridCoords, out master, out masterBracer, out thrall, out thrallBracer);
                entMan.DeleteEntity(thrallBracer);
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var thrallComp = entMan.GetComponent<YautjaThrallComponent>(thrall);

                Assert.Multiple(() =>
                {
                    Assert.That(thrallComp.BracerLinked, Is.False,
                        "CMSS13 yautja bracer Destroy() clears the reciprocal linked_bracer field.");
                    Assert.That(thrallComp.ThrallBracer, Is.Null);
                    Assert.That(thrallComp.MasterBracer, Is.Null);
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                DeleteAll(entMan, master, thrall, masterBracer, thrallBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StunThrallUsesCmss13WeakeningAndSourceText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid master = default;
        EntityUid thrall = default;
        EntityUid masterBracer = default;
        EntityUid thrallBracer = default;
        EntityUid targetMaster = default;
        EntityUid targetThrall = default;
        EntityUid targetMasterBracer = default;
        EntityUid targetThrallBracer = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                SpawnLinkedThrall(entMan, map.GridCoords, out master, out masterBracer, out thrall, out thrallBracer);
                server.PlayerMan.SetAttachedEntity(session, master);

                var thralls = entMan.System<YautjaThrallSystem>();
                Assert.That(
                    thralls.TryStunLinkedThrall((masterBracer, entMan.GetComponent<YautjaBracerComponent>(masterBracer)), master),
                    Is.True);
                Assert.That(entMan.HasComponent<KnockedDownComponent>(thrall), Is.True,
                    "CMSS13 stun_thrall() applies 10 seconds of WEAKEN, represented locally as knockdown/paralyze.");
                Assert.That(
                    thralls.TryStunLinkedThrall((masterBracer, entMan.GetComponent<YautjaBracerComponent>(masterBracer)), master),
                    Is.False,
                    "CMSS13 stun_thrall() refuses a second punishment while the thrall is already stunned.");
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
                        labels,
                        Has.Some.Contains("Your bracer beeps, your thrall is punished.").IgnoreCase,
                        $"CMSS13 stun_thrall() tells the master their thrall is punished.\nActual labels:\n{joinedLabels}");
                    Assert.That(
                        labels,
                        Has.Some.Contains("Your thrall is already stunned!").IgnoreCase,
                        $"CMSS13 stun_thrall() rejects repeat use while IsStun() is true.\nActual labels:\n{joinedLabels}");
                });
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                SpawnLinkedThrall(entMan, map.GridCoords.Offset(new Vector2(4, 0)), out targetMaster, out targetMasterBracer, out targetThrall, out targetThrallBracer);
                server.PlayerMan.SetAttachedEntity(session, targetThrall);

                Assert.That(
                    entMan.System<YautjaThrallSystem>().TryStunLinkedThrall((targetMasterBracer, entMan.GetComponent<YautjaBracerComponent>(targetMasterBracer)), targetMaster),
                    Is.True);
                Assert.That(entMan.HasComponent<KnockedDownComponent>(targetThrall), Is.True,
                    "CMSS13 stun_thrall() applies 10 seconds of WEAKEN, represented locally as knockdown/paralyze.");
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels,
                    Has.Some.Contains("You feel a searing shock rip through your body! You fall to the ground in pain!").IgnoreCase,
                    $"CMSS13 stun_thrall() tells the thrall about the shock and fall.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, master, thrall, masterBracer, thrallBracer, targetMaster, targetThrall, targetMasterBracer, targetThrallBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StunThrallNoThrallUsesCmss13Denial()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid master = default;
        EntityUid masterBracer = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                master = SpawnHunterWithBracer(entMan, map.GridCoords, out masterBracer);
                server.PlayerMan.SetAttachedEntity(session, master);

                Assert.That(
                    entMan.System<YautjaThrallSystem>().TryStunLinkedThrall((masterBracer, entMan.GetComponent<YautjaBracerComponent>(masterBracer)), master),
                    Is.False);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                var joinedLabels = string.Join("\n", labels);
                Assert.That(
                    labels,
                    Has.Some.Contains("You have no thrall to punish!").IgnoreCase,
                    $"CMSS13 stun_thrall() uses a distinct no-thrall punishment denial.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, master, masterBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MarkedUnlinkedThrallCanBeStunnedLikeCmss13HunterDataThrall()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid master = default;
        EntityUid thrall = default;
        EntityUid masterBracer = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                master = SpawnHunterWithBracer(entMan, map.GridCoords, out masterBracer);
                thrall = SpawnMarkedThrall(entMan, master, masterBracer, map.GridCoords.Offset(new Vector2(1, 0)));
                server.PlayerMan.SetAttachedEntity(session, master);

                Assert.That(
                    entMan.System<YautjaThrallSystem>().TryStunLinkedThrall((masterBracer, entMan.GetComponent<YautjaBracerComponent>(masterBracer)), master),
                    Is.True,
                    "CMSS13 stun_thrall() reads master.hunter_data.thrall directly and does not require the thrall bracer link verb.");
                Assert.That(entMan.HasComponent<KnockedDownComponent>(thrall), Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                var joinedLabels = string.Join("\n", labels);
                Assert.That(
                    labels,
                    Has.Some.Contains("Your bracer beeps, your thrall is punished.").IgnoreCase,
                    $"CMSS13 stun_thrall() gives the source success text even when only the huntdata thrall link exists.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, master, thrall, masterBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemoteThrallSelfDestructUsesCmss13TextBroadcastAndLogs()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            AdminLogsEnabled = true,
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var adminLogs = server.ResolveDependency<IAdminLogManager>();

        EntityUid master = default;
        EntityUid thrall = default;
        EntityUid masterBracer = default;
        EntityUid thrallBracer = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;
        var expectedArea = string.Empty;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var areas = entMan.System<AreaSystem>();
                var loc = server.ResolveDependency<ILocalizationManager>();
                var metadata = entMan.System<MetaDataSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                SpawnLinkedThrall(entMan, map.GridCoords, out master, out masterBracer, out thrall, out thrallBracer);
                metadata.SetEntityName(master, "A'ke Ret");
                expectedArea = areas.GetAreaName(thrall);
                server.PlayerMan.SetAttachedEntity(session, master);

                var thrallBracerComp = entMan.GetComponent<YautjaThrallBracerComponent>(thrallBracer);
                thrallBracerComp.SelfDestructDelay = TimeSpan.FromSeconds(30);

                Assert.That(
                    entMan.System<YautjaThrallSystem>().TryToggleLinkedThrallSelfDestruct((masterBracer, entMan.GetComponent<YautjaBracerComponent>(masterBracer)), master),
                    Is.True);
                AcceptRemoteThrallSelfDestructDialog(entMan, thrall);
                Assert.That(thrallBracerComp.SelfDestructArmed, Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                var joinedLabels = string.Join("\n", labels);
                Assert.That(
                    labels,
                    Has.Some.Contains("You set the timer. They have failed you.").IgnoreCase,
                    $"CMSS13 self_destruct_thrall() tells the master they set the timer.\nActual labels:\n{joinedLabels}");
            });

            await client.WaitAssertion(() =>
            {
                var history = client.ResolveDependency<IUserInterfaceManager>()
                    .GetUIController<ChatUIController>()
                    .History
                    .Select(entry => entry.Msg)
                    .ToList();
                var joinedHistory = string.Join("\n", history.Select(message => $"{message.Channel}: {message.Message}"));

                Assert.That(
                    history.Any(message =>
                        message.Channel == ChatChannel.Radio &&
                        message.Message.Contains("A'ke Ret has triggered their thrall's self-destruction sequence.", StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"CMSS13 self_destruct_thrall() broadcasts the remote thrall self-destruction to Yautja.\nActual chat history:\n{joinedHistory}");
            });

            var logs = await adminLogs.CurrentRoundLogs(new LogFilter
            {
                Types = new HashSet<LogType> { LogType.Action },
            });
            var messages = logs.Select(log => log.Message).ToList();
            var joinedMessages = string.Join("\n", messages);
            Assert.That(
                messages.Any(message =>
                    message.Contains("triggered their thrall's self-destruct sequence", StringComparison.OrdinalIgnoreCase) &&
                    message.Contains($"in {expectedArea}", StringComparison.OrdinalIgnoreCase)),
                Is.True,
                $"CMSS13 self_destruct_thrall() logs '[key_name(master)] triggered their thrall's self-destruct sequence in [area]'.\nActual logs:\n{joinedMessages}");
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, master, thrall, masterBracer, thrallBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemoteThrallSelfDestructRequiresThrallConsentLikeCmss13TguiAlert()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid noMaster = default;
        EntityUid noThrall = default;
        EntityUid noMasterBracer = default;
        EntityUid noThrallBracer = default;
        EntityUid yesMaster = default;
        EntityUid yesThrall = default;
        EntityUid yesMasterBracer = default;
        EntityUid yesThrallBracer = default;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                SpawnLinkedThrall(entMan, map.GridCoords, out noMaster, out noMasterBracer, out noThrall, out noThrallBracer);
                var noThrallBracerComp = entMan.GetComponent<YautjaThrallBracerComponent>(noThrallBracer);
                noThrallBracerComp.SelfDestructDelay = TimeSpan.FromSeconds(30);

                Assert.That(
                    entMan.System<YautjaThrallSystem>().TryToggleLinkedThrallSelfDestruct((noMasterBracer, entMan.GetComponent<YautjaBracerComponent>(noMasterBracer)), noMaster),
                    Is.True,
                    "CMSS13 self_destruct_thrall() opens a tgui_alert on the thrall after guard checks.");
                AssertRemoteThrallSelfDestructDialog(entMan, noThrall);
                Assert.That(noThrallBracerComp.SelfDestructArmed, Is.False,
                    "CMSS13 self_destruct_thrall() must not arm until the thrall accepts the alert.");

                entMan.EventBus.RaiseLocalEvent(noThrall, new DialogOptionBuiMsg(1)
                {
                    Actor = noThrall,
                    UiKey = DialogUiKey.Key,
                });
                Assert.That(noThrallBracerComp.SelfDestructArmed, Is.False,
                    "CMSS13 self_destruct_thrall() returns when the thrall chooses No.");

                SpawnLinkedThrall(entMan, map.GridCoords.Offset(new Vector2(4, 0)), out yesMaster, out yesMasterBracer, out yesThrall, out yesThrallBracer);
                var yesThrallBracerComp = entMan.GetComponent<YautjaThrallBracerComponent>(yesThrallBracer);
                yesThrallBracerComp.SelfDestructDelay = TimeSpan.FromSeconds(30);

                Assert.That(
                    entMan.System<YautjaThrallSystem>().TryToggleLinkedThrallSelfDestruct((yesMasterBracer, entMan.GetComponent<YautjaBracerComponent>(yesMasterBracer)), yesMaster),
                    Is.True);
                AssertRemoteThrallSelfDestructDialog(entMan, yesThrall);

                entMan.EventBus.RaiseLocalEvent(yesThrall, new DialogOptionBuiMsg(0)
                {
                    Actor = yesThrall,
                    UiKey = DialogUiKey.Key,
                });
                Assert.That(yesThrallBracerComp.SelfDestructArmed, Is.True,
                    "CMSS13 self_destruct_thrall() arms only after the thrall answers Yes.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);
                DeleteAll(entMan, noMaster, noThrall, noMasterBracer, noThrallBracer, yesMaster, yesThrall, yesMasterBracer, yesThrallBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemoteThrallSelfDestructMasterStateGuardsUseCmss13Text()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid deadMaster = default;
        EntityUid deadThrall = default;
        EntityUid deadMasterBracer = default;
        EntityUid deadThrallBracer = default;
        EntityUid criticalMaster = default;
        EntityUid criticalThrall = default;
        EntityUid criticalMasterBracer = default;
        EntityUid criticalThrallBracer = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var mobState = entMan.System<MobStateSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                SpawnLinkedThrall(entMan, map.GridCoords, out deadMaster, out deadMasterBracer, out deadThrall, out deadThrallBracer);
                SpawnLinkedThrall(entMan, map.GridCoords.Offset(new Vector2(4, 0)), out criticalMaster, out criticalMasterBracer, out criticalThrall, out criticalThrallBracer);

                var thrallSystem = entMan.System<YautjaThrallSystem>();

                server.PlayerMan.SetAttachedEntity(session, deadMaster);
                mobState.ChangeMobState(deadMaster, MobState.Dead);
                Assert.That(
                    thrallSystem.TryToggleLinkedThrallSelfDestruct((deadMasterBracer, entMan.GetComponent<YautjaBracerComponent>(deadMasterBracer)), deadMaster),
                    Is.False,
                    "CMSS13 self_destruct_thrall() refuses a dead master before opening the thrall consent prompt.");
                Assert.That(entMan.HasComponent<DialogComponent>(deadThrall), Is.False);
                Assert.That(entMan.GetComponent<YautjaThrallBracerComponent>(deadThrallBracer).SelfDestructArmed, Is.False);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                var joinedLabels = string.Join("\n", labels);
                Assert.That(labels, Has.Some.Contains("Little too late for that now!").IgnoreCase,
                    $"CMSS13 self_destruct_thrall() uses the dead-master denial before any consent prompt.\nActual labels:\n{joinedLabels}");
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var mobState = entMan.System<MobStateSystem>();
                var session = server.PlayerMan.Sessions.Single();
                var thrallSystem = entMan.System<YautjaThrallSystem>();

                server.PlayerMan.SetAttachedEntity(session, criticalMaster);
                mobState.ChangeMobState(criticalMaster, MobState.Critical);
                Assert.That(
                    thrallSystem.TryToggleLinkedThrallSelfDestruct((criticalMasterBracer, entMan.GetComponent<YautjaBracerComponent>(criticalMasterBracer)), criticalMaster),
                    Is.False,
                    "CMSS13 self_destruct_thrall() refuses a critical master before opening the thrall consent prompt.");
                Assert.That(entMan.HasComponent<DialogComponent>(criticalThrall), Is.False);
                Assert.That(entMan.GetComponent<YautjaThrallBracerComponent>(criticalThrallBracer).SelfDestructArmed, Is.False);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                var joinedLabels = string.Join("\n", labels);
                Assert.That(labels,
                    Has.Some.Contains("As you fall into unconsciousness you fail to activate your self-destruct device before you collapse.").IgnoreCase,
                    $"CMSS13 self_destruct_thrall() uses the critical-master denial before any consent prompt.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(
                    entMan,
                    deadMaster,
                    deadThrall,
                    deadMasterBracer,
                    deadThrallBracer,
                    criticalMaster,
                    criticalThrall,
                    criticalMasterBracer,
                    criticalThrallBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemoteThrallSelfDestructUsesThrallAreaForHuntingPreserveLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid preserveMap = default;
        EntityUid preserveGrid = default;
        EntityUid master = default;
        EntityUid thrall = default;
        EntityUid masterBracer = default;
        EntityUid thrallBracer = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var mapSystem = entMan.System<SharedMapSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                preserveMap = mapSystem.CreateMap(out var preserveMapId);
                preserveGrid = server.MapMan.CreateGridEntity(preserveMapId).Owner;
                entMan.EnsureComponent<YautjaHuntingGroundComponent>(preserveGrid);

                SpawnLinkedThrall(entMan, map.GridCoords, out master, out masterBracer, out thrall, out thrallBracer);
                entMan.System<SharedTransformSystem>().SetCoordinates(thrall, new EntityCoordinates(preserveGrid, 0, 0));
                server.PlayerMan.SetAttachedEntity(session, master);

                Assert.That(
                    entMan.System<YautjaThrallSystem>().TryToggleLinkedThrallSelfDestruct((masterBracer, entMan.GetComponent<YautjaBracerComponent>(masterBracer)), master),
                    Is.False,
                    "CMSS13 self_destruct_thrall() checks the thrall's area for AREA_YAUTJA_HUNTING_GROUNDS, not the master's area.");
                Assert.That(entMan.GetComponent<YautjaThrallBracerComponent>(thrallBracer).SelfDestructArmed, Is.False);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                var joinedLabels = string.Join("\n", labels);
                Assert.That(
                    labels,
                    Has.Some.Contains("Your bracer will not allow you to activate a self-destruction sequence in order to protect the hunting preserve.").IgnoreCase,
                    $"CMSS13 self_destruct_thrall() uses the preserve denial when the thrall is in the preserve.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, master, thrall, masterBracer, thrallBracer, preserveGrid, preserveMap);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemoteThrallSelfDestructNoThrallUsesCmss13Denial()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid master = default;
        EntityUid masterBracer = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                master = SpawnHunterWithBracer(entMan, map.GridCoords, out masterBracer);
                entMan.EnsureComponent<YautjaHuntingGroundComponent>(map.Grid.Owner);
                server.PlayerMan.SetAttachedEntity(session, master);

                Assert.That(
                    entMan.System<YautjaThrallSystem>().TryToggleLinkedThrallSelfDestruct((masterBracer, entMan.GetComponent<YautjaBracerComponent>(masterBracer)), master),
                    Is.False);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                var joinedLabels = string.Join("\n", labels);
                Assert.That(
                    labels,
                    Has.Some.Contains("You have no thrall to destroy!").IgnoreCase,
                    $"CMSS13 self_destruct_thrall() uses a distinct no-thrall destruction denial.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, master, masterBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemoteThrallSelfDestructMarkedUnlinkedWornBracerArmsLikeCmss13HunterDataThrall()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid master = default;
        EntityUid thrall = default;
        EntityUid masterBracer = default;
        EntityUid thrallBracer = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                master = SpawnHunterWithBracer(entMan, map.GridCoords, out masterBracer);
                thrall = SpawnMarkedThrall(entMan, master, masterBracer, map.GridCoords.Offset(new Vector2(1, 0)));
                thrallBracer = entMan.SpawnEntity("CMUYautjaThrallBracer", map.GridCoords.Offset(new Vector2(1, 0)));
                Assert.That(entMan.System<InventorySystem>().TryEquip(thrall, thrallBracer, "gloves", silent: true, force: true), Is.True);

                var thrallBracerComp = entMan.GetComponent<YautjaThrallBracerComponent>(thrallBracer);
                thrallBracerComp.SelfDestructDelay = TimeSpan.Zero;

                Assert.That(
                    entMan.System<YautjaThrallSystem>().TryToggleLinkedThrallSelfDestruct((masterBracer, entMan.GetComponent<YautjaBracerComponent>(masterBracer)), master),
                    Is.True,
                    "CMSS13 self_destruct_thrall() uses master.hunter_data.thrall and should not require the local link-bracer action when the thrall is wearing a bracer.");
                AcceptRemoteThrallSelfDestructDialog(entMan, thrall);
                Assert.That(thrallBracerComp.SelfDestructArmed, Is.True);
            });

            await pair.RunTicksSync(5);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(entMan.Deleted(thrall), Is.True,
                    "CMSS13 self_destruct_thrall() still detonates a marked thrall with an unlinked worn thrall bracer.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                DeleteAll(entMan, master, thrall, masterBracer, thrallBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemoteThrallSelfDestructRepeatUseDoesNotCancelLikeCmss13ExplodingGuard()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid master = default;
        EntityUid thrall = default;
        EntityUid masterBracer = default;
        EntityUid thrallBracer = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                SpawnLinkedThrall(entMan, map.GridCoords, out master, out masterBracer, out thrall, out thrallBracer);

                var thrallBracerComp = entMan.GetComponent<YautjaThrallBracerComponent>(thrallBracer);
                thrallBracerComp.SelfDestructDelay = TimeSpan.FromSeconds(30);

                var thralls = entMan.System<YautjaThrallSystem>();
                Assert.That(
                    thralls.TryToggleLinkedThrallSelfDestruct((masterBracer, entMan.GetComponent<YautjaBracerComponent>(masterBracer)), master),
                    Is.True);
                AcceptRemoteThrallSelfDestructDialog(entMan, thrall);
                var armedAt = thrallBracerComp.SelfDestructAt;

                Assert.That(
                    thralls.TryToggleLinkedThrallSelfDestruct((masterBracer, entMan.GetComponent<YautjaBracerComponent>(masterBracer)), master),
                    Is.False,
                    "CMSS13 self_destruct_thrall() returns while exploding is true; the timer cannot be cancelled by repeating the verb.");
                Assert.That(thrallBracerComp.SelfDestructArmed, Is.True);
                Assert.That(thrallBracerComp.SelfDestructAt, Is.EqualTo(armedAt));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                DeleteAll(entMan, master, thrall, masterBracer, thrallBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemoteThrallSelfDestructDetonatesThrallLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid master = default;
        EntityUid thrall = default;
        EntityUid masterBracer = default;
        EntityUid thrallBracer = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                SpawnLinkedThrall(entMan, map.GridCoords, out master, out masterBracer, out thrall, out thrallBracer);

                var thrallBracerComp = entMan.GetComponent<YautjaThrallBracerComponent>(thrallBracer);
                thrallBracerComp.SelfDestructDelay = TimeSpan.Zero;

                Assert.That(
                    entMan.System<YautjaThrallSystem>().TryToggleLinkedThrallSelfDestruct((masterBracer, entMan.GetComponent<YautjaBracerComponent>(masterBracer)), master),
                    Is.True);
                AcceptRemoteThrallSelfDestructDialog(entMan, thrall);
            });

            await pair.RunTicksSync(5);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(
                    entMan.Deleted(thrall) || entMan.GetComponent<MobStateComponent>(thrall).CurrentState == MobState.Dead,
                    Is.True,
                    "CMSS13 self_destruct_thrall() cell_explosion/gib/qdel path kills and removes the thrall, not only the bracer.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                DeleteAll(entMan, master, thrall, masterBracer, thrallBracer);
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

    private static EntityUid SpawnBadBloodWithBracer(IEntityManager entMan, EntityCoordinates coordinates, out EntityUid bracer)
    {
        var hunter = entMan.SpawnEntity("CMMobHuman", coordinates);
        bracer = entMan.SpawnEntity("CMUYautjaBadBloodBracer", coordinates);
        entMan.EnsureComponent<YautjaComponent>(hunter);
        entMan.EnsureComponent<NpcFactionMemberComponent>(hunter).Factions.Add("CMUYautjaBadBlood");
        Assert.That(entMan.System<InventorySystem>().TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
        return hunter;
    }

    private static void SpawnLinkedThrall(
        IEntityManager entMan,
        EntityCoordinates coordinates,
        out EntityUid master,
        out EntityUid masterBracer,
        out EntityUid thrall,
        out EntityUid thrallBracer)
    {
        master = SpawnHunterWithBracer(entMan, coordinates, out masterBracer);
        thrall = SpawnMarkedThrall(entMan, master, masterBracer, coordinates.Offset(new Vector2(1, 0)));
        thrallBracer = entMan.SpawnEntity("CMUYautjaThrallBracer", coordinates.Offset(new Vector2(1, 0)));

        var inventory = entMan.System<InventorySystem>();
        Assert.That(inventory.TryEquip(thrall, thrallBracer, "gloves", silent: true, force: true), Is.True);
        Assert.That(
            entMan.System<YautjaThrallSystem>().TryLinkThrallBracer((masterBracer, entMan.GetComponent<YautjaBracerComponent>(masterBracer)), master),
            Is.True);
    }

    private static EntityUid SpawnMarkedThrall(
        IEntityManager entMan,
        EntityUid master,
        EntityUid masterBracer,
        EntityCoordinates coordinates)
    {
        var thrall = entMan.SpawnEntity("CMMobHuman", coordinates);
        Assert.That(
            entMan.System<YautjaMarkSystem>().TryMark((masterBracer, entMan.GetComponent<YautjaBracerComponent>(masterBracer)), master, thrall, YautjaMarkKind.Thrall, "claimed"),
            Is.True);
        return thrall;
    }

    private static void AssertRemoteThrallSelfDestructDialog(IEntityManager entMan, EntityUid thrall)
    {
        Assert.That(entMan.TryGetComponent(thrall, out DialogComponent? dialog), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(dialog!.Title, Is.EqualTo("Self Destruct Thrall"));
            Assert.That(dialog.Message.Text, Is.EqualTo("Are you sure you want to detonate this human's bracer? There is no stopping this process"));
            Assert.That(dialog.Options.Select(option => option.Text), Is.EqualTo(new[] { "Yes", "No" }));
        });
    }

    private static void AcceptRemoteThrallSelfDestructDialog(IEntityManager entMan, EntityUid thrall)
    {
        AssertRemoteThrallSelfDestructDialog(entMan, thrall);
        entMan.EventBus.RaiseLocalEvent(thrall, new DialogOptionBuiMsg(0)
        {
            Actor = thrall,
            UiKey = DialogUiKey.Key,
        });
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
