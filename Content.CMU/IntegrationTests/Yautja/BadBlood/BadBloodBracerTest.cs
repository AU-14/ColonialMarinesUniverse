using Content.Shared._RMC14.Actions;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Radio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-105 and ECS-103: the bad blood bracer talks on its own channel and carries no thrall surface
[TestFixture]
public sealed class BadBloodBracerTest
{
    private const string BadBloodBracer = "CMUYautjaBadBloodBracer";

    [Test]
    public async Task BadBloodBracerIsKeyedToTheBadBloodChannelOnly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bracer = entMan.SpawnEntity(BadBloodBracer, map.GridCoords);
            var channels = entMan.GetComponent<EncryptionKeyHolderComponent>(bracer).Channels;

            Assert.Multiple(() =>
            {
                Assert.That(channels.Any(channel => channel == "CMUYautjaBadBlood"), Is.True,
                    "The bad blood bracer must be on the bad blood channel.");
                Assert.That(channels.Any(channel => channel == "CMUYautja"), Is.False,
                    "The bad blood bracer must not hear the honourable hunters.");
                Assert.That(channels.Any(channel => channel == "CMUYautjaOverseer"), Is.False);
            });

            entMan.DeleteEntity(bracer);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodGetsNoThrallActionsOrThrallMessageUi()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid badBlood = default;
        EntityUid honourable = default;
        EntityUid wornBracer = default;
        EntityUid heldByBadBlood = default;
        EntityUid heldByHonourable = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();

            badBlood = SpawnBadBlood(entMan, map.GridCoords, out wornBracer);
            honourable = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(0, 1)));

            heldByBadBlood = entMan.SpawnEntity(BadBloodBracer, map.GridCoords);
            heldByHonourable = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(0, 1)));
            Assert.That(hands.TryPickupAnyHand(badBlood, heldByBadBlood), Is.True);
            Assert.That(hands.TryPickupAnyHand(honourable, heldByHonourable), Is.True);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<SharedRMCActionsSystem>();
            var ui = entMan.System<SharedUserInterfaceSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(actions.GetActionsWithEvent<YautjaRaiseThrallActionEvent>(badBlood).Any(), Is.False,
                    "A Bad Blood must not get Raise Thrall.");
                Assert.That(actions.GetActionsWithEvent<YautjaLinkThrallBracerActionEvent>(badBlood).Any(), Is.False,
                    "A Bad Blood must not get Link Thrall from a held bracer.");
                Assert.That(actions.GetActionsWithEvent<YautjaLinkThrallBracerActionEvent>(honourable).Any(), Is.True,
                    "Honourable hunters keep Link Thrall on a held bracer.");
                Assert.That(ui.HasUi(wornBracer, YautjaBracerUIKey.Key), Is.True,
                    "The bad blood bracer keeps its main panel.");
            });
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            foreach (var uid in new[] { badBlood, honourable, heldByBadBlood, heldByHonourable })
            {
                if (entMan.EntityExists(uid))
                    entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerPanelThrallCommandsDoNothingForBadBlood()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();

            var badBlood = SpawnBadBlood(entMan, map.GridCoords, out var badBloodBracer);
            var honourable = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(0, 2)));
            if (!inventory.TryGetSlotEntity(honourable, "gloves", out var honourableBracer))
            {
                honourableBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(0, 2)));
                Assert.That(inventory.TryEquip(honourable, honourableBracer.Value, "gloves", silent: true, force: true),
                    Is.True);
            }

            var badBloodThrall = SpawnThrallWithBracer(entMan, map.GridCoords.Offset(new Vector2(1, 0)), badBlood);
            var honourableThrall = SpawnThrallWithBracer(entMan, map.GridCoords.Offset(new Vector2(1, 2)), honourable);

            SendPanelCommand(entMan, badBloodBracer, badBlood, YautjaBracerPanelCommand.LinkThrallBracer);
            SendPanelCommand(entMan, honourableBracer.Value, honourable, YautjaBracerPanelCommand.LinkThrallBracer);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<YautjaThrallComponent>(badBloodThrall).BracerLinked, Is.False,
                    "The thrall page of the bracer panel must be dead for a Bad Blood.");
                Assert.That(entMan.GetComponent<YautjaThrallComponent>(honourableThrall).BracerLinked, Is.True,
                    "Honourable hunters still link a thrall from the bracer panel.");
            });

            foreach (var uid in new[] { badBlood, honourable, badBloodThrall, honourableThrall })
            {
                if (entMan.EntityExists(uid))
                    entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodCanUseAHivebreaker()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid? previousAttached = null;
        var spawned = new List<EntityUid>();

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var mobState = entMan.System<MobStateSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                // the hivebreaker is high-tier Bad Blood armory gear: a Bad Blood hunter uses it like any faction user
                var factionUser = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                entMan.EnsureComponent<YautjaTechAuthorizedComponent>(factionUser);
                entMan.EnsureComponent<NpcFactionMemberComponent>(factionUser).Factions.Add("CMUYautjaBadBlood");

                var badBlood = SpawnBadBlood(entMan, map.GridCoords.Offset(new Vector2(0, 2)), out _);
                entMan.EnsureComponent<NpcFactionMemberComponent>(badBlood).Factions.Add("CMUYautjaBadBlood");

                var allowedHivebreaker = entMan.SpawnEntity("CMUYautjaHivebreaker", map.GridCoords);
                var badBloodHivebreaker = entMan.SpawnEntity("CMUYautjaHivebreaker", map.GridCoords.Offset(new Vector2(0, 2)));
                                var allowedTarget = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(1, 0)));
                var deniedTarget = entMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(1, 2)));
                spawned.AddRange(new[]
                {
                    factionUser, badBlood, allowedHivebreaker, badBloodHivebreaker, allowedTarget,
                    deniedTarget,
                });

                mobState.ChangeMobState(allowedTarget, MobState.Critical);
                mobState.ChangeMobState(deniedTarget, MobState.Critical);

                server.PlayerMan.SetAttachedEntity(session, allowedTarget);
                var allowed = Interact(entMan, factionUser, allowedHivebreaker, allowedTarget);

                server.PlayerMan.SetAttachedEntity(session, deniedTarget);
                var badBloodUse = Interact(entMan, badBlood, badBloodHivebreaker, deniedTarget);
                

                Assert.Multiple(() =>
                {
                    Assert.That(allowed.Handled, Is.True, "Test setup: the hivebreaker itself must still work.");
                    Assert.That(badBloodUse.Handled, Is.True, "A Bad Blood hunter must be able to start a hivebreaker.");
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

                foreach (var uid in spawned)
                {
                    if (entMan.EntityExists(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    // a hunter marked as a Bad Blood, wearing a fresh bad blood bracer
    private static EntityUid SpawnBadBlood(IEntityManager entMan, EntityCoordinates coords, out EntityUid bracer)
    {
        var inventory = entMan.System<InventorySystem>();
        var badBlood = entMan.SpawnEntity("CMUMobYautja", coords);
        entMan.EnsureComponent<YautjaBadBloodComponent>(badBlood);

        if (inventory.TryUnequip(badBlood, "gloves", out var oldBracer, silent: true, force: true))
            entMan.DeleteEntity(oldBracer.Value);

        bracer = entMan.SpawnEntity(BadBloodBracer, coords);
        Assert.That(inventory.TryEquip(badBlood, bracer, "gloves", silent: true, force: true), Is.True);
        return badBlood;
    }

    private static EntityUid SpawnThrallWithBracer(IEntityManager entMan, EntityCoordinates coords, EntityUid master)
    {
        var thrall = entMan.SpawnEntity("CMMobHuman", coords);
        entMan.EnsureComponent<YautjaThrallComponent>(thrall).Master = master;

        var thrallBracer = entMan.SpawnEntity("CMUYautjaThrallBracer", coords);
        Assert.That(entMan.System<InventorySystem>().TryEquip(thrall, thrallBracer, "gloves", silent: true, force: true),
            Is.True);
        return thrall;
    }

    private static void SendPanelCommand(
        IEntityManager entMan,
        EntityUid bracer,
        EntityUid actor,
        YautjaBracerPanelCommand command)
    {
        var msg = new YautjaBracerPanelCommandMsg(command)
        {
            UiKey = YautjaBracerUIKey.Key,
            Actor = actor,
        };
        entMan.EventBus.RaiseLocalEvent(bracer, (object) msg);
    }

    private static AfterInteractEvent Interact(IEntityManager entMan, EntityUid user, EntityUid used, EntityUid target)
    {
        var interact = new AfterInteractEvent(
            user,
            used,
            target,
            entMan.GetComponent<TransformComponent>(target).Coordinates,
            true);
        entMan.EventBus.RaiseLocalEvent(used, interact);
        return interact;
    }
}
