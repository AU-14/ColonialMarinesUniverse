using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server._RMC14.Chat.Chat;
using Content.Client.Popups;
using Content.Server._CMU14.Yautja;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.NPC.Components;
using Content.Shared.Roles;
using Content.Shared.Speech;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Player;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaHivebreakerThrallRuntimeTest
{
    [Test]
    public async Task HivebreakerRejectsUncontrolledT0BannedAndAlreadyBadBloodXenosBeforeDoAfter()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid uncontrolled = default;
        EntityUid larva = default;
        EntityUid drone = default;
        EntityUid badBlood = default;
        EntityUid valid = default;
        EntityUid hivebreaker = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var mobState = entMan.System<MobStateSystem>();
                var transform = entMan.System<SharedTransformSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = SpawnBadBloodTechUser(entMan, map.GridCoords);
                hivebreaker = SpawnHeldHivebreaker(entMan, hands, hunter, map.GridCoords);
                uncontrolled = SpawnCriticalXeno(entMan, mobState, "CMXenoRunner", map.GridCoords.Offset(new Vector2(1, 0)));
                larva = SpawnCriticalXeno(entMan, mobState, "CMXenoLarva", map.GridCoords.Offset(new Vector2(2, 0)));
                drone = SpawnCriticalXeno(entMan, mobState, "CMXenoDrone", map.GridCoords.Offset(new Vector2(3, 0)));
                badBlood = SpawnCriticalXeno(entMan, mobState, "CMXenoRunner", map.GridCoords.Offset(new Vector2(4, 0)));
                valid = SpawnCriticalXeno(entMan, mobState, "CMXenoRunner", map.GridCoords.Offset(new Vector2(5, 0)));
                entMan.EnsureComponent<NpcFactionMemberComponent>(badBlood).Factions.Add("CMUYautjaBadBlood");

                Assert.That(StartHivebreaker(entMan, hunter, hivebreaker, uncontrolled), Is.False,
                    "CMSS13 rejects xenos without client before starting the enthrall do_after.");

                server.PlayerMan.SetAttachedEntity(session, larva);
                Assert.That(StartHivebreaker(entMan, hunter, hivebreaker, larva), Is.False,
                    "CMSS13 rejects XENO_T0_CASTES before starting the enthrall do_after.");

                server.PlayerMan.SetAttachedEntity(session, drone);
                Assert.That(StartHivebreaker(entMan, hunter, hivebreaker, drone), Is.False,
                    "CMSS13 rejects hivebreaker_banned_castes before starting the enthrall do_after.");

                server.PlayerMan.SetAttachedEntity(session, badBlood);
                Assert.That(StartHivebreaker(entMan, hunter, hivebreaker, badBlood), Is.False,
                    "CMSS13 rejects targets already in FACTION_YAUTJA_BADBLOOD before starting the do_after.");

                server.PlayerMan.SetAttachedEntity(session, valid);
                MoveNextTo(entMan, transform, hunter, valid);
                Assert.That(StartHivebreaker(entMan, hunter, hivebreaker, valid), Is.True,
                    "A controlled defeated non-banned xeno should still start the 3 second enthrall do_after.");

                Assert.Multiple(() =>
                {
                    Assert.That(ActiveHivebreakerDoAfters(entMan, hunter), Is.EqualTo(1));
                    Assert.That(entMan.HasComponent<DialogComponent>(valid), Is.False,
                        "The consent dialog opens only after the CMSS13 3 second do_after completes.");
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
                DeleteAll(entMan, hunter, hivebreaker, uncontrolled, larva, drone, badBlood, valid);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DirectHivebreakXenoRefusesHellhoundLikeCmss13HandleEnthrallOverride()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid hivebreaker = default;
        EntityUid hellhound = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var thralls = entMan.System<YautjaThrallSystem>();

                hunter = SpawnBadBloodTechUser(entMan, map.GridCoords);
                hivebreaker = entMan.SpawnEntity("CMUYautjaHivebreaker", map.GridCoords);
                hellhound = entMan.SpawnEntity("CMUMobYautjaHellhound", map.GridCoords.Offset(new Vector2(1, 0)));

                Assert.That(thralls.HivebreakXeno(
                    hunter,
                    hellhound,
                    hivebreaker,
                    entMan.GetComponent<YautjaHivebreakerComponent>(hivebreaker)), Is.False,
                    "CMSS13 /mob/living/carbon/xenomorph/hellhound/handle_enthrall() returns FALSE even if the generic enthrall path is reached.");

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<YautjaThrallComponent>(hellhound), Is.False,
                        "CMSS13 /mob/living/carbon/xenomorph/hellhound/handle_enthrall() returns FALSE even if the generic enthrall path is reached.");
                    Assert.That(entMan.HasComponent<YautjaHivebrokenXenoComponent>(hellhound), Is.False,
                        "Hellhounds should keep their native Yautja hound name/camera surface instead of gaining the hivebroken xeno name modifier.");
                    Assert.That(entMan.HasComponent<YautjaTechAuthorizedComponent>(hellhound), Is.False,
                        "A rejected Hellhound enthrall must not grant Yautja tech access.");
                    Assert.That(entMan.GetComponent<NpcFactionMemberComponent>(hellhound).Factions, Is.EquivalentTo(new[] { "CMUYautja" }),
                        "Rejected Hellhounds should remain in their original Yautja Hellhound faction.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                DeleteAll(server.EntMan, hunter, hivebreaker, hellhound);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HivebreakerSwapsHivemindForYautjaSpeechRecipientsLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid hivebreaker = default;
        EntityUid target = default;
        EntityUid ordinaryXeno = default;
        EntityUid yautja = default;
        EntityUid human = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var thralls = entMan.System<YautjaThrallSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = SpawnBadBloodTechUser(entMan, map.GridCoords);
                hivebreaker = entMan.SpawnEntity("CMUYautjaHivebreaker", map.GridCoords);
                target = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(1, 0)));
                ordinaryXeno = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(2, 0)));
                yautja = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));
                human = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4, 0)));
                entMan.EnsureComponent<YautjaComponent>(yautja);

                Assert.That(thralls.HivebreakXeno(
                    hunter,
                    target,
                    hivebreaker,
                    entMan.GetComponent<YautjaHivebreakerComponent>(hivebreaker)), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(ChatRecipientKept(entMan, server.PlayerMan, session, target, ordinaryXeno), Is.True,
                        "CMSS13 handle_enthrall() keeps LANGUAGE_XENOMORPH, so hivebroken xeno speech should still be understood by xenos.");
                    Assert.That(ChatRecipientKept(entMan, server.PlayerMan, session, target, yautja), Is.True,
                        "CMSS13 handle_enthrall() adds LANGUAGE_YAUTJA, so hivebroken xeno speech should be understood by Yautja.");
                    Assert.That(ChatRecipientKept(entMan, server.PlayerMan, session, target, human), Is.False,
                        "CMSS13 handle_enthrall() removes LANGUAGE_HIVEMIND and does not grant common-language speech to ordinary humans.");
                });

                entMan.RemoveComponent<YautjaThrallComponent>(target);

                Assert.Multiple(() =>
                {
                    Assert.That(ChatRecipientKept(entMan, server.PlayerMan, session, target, ordinaryXeno), Is.True,
                        "CMSS13 handle_dethrall() restores LANGUAGE_HIVEMIND while keeping LANGUAGE_XENOMORPH.");
                    Assert.That(ChatRecipientKept(entMan, server.PlayerMan, session, target, yautja), Is.False,
                        "CMSS13 handle_dethrall() removes LANGUAGE_YAUTJA from the former hivebroken xeno.");
                    Assert.That(ChatRecipientKept(entMan, server.PlayerMan, session, target, human), Is.False,
                        "An ordinary xeno should not become understandable to ordinary humans after dethrall.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(server.EntMan, hunter, hivebreaker, target, ordinaryXeno, yautja, human);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HivebreakerConsentRefusalAndTimeoutDoNotConvertOrConsumeUse()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid hivebreaker = default;
        EntityUid refusedTarget = default;
        EntityUid timeoutTarget = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var hands = entMan.System<SharedHandsSystem>();
                var mobState = entMan.System<MobStateSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                hunter = SpawnBadBloodTechUser(entMan, map.GridCoords);
                hivebreaker = SpawnHeldHivebreaker(entMan, hands, hunter, map.GridCoords);
                refusedTarget = SpawnCriticalXeno(entMan, mobState, "CMXenoRunner", map.GridCoords.Offset(new Vector2(1, 0)));
                timeoutTarget = SpawnCriticalXeno(entMan, mobState, "CMXenoRunner", map.GridCoords.Offset(new Vector2(2, 0)));
                server.PlayerMan.SetAttachedEntity(session, refusedTarget);

                Assert.That(StartHivebreaker(entMan, hunter, hivebreaker, refusedTarget), Is.True);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.5f));

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var transform = entMan.System<SharedTransformSystem>();
                AssertHivebreakerConsentDialog(entMan, refusedTarget);

                server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.Single(), hunter);
                MoveNextTo(entMan, transform, hunter, refusedTarget);
                entMan.EventBus.RaiseLocalEvent(refusedTarget, new DialogOptionBuiMsg(1)
                {
                    Actor = refusedTarget,
                    UiKey = DialogUiKey.Key,
                });

                AssertNoHivebreakConversion(entMan, refusedTarget);
                Assert.That(entMan.GetComponent<YautjaHivebreakerComponent>(hivebreaker).Uses, Is.EqualTo(1),
                    "CMSS13 does not consume the hivebreaker when the target refuses the consent prompt.");
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(labels,
                    Has.Some.Contains("The hivemind resists your attempt to break the connection! (This player does not wish to be a thrall.)").IgnoreCase,
                    $"CMSS13 notifies the hivebreaker user when the target does not answer Yes.\nActual labels:\n{joinedLabels}");
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;

                var session = server.PlayerMan.Sessions.Single();
                var transform = entMan.System<SharedTransformSystem>();
                var mobState = entMan.System<MobStateSystem>();
                server.PlayerMan.SetAttachedEntity(session, timeoutTarget);
                mobState.ChangeMobState(timeoutTarget, MobState.Critical);
                MoveNextTo(entMan, transform, hunter, timeoutTarget);
                Assert.That(StartHivebreaker(entMan, hunter, hivebreaker, timeoutTarget), Is.True);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.5f));

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                AssertHivebreakerConsentDialog(entMan, timeoutTarget);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(10.5f));

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                AssertNoHivebreakConversion(entMan, timeoutTarget);
                Assert.That(entMan.HasComponent<DialogComponent>(timeoutTarget), Is.False,
                    "CMSS13 consent prompt times out after 10 seconds.");
                Assert.That(entMan.GetComponent<YautjaHivebreakerComponent>(hivebreaker).Uses, Is.EqualTo(1),
                    "CMSS13 does not consume the hivebreaker when the target does not answer Yes.");
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
                DeleteAll(entMan, hunter, hivebreaker, refusedTarget, timeoutTarget);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HivebreakerFlowTextUsesCmss13SourceStrings()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;

        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var loc = server.ResolveDependency<ILocalizationManager>();
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                Assert.Multiple(() =>
                {
                    Assert.That(Loc.GetString("cmu-yautja-hivebreaker-denied"),
                        Is.EqualTo("You have no idea what you're doing with this thing."));
                    Assert.That(Loc.GetString("cmu-yautja-hivebreaker-already"),
                        Is.EqualTo("This serpent is already enthralled... what are you doing?"));
                    Assert.That(Loc.GetString("cmu-yautja-hivebreaker-requires-critical"),
                        Is.EqualTo("The target must be in a defeated state before you can enthrall them!"));
                    Assert.That(Loc.GetString("cmu-yautja-hivebreaker-start-self", ("target", "Runner")),
                        Is.EqualTo("You start to enthrall Runner."));
                    Assert.That(Loc.GetString("cmu-yautja-hivebreaker-cancel-self", ("target", "Runner")),
                        Is.EqualTo("You decide not to enthrall Runner."));
                    Assert.That(Loc.GetString("cmu-yautja-hivebreaker-finished-self", ("target", "Runner")),
                        Is.EqualTo("You have enthralled Runner!"));
                    Assert.That(Loc.GetString("cmu-yautja-hivebreaker-refused"),
                        Is.EqualTo("The hivemind resists your attempt to break the connection! (This player does not wish to be a thrall.)"));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                if (previousCulture != null)
                {
                    var loc = server.ResolveDependency<ILocalizationManager>();
                    loc.SetCulture(previousCulture);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HivebreakerConsentAcceptanceConvertsAndConsumesUseLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid target = default;
        EntityUid hivebreaker = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var mobState = entMan.System<MobStateSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = SpawnBadBloodTechUser(entMan, map.GridCoords);
                hivebreaker = SpawnHeldHivebreaker(entMan, hands, hunter, map.GridCoords);
                target = SpawnCriticalXeno(entMan, mobState, "CMXenoRunner", map.GridCoords.Offset(new Vector2(1, 0)));
                server.PlayerMan.SetAttachedEntity(session, target);
                Assert.That(StartHivebreaker(entMan, hunter, hivebreaker, target), Is.True);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.5f));

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                AssertHivebreakerConsentDialog(entMan, target);

                entMan.EventBus.RaiseLocalEvent(target, new DialogOptionBuiMsg(0)
                {
                    Actor = target,
                    UiKey = DialogUiKey.Key,
                });
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.TryGetComponent(target, out YautjaThrallComponent? thrall), Is.True);
                    Assert.That(thrall!.Master, Is.EqualTo(hunter));
                    Assert.That(thrall.Hivebroken, Is.True);
                    Assert.That(thrall.Blooded, Is.True);
                    Assert.That(thrall.TechAuthorized, Is.True);
                    Assert.That(entMan.HasComponent<YautjaTechAuthorizedComponent>(target), Is.True);
                    Assert.That(entMan.HasComponent<YautjaHivebrokenXenoComponent>(target), Is.True);
                    Assert.That(entMan.HasComponent<IgnoreXenoWeedsSlowdownComponent>(target), Is.True);
                    Assert.That(entMan.GetComponent<NpcFactionMemberComponent>(target).Factions, Is.EquivalentTo(new[] { "CMUYautja" }));
                    Assert.That(entMan.GetComponent<UserIFFComponent>(target).Factions, Does.Contain("FactionYautja"));
                    Assert.That(entMan.Deleted(hivebreaker) || entMan.IsQueuedForDeletion(hivebreaker), Is.True,
                        "CMSS13 decrements the single use and deletes the hivebreaker when uses reaches zero.");
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
                if (hivebreaker != default && !entMan.Deleted(hivebreaker))
                    entMan.DeleteEntity(hivebreaker);
                DeleteAll(entMan, hunter, target);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HivebreakerConversionMarksDishonoredLikeCmss13HandleEnthrall()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid hivebreaker = default;
        EntityUid target = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var hands = entMan.System<SharedHandsSystem>();
                var mobState = entMan.System<MobStateSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                hunter = SpawnBadBloodTechUser(entMan, map.GridCoords);
                hivebreaker = SpawnHeldHivebreaker(entMan, hands, hunter, map.GridCoords);
                target = SpawnCriticalXeno(entMan, mobState, "CMXenoRunner", map.GridCoords.Offset(new Vector2(1, 0)));
                server.PlayerMan.SetAttachedEntity(session, target);
                Assert.That(StartHivebreaker(entMan, hunter, hivebreaker, target), Is.True);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.5f));

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                entMan.EventBus.RaiseLocalEvent(target, new DialogOptionBuiMsg(0)
                {
                    Actor = target,
                    UiKey = DialogUiKey.Key,
                });
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var marks = entMan.System<YautjaMarkSystem>();

                Assert.Multiple(() =>
                {
                    Assert.That(marks.IsMarkedBy(target, YautjaMarkKind.Dishonored, target), Is.True,
                        "CMSS13 handle_enthrall() sets hunter_data.dishonored and dishonored_set = src on the enthralled xeno.");
                    Assert.That(marks.GetMarkReason(target, YautjaMarkKind.Dishonored), Is.EqualTo("Enthralled to a Bad Blood!"),
                        "CMSS13 handle_enthrall() overwrites the enthrall() reason with the final local dishonored reason.");
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
                if (previousCulture is { } culture)
                    loc.SetCulture(culture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                if (hivebreaker != default && !entMan.Deleted(hivebreaker))
                    entMan.DeleteEntity(hivebreaker);
                DeleteAll(entMan, hunter, target);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HivebreakerConversionMovesTargetToBadBloodHiveLikeCmss13SetHiveAndUpdate()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid hivebreaker = default;
        EntityUid target = default;
        EntityUid originalHive = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var mobState = entMan.System<MobStateSystem>();
                var hiveSystem = entMan.System<SharedXenoHiveSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = SpawnBadBloodTechUser(entMan, map.GridCoords);
                hivebreaker = SpawnHeldHivebreaker(entMan, hands, hunter, map.GridCoords);
                target = SpawnCriticalXeno(entMan, mobState, "CMXenoRunner", map.GridCoords.Offset(new Vector2(1, 0)));
                originalHive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(2, 0)));
                hiveSystem.SetHive(target, originalHive);

                server.PlayerMan.SetAttachedEntity(session, target);
                Assert.That(StartHivebreaker(entMan, hunter, hivebreaker, target), Is.True);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.5f));

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                entMan.EventBus.RaiseLocalEvent(target, new DialogOptionBuiMsg(0)
                {
                    Actor = target,
                    UiKey = DialogUiKey.Key,
                });
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hiveSystem = entMan.System<SharedXenoHiveSystem>();
                var hiveMember = entMan.GetComponent<HiveMemberComponent>(target);

                Assert.Multiple(() =>
                {
                    Assert.That(hiveMember.Hive, Is.Not.Null,
                        "CMSS13 enthrall() calls set_hive_and_update(XENO_HIVE_YAUTJA_BADBLOOD), not rogue/no-hive conversion.");
                    Assert.That(hiveMember.Hive, Is.Not.EqualTo(originalHive),
                        "CMSS13 moves the xeno out of its original hive when enthralled by a Bad Blood.");
                    Assert.That(hiveSystem.HasFaction(hiveMember.Hive!.Value, "CMUYautjaBadBlood"), Is.True,
                        "The local Bad Blood hive must be identifiable by the Bad Blood faction ally used by source-parity Bad Blood checks.");
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
                DeleteAll(entMan, hunter, hivebreaker, target, originalHive);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HivebreakerConversionAndRemovalUseCmss13TargetMessages()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid hivebreaker = default;
        EntityUid target = default;
        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var hands = entMan.System<SharedHandsSystem>();
                var mobState = entMan.System<MobStateSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));

                hunter = SpawnBadBloodTechUser(entMan, map.GridCoords);
                hivebreaker = SpawnHeldHivebreaker(entMan, hands, hunter, map.GridCoords);
                target = SpawnCriticalXeno(entMan, mobState, "CMXenoRunner", map.GridCoords.Offset(new Vector2(1, 0)));
                server.PlayerMan.SetAttachedEntity(session, target);
                Assert.That(StartHivebreaker(entMan, hunter, hivebreaker, target), Is.True);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.5f));

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                entMan.EventBus.RaiseLocalEvent(target, new DialogOptionBuiMsg(0)
                {
                    Actor = target,
                    UiKey = DialogUiKey.Key,
                });
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.Multiple(() =>
                {
                    Assert.That(labels, Has.Some.Contains("We have been enthralled by a Yautja Bad Blood!").IgnoreCase,
                        $"CMSS13 handle_enthrall() sends the high-danger enthrall notice.\nActual labels:\n{joinedLabels}");
                    Assert.That(labels, Has.Some.Contains("Our connection to the hivemind has been lost! We are now subservient to our master. Obey their commands.").IgnoreCase,
                        $"CMSS13 handle_enthrall() sends the hivemind-loss announcement.\nActual labels:\n{joinedLabels}");
                    Assert.That(labels, Has.Some.Contains("We are no longer able to evolve, or to harm our master.").IgnoreCase,
                        $"CMSS13 handle_enthrall() sends the evolution/master-harm warning.\nActual labels:\n{joinedLabels}");
                });
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                entMan.RemoveComponent<YautjaThrallComponent>(target);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.Multiple(() =>
                {
                    Assert.That(labels, Has.Some.Contains("We are no longer enthralled by a Yautja Bad Blood!").IgnoreCase,
                        $"CMSS13 handle_dethrall() sends the high-danger dethrall notice.\nActual labels:\n{joinedLabels}");
                    Assert.That(labels, Has.Some.Contains("Our connection to the hivemind has been restored!").IgnoreCase,
                        $"CMSS13 handle_dethrall() sends the hivemind-restored announcement.\nActual labels:\n{joinedLabels}");
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
                if (previousCulture is { } culture)
                    loc.SetCulture(culture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                if (hivebreaker != default && !entMan.Deleted(hivebreaker))
                    entMan.DeleteEntity(hivebreaker);
                DeleteAll(entMan, hunter, target);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovingHivebrokenThrallRestoresPreConversionRuntimeState()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid hivebreaker = default;
        EntityUid target = default;
        EntityUid hive = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var hands = entMan.System<SharedHandsSystem>();
                var mobState = entMan.System<MobStateSystem>();
                var hiveSystem = entMan.System<SharedXenoHiveSystem>();
                var iffSystem = entMan.System<GunIFFSystem>();
                var xenoSystem = entMan.System<XenoSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = SpawnBadBloodTechUser(entMan, map.GridCoords);
                hivebreaker = SpawnHeldHivebreaker(entMan, hands, hunter, map.GridCoords);
                target = SpawnCriticalXeno(entMan, mobState, "CMXenoRunner", map.GridCoords.Offset(new Vector2(1, 0)));
                hive = entMan.SpawnEntity("CMXenoHive", map.GridCoords.Offset(new Vector2(2, 0)));

                hiveSystem.SetHive(target, hive);
                entMan.EnsureComponent<NpcFactionMemberComponent>(target).Factions.Add("RMCXeno");
                iffSystem.AddUserFaction(target, "FactionXeno");
                var speech = entMan.EnsureComponent<SpeechComponent>(target);
                speech.SpeechVerb = "Default";
                speech.SpeechSounds = "Xeno";
                entMan.Dirty(target, speech);

                Assert.That(entMan.TryGetComponent(target, out XenoRegenComponent? regen), Is.True);
                xenoSystem.SetHealOffWeeds((target, regen), false);

                server.PlayerMan.SetAttachedEntity(session, target);
                Assert.That(StartHivebreaker(entMan, hunter, hivebreaker, target), Is.True);
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3.5f));

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                entMan.EventBus.RaiseLocalEvent(target, new DialogOptionBuiMsg(0)
                {
                    Actor = target,
                    UiKey = DialogUiKey.Key,
                });
            });

            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.HasComponent<YautjaThrallComponent>(target), Is.True);
            });

            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                entMan.RemoveComponent<YautjaThrallComponent>(target);
            });

            await pair.RunTicksSync(2);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var hiveMember = entMan.GetComponent<HiveMemberComponent>(target);
                var faction = entMan.GetComponent<NpcFactionMemberComponent>(target);
                var iff = entMan.GetComponent<UserIFFComponent>(target);
                var speech = entMan.GetComponent<SpeechComponent>(target);
                var regen = entMan.GetComponent<XenoRegenComponent>(target);

                Assert.Multiple(() =>
                {
                    Assert.That(hiveMember.Hive, Is.EqualTo(hive));
                    Assert.That(faction.Factions, Is.EquivalentTo(new[] { "RMCXeno" }));
                    Assert.That(iff.Factions, Is.EquivalentTo(new[] { "FactionXeno" }));
                    Assert.That(entMan.HasComponent<IgnoreXenoWeedsSlowdownComponent>(target), Is.False);
                    Assert.That(entMan.HasComponent<YautjaTechAuthorizedComponent>(target), Is.False);
                    Assert.That(entMan.HasComponent<YautjaHivebrokenXenoComponent>(target), Is.False);
                    Assert.That(speech.SpeechVerb, Is.EqualTo((ProtoId<SpeechVerbPrototype>) "Default"));
                    Assert.That(speech.SpeechSounds, Is.EqualTo((ProtoId<SpeechSoundsPrototype>) "Xeno"));
                    Assert.That(regen.HealOffWeeds, Is.False);
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
                DeleteAll(entMan, hunter, hivebreaker, target, hive);
            });
        }

        await pair.CleanReturnAsync();
    }

    private static EntityUid SpawnBadBloodTechUser(IEntityManager entMan, EntityCoordinates coordinates)
    {
        var user = entMan.SpawnEntity("CMMobHuman", coordinates);
        entMan.EnsureComponent<YautjaTechAuthorizedComponent>(user);
        entMan.EnsureComponent<NpcFactionMemberComponent>(user).Factions.Add("CMUYautjaBadBlood");
        return user;
    }

    private static EntityUid SpawnHeldHivebreaker(
        IEntityManager entMan,
        SharedHandsSystem hands,
        EntityUid hunter,
        EntityCoordinates coordinates)
    {
        var hivebreaker = entMan.SpawnEntity("CMUYautjaHivebreaker", coordinates);
        Assert.That(hands.TryPickupAnyHand(hunter, hivebreaker), Is.True);
        return hivebreaker;
    }

    private static EntityUid SpawnCriticalXeno(
        IEntityManager entMan,
        MobStateSystem mobState,
        string prototype,
        EntityCoordinates coordinates)
    {
        var xeno = entMan.SpawnEntity(prototype, coordinates);
        mobState.ChangeMobState(xeno, MobState.Critical);
        return xeno;
    }

    private static bool StartHivebreaker(
        IEntityManager entMan,
        EntityUid hunter,
        EntityUid hivebreaker,
        EntityUid target)
    {
        var ev = new AfterInteractEvent(
            hunter,
            hivebreaker,
            target,
            entMan.GetComponent<TransformComponent>(target).Coordinates,
            true);
        entMan.EventBus.RaiseLocalEvent(hivebreaker, ev);
        return ev.Handled;
    }

    private static void MoveNextTo(
        IEntityManager entMan,
        SharedTransformSystem transform,
        EntityUid mover,
        EntityUid target)
    {
        var targetCoordinates = entMan.GetComponent<TransformComponent>(target).Coordinates;
        transform.SetCoordinates(mover, targetCoordinates.Offset(new Vector2(-1, 0)));
    }

    private static int ActiveHivebreakerDoAfters(IEntityManager entMan, EntityUid user)
    {
        return entMan.TryGetComponent(user, out DoAfterComponent? doAfter)
            ? doAfter.DoAfters.Values.Count(active =>
                !active.Cancelled &&
                !active.Completed &&
                active.Args.Event is YautjaHivebreakerDoAfterEvent)
            : 0;
    }

    private static void AssertHivebreakerConsentDialog(IEntityManager entMan, EntityUid target)
    {
        Assert.That(entMan.TryGetComponent(target, out DialogComponent? dialog), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(dialog!.Title, Is.EqualTo("Submit?"));
            Assert.That(dialog.Message.Text, Is.EqualTo("Do you wish to be Enthralled by the Bad Blood?"));
            Assert.That(dialog.Options.Select(option => option.Text), Is.EqualTo(new[] { "Yes", "No" }));
            Assert.That(dialog.CloseAt, Is.Not.Null);
        });
    }

    private static void AssertNoHivebreakConversion(IEntityManager entMan, EntityUid target)
    {
        Assert.Multiple(() =>
        {
            Assert.That(entMan.HasComponent<YautjaThrallComponent>(target), Is.False);
            Assert.That(entMan.HasComponent<YautjaHivebrokenXenoComponent>(target), Is.False);
            Assert.That(entMan.HasComponent<YautjaTechAuthorizedComponent>(target), Is.False);
        });
    }

    private static bool ChatRecipientKept(
        IEntityManager entMan,
        ISharedPlayerManager playerManager,
        ICommonSession session,
        EntityUid source,
        EntityUid listener)
    {
        playerManager.SetAttachedEntity(session, listener);
        var recipients = new Dictionary<ICommonSession, ChatSystem.ICChatRecipientData>
        {
            [session] = new(1, false)
        };
        var ev = new ChatMessageAfterGetRecipients(recipients);
        entMan.EventBus.RaiseLocalEvent(source, ref ev);
        return recipients.ContainsKey(session);
    }

    private static void DeleteAll(IEntityManager entMan, params EntityUid[] entities)
    {
        foreach (var uid in entities)
        {
            if (uid != default && !entMan.Deleted(uid))
                entMan.DeleteEntity(uid);
        }
    }
}
