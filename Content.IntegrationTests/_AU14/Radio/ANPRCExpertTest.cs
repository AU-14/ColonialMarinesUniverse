using Content.Server.CMU14.Radio;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.CMU14.Radio;
using Content.Shared.Inventory;
using Content.Shared.PowerCell;
using Content.Shared.Preferences;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Radio;

// the faceplate-only techniques, driven through the same BUI messages the faceplate sends. each one
// pins the trade it is supposed to make, so a later balance pass cannot quietly turn a technique
// into a free lunch or a trap
[TestFixture]
public sealed class ANPRCExpertTest
{
    private static readonly EntProtoId FilledPack = "ANPRC117GRadioFilled";
    private static readonly ProtoId<JobPrototype> Rifleman = "AU14JobGOVFORSquadRifleman";

    private static readonly ProtoId<RadioChannelPrototype> Alpha = "radioGovforAlpha";
    private static readonly ProtoId<RadioChannelPrototype> Command = "radioGovforCommand";

    private const string BackSlot = "back";

    [Test]
    public async Task PowerSaveAndEmconStretchTheCell()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid normal = default, saving = default, silent = default;
        var start = new Dictionary<EntityUid, float>();

        await server.WaitAssertion(() =>
        {
            var ui = server.System<SharedUserInterfaceSystem>();

            (_, normal) = SpawnOperator(server, testMap.GridCoords, FilledPack, trained: false);
            (var savingWearer, saving) = SpawnOperator(server, testMap.GridCoords, FilledPack, trained: false);
            (var silentWearer, silent) = SpawnOperator(server, testMap.GridCoords, FilledPack, trained: false);

            ui.OpenUi(saving, ANPRCRadioUI.Key, savingWearer);
            ui.RaiseUiMessage(saving, ANPRCRadioUI.Key, new ANPRCSetPowerSaveMsg(true) { Actor = savingWearer });

            ui.OpenUi(silent, ANPRCRadioUI.Key, silentWearer);
            ui.RaiseUiMessage(silent, ANPRCRadioUI.Key, new ANPRCSetEmconMsg(true) { Actor = silentWearer });

            foreach (var pack in new[] { normal, saving, silent })
            {
                start[pack] = Charge(server, pack);
            }
        });

        await pair.RunSeconds(10);

        await server.WaitAssertion(() =>
        {
            var normalUsed = start[normal] - Charge(server, normal);
            var savingUsed = start[saving] - Charge(server, saving);
            var silentUsed = start[silent] - Charge(server, silent);

            Assert.Multiple(() =>
            {
                Assert.That(normalUsed, Is.GreaterThan(0f), "a switched-on set drains");
                Assert.That(savingUsed, Is.LessThan(normalUsed * 0.75f), "power save costs clearly less");
                Assert.That(silentUsed, Is.LessThan(savingUsed), "EMCON costs less again");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelayingCostsExtra()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid relaying = default, idle = default;
        float relayStart = 0, idleStart = 0;

        await server.WaitAssertion(() =>
        {
            // a trained operator's set anchors its memories; an untrained wearer's does not
            (var relayWearer, relaying) = SpawnOperator(server, testMap.GridCoords, FilledPack, trained: true);
            (var idleWearer, idle) = SpawnOperator(server, testMap.GridCoords, FilledPack, trained: false);

            LoadNet(server, relaying, relayWearer, 0, "CMD", Command);
            LoadNet(server, idle, idleWearer, 0, "CMD", Command);

            Assert.That(server.EntMan.GetComponent<ANPRCRelayAnchorComponent>(relaying).Channels, Does.Contain(Command));

            relayStart = Charge(server, relaying);
            idleStart = Charge(server, idle);
        });

        await pair.RunSeconds(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(relayStart - Charge(server, relaying), Is.GreaterThan(idleStart - Charge(server, idle)),
                "anchoring a net for the headsets around it draws more than just being on");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EmconGoesSilent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var ui = server.System<SharedUserInterfaceSystem>();

            var (wearer, pack) = SpawnOperator(server, testMap.GridCoords, FilledPack, trained: true);
            LoadNet(server, pack, wearer, 0, "CMD", Command);

            Assert.That(entities.HasComponent<ANPRCRelayAnchorComponent>(pack), Is.True, "relaying before EMCON");

            ui.OpenUi(pack, ANPRCRadioUI.Key, wearer);
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSetEmconMsg(true) { Actor = wearer });

            Assert.That(entities.HasComponent<ANPRCRelayAnchorComponent>(pack), Is.False, "EMCON takes the set off the relay");

            // a radio check is a transmission. in EMCON it must not go out, so it costs nothing
            var before = Charge(server, pack);
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCRadioCheckMsg { Actor = wearer });
            Assert.That(Charge(server, pack), Is.EqualTo(before), "nothing was keyed");

            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSetEmconMsg(false) { Actor = wearer });
            Assert.That(entities.HasComponent<ANPRCRelayAnchorComponent>(pack), Is.True, "lifting EMCON restores the relay");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PriorityWatchHearsASecondNet()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var ui = server.System<SharedUserInterfaceSystem>();

            var (wearer, pack) = SpawnOperator(server, testMap.GridCoords, FilledPack, trained: true);
            LoadNet(server, pack, wearer, 0, "ALPHA", Alpha);
            LoadNet(server, pack, wearer, 1, "CMD", Command);

            ui.OpenUi(pack, ANPRCRadioUI.Key, wearer);
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSelectSlotMsg(0) { Actor = wearer });

            var active = entities.GetComponent<ActiveRadioComponent>(pack);
            Assert.That(active.Channels, Does.Not.Contain(Command.Id), "only the working net is heard");

            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSetPriorityWatchMsg(1) { Actor = wearer });
            Assert.That(entities.GetComponent<ActiveRadioComponent>(pack).Channels, Does.Contain(Command.Id),
                "the watched net is heard alongside the working one");

            // power save needs the receiver asleep between messages, so it ends the watch
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSetPowerSaveMsg(true) { Actor = wearer });
            Assert.That(entities.GetComponent<ANPRCRadioComponent>(pack).PriorityWatchSlot, Is.EqualTo(-1));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OverTheAirRekeyBringsSetsCurrent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var ui = server.System<SharedUserInterfaceSystem>();
            var crypto = server.System<ANPRCCryptoSystem>();

            var (wearer, sender) = SpawnOperator(server, testMap.GridCoords, FilledPack, trained: true);
            var (_, behind) = SpawnOperator(server, testMap.GridCoords, FilledPack, trained: true);

            // the second set's card is from a superseded generation
            var slots = entities.GetComponent<Content.Shared.Containers.ItemSlots.ItemSlotsComponent>(behind);
            var card = slots.Slots["fill_card"].Item!.Value;
            entities.GetComponent<ANPRCFillCardComponent>(card).Generation = 7;

            Assert.That(crypto.IsFillStale(behind), Is.True);
            Assert.That(crypto.IsFillStale(sender), Is.False);

            ui.OpenUi(sender, ANPRCRadioUI.Key, wearer);
            ui.RaiseUiMessage(sender, ANPRCRadioUI.Key, new ANPRCOtarMsg { Actor = wearer });

            Assert.That(crypto.IsFillStale(behind), Is.False, "the key push brought the set current");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReturnToAutoResetsEveryFaceplateSetting()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var ui = server.System<SharedUserInterfaceSystem>();

            var (wearer, pack) = SpawnOperator(server, testMap.GridCoords, FilledPack, trained: true);
            LoadNet(server, pack, wearer, 0, "ALPHA", Alpha);
            LoadNet(server, pack, wearer, 1, "CMD", Command);

            ui.OpenUi(pack, ANPRCRadioUI.Key, wearer);
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSetTxPowerMsg(RadioTxPower.High) { Actor = wearer });
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSetModeMsg(RadioMode.PlainText) { Actor = wearer });
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSetSquelchMsg(0) { Actor = wearer });
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSetCallsignMsg("PLT MAIN") { Actor = wearer });
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSetBurstMsg(true) { Actor = wearer });
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSetPriorityWatchMsg(1) { Actor = wearer });
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSetEmconMsg(true) { Actor = wearer });

            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCReturnToAutoMsg { Actor = wearer });

            var radio = entities.GetComponent<ANPRCRadioComponent>(pack);

            Assert.Multiple(() =>
            {
                Assert.That(radio.TxPower, Is.EqualTo(RadioTxPower.Medium));
                Assert.That(radio.Mode, Is.EqualTo(RadioMode.FrequencyHopping));
                Assert.That(radio.SquelchLevel, Is.EqualTo(3));
                Assert.That(radio.Callsign, Is.Empty);
                Assert.That(radio.Burst, Is.False);
                Assert.That(radio.PriorityWatchSlot, Is.EqualTo(-1));
                Assert.That(radio.Emcon, Is.False);
                Assert.That(radio.Presets, Has.Count.EqualTo(2), "memories are never touched");
                Assert.That(entities.HasComponent<ANPRCRelayAnchorComponent>(pack), Is.True, "back on the relay");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PeakedAntennaCoversFurther()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid operatorUid = default, pack = default;
        float before = 0;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var spawning = server.System<StationSpawningSystem>();
            var ui = server.System<SharedUserInterfaceSystem>();

            operatorUid = spawning.SpawnPlayerMob(testMap.GridCoords, Rifleman, new HumanoidCharacterProfile(), station: null);
            entities.EnsureComponent<ANPRCRadioUserComponent>(operatorUid);

            pack = entities.SpawnEntity(FilledPack, testMap.GridCoords);
            var radio = entities.GetComponent<ANPRCRadioComponent>(pack);
            radio.Planted = true;
            LoadNet(server, pack, operatorUid, 0, "CMD", Command);

            before = entities.GetComponent<ANPRCRelayAnchorComponent>(pack).FullRange;

            ui.OpenUi(pack, ANPRCRadioUI.Key, operatorUid);
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCPeakAntennaMsg { Actor = operatorUid });
        });

        await pair.RunSeconds(17);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;

            Assert.That(entities.GetComponent<ANPRCRadioComponent>(pack).AntennaPeaked, Is.True, "peaking finished");
            Assert.That(entities.GetComponent<ANPRCRelayAnchorComponent>(pack).FullRange, Is.GreaterThan(before * 1.2f),
                "a peaked antenna reaches further");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RetransRepeatsTrafficAcrossTheBridge()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        EntityUid pack = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var spawning = server.System<StationSpawningSystem>();
            var ui = server.System<SharedUserInterfaceSystem>();
            var radioSystem = server.System<RadioSystem>();

            var operatorUid = spawning.SpawnPlayerMob(testMap.GridCoords, Rifleman, new HumanoidCharacterProfile(), station: null);

            pack = entities.SpawnEntity(FilledPack, testMap.GridCoords);
            entities.GetComponent<ANPRCRadioComponent>(pack).Planted = true;
            LoadNet(server, pack, operatorUid, 0, "ALPHA", Alpha);
            LoadNet(server, pack, operatorUid, 1, "CMD", Command);

            ui.OpenUi(pack, ANPRCRadioUI.Key, operatorUid);
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSetRetransMsg(0, 1) { Actor = operatorUid });

            var radio = entities.GetComponent<ANPRCRadioComponent>(pack);
            Assert.That(radio.RetransSlotA, Is.EqualTo(0));
            Assert.That(entities.GetComponent<ActiveRadioComponent>(pack).Channels, Does.Contain(Command.Id),
                "a bridging set hears both sides");

            radio.LastTransmit = TimeSpan.Zero;

            // somebody on the squad net talks
            var talker = spawning.SpawnPlayerMob(testMap.GridCoords, Rifleman, new HumanoidCharacterProfile(), station: null);
            var alpha = server.ProtoMan.Index(Alpha);
            radioSystem.SendRadioMessage(talker, "contact north", alpha, talker);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<ANPRCRadioComponent>(pack).LastTransmit, Is.GreaterThan(TimeSpan.Zero),
                "the set put the squad net's traffic out on the command net");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void CompassBearingPointsTheRightWay()
    {
        var origin = System.Numerics.Vector2.Zero;

        Assert.Multiple(() =>
        {
            Assert.That(ANPRCRadioSystem.CompassBearing(origin, new(0, 10)), Is.EqualTo(0f).Within(0.01f), "north");
            Assert.That(ANPRCRadioSystem.CompassBearing(origin, new(10, 0)), Is.EqualTo(90f).Within(0.01f), "east");
            Assert.That(ANPRCRadioSystem.CompassBearing(origin, new(0, -10)), Is.EqualTo(180f).Within(0.01f), "south");
            Assert.That(ANPRCRadioSystem.CompassBearing(origin, new(-10, 0)), Is.EqualTo(270f).Within(0.01f), "west");
        });
    }

    private static float Charge(Robust.UnitTesting.RobustIntegrationTest.ServerIntegrationInstance server, EntityUid pack)
    {
        var powerCell = server.System<PowerCellSystem>();
        var battery = server.System<BatterySystem>();

        Assert.That(powerCell.TryGetBatteryFromSlot(pack, out var cell), Is.True);
        return battery.GetCharge(cell!.Value.AsNullable());
    }

    private static void LoadNet(
        Robust.UnitTesting.RobustIntegrationTest.ServerIntegrationInstance server,
        EntityUid pack,
        EntityUid actor,
        int slot,
        string label,
        ProtoId<RadioChannelPrototype> channel)
    {
        var ui = server.System<SharedUserInterfaceSystem>();
        var radio = server.EntMan.GetComponent<ANPRCRadioComponent>(pack);

        radio.SlotLabels[slot] = label;

        // through the real handler, so channels and the relay are rebuilt the way play does it. BUI
        // messages only count from someone who has the panel open
        ui.OpenUi(pack, ANPRCRadioUI.Key, actor);
        ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSetSlotChannelMsg(slot, channel) { Actor = actor });

        if (radio.ActiveSlot < 0)
            ui.RaiseUiMessage(pack, ANPRCRadioUI.Key, new ANPRCSelectSlotMsg(slot) { Actor = actor });
    }

    private static (EntityUid Wearer, EntityUid Pack) SpawnOperator(
        Robust.UnitTesting.RobustIntegrationTest.ServerIntegrationInstance server,
        EntityCoordinates coords,
        EntProtoId packId,
        bool trained)
    {
        var entities = server.EntMan;
        var spawning = server.System<StationSpawningSystem>();
        var inventory = server.System<InventorySystem>();

        var wearer = spawning.SpawnPlayerMob(coords, Rifleman, new HumanoidCharacterProfile(), station: null);
        var pack = entities.SpawnEntity(packId, coords);

        if (inventory.TryGetSlotEntity(wearer, BackSlot, out var oldBack))
            entities.DeleteEntity(oldBack.Value);

        if (trained)
            entities.EnsureComponent<ANPRCRadioUserComponent>(wearer);

        Assert.That(inventory.TryEquip(wearer, pack, BackSlot, force: true), Is.True);

        return (wearer, pack);
    }
}
