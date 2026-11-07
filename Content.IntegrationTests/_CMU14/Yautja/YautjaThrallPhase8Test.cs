using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client.Popups;
using Content.Client.UserInterface.Systems.Chat;
using Content.Server.CMU14.Yautja;
using Content.Server.Administration.Logs;
using Content.Shared.CMU14.Yautja;
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

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaThrallPhase8Test
{

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
    public async Task MarkPanelRejectsNullRequiredReasonsButTrustedNullMarksStillApply()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var marks = entMan.System<YautjaMarkSystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();

            var hunter = SpawnHunterWithBracer(entMan, map.GridCoords, out var bracer);
            var trustedHunter = SpawnHunterWithBracer(
                entMan,
                map.GridCoords.Offset(new Vector2(-1, 0)),
                out var trustedBracer);
            var networkThrall = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var networkBlooded = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var trustedTarget = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));
            var preyTarget = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4, 0)));
            var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
            var trustedBracerComp = entMan.GetComponent<YautjaBracerComponent>(trustedBracer);

            try
            {
                Assert.That(marks.TryOpenMarkPanel((bracer, bracerComp), hunter), Is.True);
                void SendPanelMark(EntityUid target, YautjaMarkKind kind)
                {
                    ui.RaiseUiMessage(
                        bracer,
                        YautjaMarkUIKey.Key,
                        YautjaTestMarkMessages.Mark(entMan, hunter, target, kind, null));
                }

                SendPanelMark(networkThrall, YautjaMarkKind.Thrall);
                Assert.That(marks.IsMarkedBy(networkThrall, YautjaMarkKind.Thrall, hunter), Is.False);

                Assert.That(
                    marks.TryMark((bracer, bracerComp), hunter, networkBlooded, YautjaMarkKind.Thrall, "setup"),
                    Is.True);
                SendPanelMark(networkBlooded, YautjaMarkKind.Blooded);

                Assert.That(
                    marks.TryMark(
                        (trustedBracer, trustedBracerComp),
                        trustedHunter,
                        trustedTarget,
                        YautjaMarkKind.Thrall,
                        null),
                    Is.True);
                Assert.That(
                    marks.TryMark(
                        (trustedBracer, trustedBracerComp),
                        trustedHunter,
                        trustedTarget,
                        YautjaMarkKind.Blooded,
                        null),
                    Is.True);

                SendPanelMark(preyTarget, YautjaMarkKind.Prey);

                Assert.Multiple(() =>
                {
                    Assert.That(marks.IsMarkedBy(networkThrall, YautjaMarkKind.Thrall, hunter), Is.False);
                    Assert.That(marks.IsMarkedBy(networkBlooded, YautjaMarkKind.Blooded, hunter), Is.False);
                    Assert.That(marks.IsMarkedBy(trustedTarget, YautjaMarkKind.Blooded, trustedHunter), Is.True);
                    Assert.That(marks.IsMarkedBy(preyTarget, YautjaMarkKind.Prey, hunter), Is.True);
                });
            }
            finally
            {
                DeleteAll(
                    entMan,
                    hunter,
                    bracer,
                    trustedHunter,
                    trustedBracer,
                    networkThrall,
                    networkBlooded,
                    trustedTarget,
                    preyTarget);
            }
        });

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
    public async Task RemoteThrallSelfDestructAsksTheMasterNotTheThrall()
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
                    "The master gets the confirm, the thrall must not be able to refuse.");
                AssertRemoteThrallSelfDestructDialog(entMan, noMaster);
                Assert.That(entMan.HasComponent<DialogComponent>(noThrall), Is.False);
                Assert.That(noThrallBracerComp.SelfDestructArmed, Is.False,
                    "Nothing arms until the master answers Yes.");

                entMan.EventBus.RaiseLocalEvent(noMaster, new DialogOptionBuiMsg(1)
                {
                    Actor = noMaster,
                    UiKey = DialogUiKey.Key,
                });
                Assert.That(noThrallBracerComp.SelfDestructArmed, Is.False,
                    "The master choosing No cancels.");

                SpawnLinkedThrall(entMan, map.GridCoords.Offset(new Vector2(4, 0)), out yesMaster, out yesMasterBracer, out yesThrall, out yesThrallBracer);
                var yesThrallBracerComp = entMan.GetComponent<YautjaThrallBracerComponent>(yesThrallBracer);
                yesThrallBracerComp.SelfDestructDelay = TimeSpan.FromSeconds(30);

                Assert.That(
                    entMan.System<YautjaThrallSystem>().TryToggleLinkedThrallSelfDestruct((yesMasterBracer, entMan.GetComponent<YautjaBracerComponent>(yesMasterBracer)), yesMaster),
                    Is.True);
                AssertRemoteThrallSelfDestructDialog(entMan, yesMaster);

                entMan.EventBus.RaiseLocalEvent(yesMaster, new DialogOptionBuiMsg(0)
                {
                    Actor = yesMaster,
                    UiKey = DialogUiKey.Key,
                });
                Assert.That(yesThrallBracerComp.SelfDestructArmed, Is.True,
                    "The master answering Yes arms the thrall's bracer.");
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
                preserveGrid = mapSystem.CreateGridEntity(preserveMapId).Owner;
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
                AcceptRemoteThrallSelfDestructDialog(entMan, master);
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
                AcceptRemoteThrallSelfDestructDialog(entMan, master);
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

    private static void AssertRemoteThrallSelfDestructDialog(IEntityManager entMan, EntityUid master)
    {
        Assert.That(entMan.TryGetComponent(master, out DialogComponent? dialog), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(dialog!.Title, Is.EqualTo("Self Destruct Thrall"));
            Assert.That(dialog.Message.Text, Is.EqualTo("Are you sure you want to detonate this human's bracer? There is no stopping this process"));
            Assert.That(dialog.Options.Select(option => option.Text), Is.EqualTo(new[] { "Yes", "No" }));
        });
    }

    private static void AcceptRemoteThrallSelfDestructDialog(IEntityManager entMan, EntityUid master)
    {
        AssertRemoteThrallSelfDestructDialog(entMan, master);
        entMan.EventBus.RaiseLocalEvent(master, new DialogOptionBuiMsg(0)
        {
            Actor = master,
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
