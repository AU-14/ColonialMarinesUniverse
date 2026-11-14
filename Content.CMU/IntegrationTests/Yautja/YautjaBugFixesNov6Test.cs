using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Yautja;
using Content.Server.Database;
using Content.Server.Examine;
using Content.Server.GameTicking;
using Content.Server.Players.JobWhitelist;
using Content.Shared.ActionBlocker;
using Content.Shared.Cuffs;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared.Actions.Components;
using Content.Shared.Blocking.Components;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaBugFixesNov6Test
{
    [Test]
    public async Task BrokenBracerShieldCanBeRedeployedAndRemoved()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var attachments = entMan.System<YautjaAttachmentSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var holder = entMan.SpawnEntity("CMUYautjaBracerShieldAttachment", map.GridCoords);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleShield", MapCoordinates.Nullspace);

            entMan.EnsureComponent<YautjaComponent>(hunter);
            Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
            Assert.That(hands.TryPickupAnyHand(hunter, holder), Is.True);

            var install = new InteractUsingEvent(hunter, holder, bracer, entMan.GetComponent<TransformComponent>(bracer).Coordinates);
            entMan.EventBus.RaiseLocalEvent(bracer, install);
            PickDialogOption(entMan, bracer, hunter, "Left");

            entMan.GetComponent<YautjaBracerComponent>(bracer).Charge = 300;
            var stored = entMan.GetComponent<YautjaStoredGearComponent>(holder);
            var actionComp = entMan.GetComponent<ActionComponent>(action);

            void Toggle()
            {
                entMan.EventBus.RaiseLocalEvent(bracer, new YautjaToggleShieldActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                });
            }

            Toggle();
            var shield = stored.AttachedWeapon!.Value;
            Assert.That(hands.IsHolding(hunter, shield), Is.True);

            // Z on the deployed shield raises it like a cmss13 riot shield instead of retracting it
            var use = new UseInHandEvent(hunter);
            entMan.EventBus.RaiseLocalEvent(shield, use);
            Assert.Multiple(() =>
            {
                Assert.That(use.Handled, Is.True);
                Assert.That(entMan.GetComponent<BlockingComponent>(shield).IsRaised, Is.True);
                Assert.That(hands.IsHolding(hunter, shield), Is.True);
            });

            // broken in hand: the holder has to let go of it instead of staying "deployed" forever
            entMan.DeleteEntity(shield);
            Assert.Multiple(() =>
            {
                Assert.That(stored.Deployed, Is.False);
                Assert.That(stored.AttachedWeapon, Is.Null);
            });

            Toggle();
            Assert.That(stored.AttachedWeapon, Is.Not.Null, "redeploying makes a fresh shield");
            var fresh = stored.AttachedWeapon!.Value;
            Assert.That(hands.IsHolding(hunter, fresh), Is.True);

            Toggle();
            Assert.That(stored.Deployed, Is.False);
            Assert.That(attachments.TryRemoveBracerAttachments((bracer, entMan.GetComponent<YautjaGearContainerComponent>(bracer)), hunter), Is.True);
            Assert.That(hands.IsHolding(hunter, holder), Is.True, "the attachment comes back out of the bracer");

            entMan.DeleteEntity(hunter);
            entMan.DeleteEntity(action);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MarkedTargetShowsWhoMarkedItToHuntersOnly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var marks = entMan.System<YautjaMarkSystem>();
            var examine = entMan.System<ExamineSystem>();

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            entMan.System<MetaDataSystem>().SetEntityName(hunter, "Kha'ta");
            var otherHunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords);

            marks.ForceMark(hunter, target, YautjaMarkKind.Prey);
            marks.ForceMark(hunter, target, YautjaMarkKind.Dishonored, reason: "shot me in the back");

            var seen = examine.GetExamineText(target, otherHunter).ToString();
            var human = examine.GetExamineText(target, marine).ToString();

            Assert.Multiple(() =>
            {
                Assert.That(seen, Does.Contain("is being hunted by Kha'ta"));
                Assert.That(seen, Does.Contain("dishonorable by Kha'ta for 'shot me in the back'"));
                Assert.That(human, Does.Not.Contain("Kha'ta"), "only hunters read the marks");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HarpoonFitsHuntingPouchAndTrophyBelt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var storage = entMan.System<SharedStorageSystem>();

            var pouch = entMan.SpawnEntity("CMUYautjaHuntingPouch", map.GridCoords);
            var belt = entMan.SpawnEntity("CMUYautjaTrophyBelt", map.GridCoords);
            var harpoon = entMan.SpawnEntity("CMUYautjaHarpoon", map.GridCoords);
            var sword = entMan.SpawnEntity("CMUYautjaClanSword", map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(storage.CanInsert(pouch, harpoon, out var pouchReason), Is.True, pouchReason);
                Assert.That(storage.CanInsert(belt, harpoon, out var beltReason), Is.True, beltReason);
                Assert.That(storage.CanInsert(belt, sword, out _), Is.False, "the belt still only takes trophies");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PounceIntoYautjaFrontGetsBodySlammed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var transform = entMan.System<SharedTransformSystem>();

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            transform.SetLocalRotation(hunter, Angle.Zero); // facing south

            // the 75% roll is predicted per (yautja, xeno), so a handful of runners covers both outcomes
            var slammed = 0;
            var passed = 0;
            for (var i = 0; i < 12; i++)
            {
                var runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(0, -2)));
#pragma warning disable RA0002
                entMan.EnsureComponent<XenoLeapingComponent>(runner).Origin = map.GridCoords.Offset(new Vector2(0, -2));
#pragma warning restore RA0002
                var ev = new XenoLeapHitAttempt(runner);
                entMan.EventBus.RaiseLocalEvent(hunter, ref ev);

                if (ev.Cancelled)
                {
                    slammed++;
                    Assert.That(entMan.HasComponent<StunnedComponent>(runner), Is.True, "a body slammed xeno is stunned");
                }
                else
                {
                    passed++;
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(slammed, Is.GreaterThan(0));
                Assert.That(passed, Is.GreaterThan(0));
            });

            // from behind there's no slam at all
            transform.SetLocalRotation(hunter, Angle.FromDegrees(180));
            for (var i = 0; i < 6; i++)
            {
                var runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(0, -2)));
#pragma warning disable RA0002
                entMan.EnsureComponent<XenoLeapingComponent>(runner).Origin = map.GridCoords.Offset(new Vector2(0, -2));
#pragma warning restore RA0002
                var ev = new XenoLeapHitAttempt(runner);
                entMan.EventBus.RaiseLocalEvent(hunter, ref ev);
                Assert.That(ev.Cancelled, Is.False);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GauntletComboOnlyStacksOnLivingMobs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mobState = entMan.System<MobStateSystem>();

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var gauntlet = entMan.SpawnEntity("CMUYautjaChainGauntlet", map.GridCoords);
            var wall = entMan.SpawnEntity("CMUYautjaHarpoon", map.GridCoords);
            var corpse = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            mobState.ChangeMobState(corpse, MobState.Dead);

            var comp = entMan.GetComponent<YautjaChainGauntletComponent>(gauntlet);

            void Hit(EntityUid target)
            {
                var ev = new MeleeHitEvent(new List<EntityUid> { target }, hunter, gauntlet, new DamageSpecifier(), null);
                entMan.EventBus.RaiseLocalEvent(gauntlet, ev);
            }

            Hit(wall);
            Hit(corpse);
            Assert.That(comp.ComboCounter, Is.Zero, "items and corpses don't farm the combo");

            Hit(marine);
            Hit(marine);
            Assert.That(comp.ComboCounter, Is.EqualTo(2));
            Assert.That(comp.HelpFinisherKnockdown, Is.EqualTo(TimeSpan.FromSeconds(2)), "cmss13 KnockDown(1) is 2s");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntPreyScatterOverThePreserve()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var before = new HashSet<EntityUid>();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            entMan.EnsureComponent<YautjaComponent>(hunter);
            var flightConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipFlightConsoleOverwatchSouthOffset0x13", map.GridCoords);
            var huntsmasterConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipHuntsmastersConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);

            entMan.EventBus.RaiseLocalEvent(flightConsole, new YautjaHuntingGroundSelectedEvent(entMan.GetNetEntity(hunter), "jungle_moon"));

            var query = entMan.EntityQueryEnumerator<YautjaHuntPreyComponent>();
            while (query.MoveNext(out var uid, out _))
                before.Add(uid);

            entMan.EventBus.RaiseLocalEvent(huntsmasterConsole, new YautjaHuntCallSelectedEvent(entMan.GetNetEntity(hunter), "mixed_small"));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var transform = entMan.System<SharedTransformSystem>();
            var spots = new HashSet<Vector2i>();
            var count = 0;
            var query = entMan.EntityQueryEnumerator<YautjaHuntPreyComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                if (before.Contains(uid))
                    continue;

                count++;
                var pos = transform.GetMapCoordinates(uid).Position;
                spots.Add(new Vector2i((int) MathF.Floor(pos.X), (int) MathF.Floor(pos.Y)));
            }

            Assert.That(count, Is.EqualTo(4));
            Assert.That(spots, Has.Count.EqualTo(count), "every prey lands on its own spawn marker");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WhitelistedGhostCanJoinTheHuntOnce()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            InLobby = true,
            Dirty = true,
            DummyTicker = false,
        });
        var server = pair.Server;
        var player = pair.Player!;
        var ticker = server.System<GameTicker>();
        var round = server.System<YautjaPredatorRoundSystem>();
        var ghostJoin = server.System<YautjaGhostJoinHuntSystem>();
        var jobWhitelist = server.ResolveDependency<JobWhitelistManager>();

        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(YautjaPredatorRoundCVars.RandomEnabled, false);
            server.CfgMan.SetCVar(YautjaStrandedCVars.Chance, 0f);
            ticker.StartRound();
        });
        await pair.RunTicksSync(10);

        await server.WaitPost(() => ticker.JoinAsObserver(player));
        await pair.RunTicksSync(10);

        EntityUid ghost = default;
        await server.WaitAssertion(() =>
        {
            ghost = player.AttachedEntity!.Value;
            Assert.That(server.EntMan.HasComponent<GhostComponent>(ghost), Is.True);

            // no hunt yet, no button
            ghostJoin.Refresh(ghost, player);
            Assert.That(server.EntMan.HasComponent<YautjaGhostJoinHuntComponent>(ghost), Is.False);

            Assert.That(round.TryInitializePredatorRound(out _), Is.True);
            ghostJoin.Refresh(ghost, player);
            Assert.That(server.EntMan.HasComponent<YautjaGhostJoinHuntComponent>(ghost), Is.False, "not whitelisted");
            Assert.That(round.CanGhostJoinHunt(player, out _), Is.False);
        });
        await pair.RunTicksSync(30);

        var db = server.ResolveDependency<IServerDbManager>();
        await db.SetYautjaWhitelistFlagsAsync(player.UserId.UserId, (int) YautjaWhitelistFlags.Yautja);
        await jobWhitelist.RefreshYautjaWhitelist(player.UserId);
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            ghostJoin.Refresh(ghost, player);
            Assert.That(server.EntMan.HasComponent<YautjaGhostJoinHuntComponent>(ghost), Is.True);
            Assert.That(round.CanGhostJoinHunt(player, out var reason), Is.True, reason);
            Assert.That(round.TryGhostJoinHunt(player, out reason), Is.True, reason);
        });
        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var mob = player.AttachedEntity!.Value;
            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.HasComponent<YautjaComponent>(mob), Is.True);
                Assert.That(server.EntMan.Deleted(ghost) || server.EntMan.IsQueuedForDeletion(ghost), Is.True, "the old ghost goes away");
                Assert.That(round.CanGhostJoinHunt(player, out _), Is.False, "once per round, like cmss13");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RestrainedXenoCannotTackleAYautja()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var blocker = entMan.System<ActionBlockerSystem>();
            var cuffs = entMan.System<SharedCuffableSystem>();

            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(1, 0)));
            var restraints = entMan.SpawnEntity("CMUHunterShipPlacedZiptiesLegcuffSouthVariant02Offset0x13", map.GridCoords);

            Assert.That(blocker.CanAttack(runner, hunter, disarm: true), Is.True);

            // cmss13 legcuffed xenos can't tackle a headhunter. cmu xeno restraints bind the whole xeno, so the
            // disarm (and with it the tackle) is refused before TackleSystem ever sees it
            Assert.That(cuffs.TryAddNewCuffs(runner, hunter, restraints), Is.True);
            Assert.That(blocker.CanAttack(runner, hunter, disarm: true), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    private static void PickDialogOption(IEntityManager entMan, EntityUid target, EntityUid actor, string optionText)
    {
        var dialog = entMan.GetComponent<DialogComponent>(target);
        var index = dialog.Options
            .Select((option, i) => (option, i))
            .Single(pair => pair.option.Text == optionText)
            .i;

        entMan.EventBus.RaiseLocalEvent(target, new DialogOptionBuiMsg(index)
        {
            Actor = actor,
            UiKey = DialogUiKey.Key,
        });
    }
}
