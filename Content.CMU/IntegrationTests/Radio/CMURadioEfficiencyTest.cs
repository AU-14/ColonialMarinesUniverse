using System.Reflection;
using System.IO;
using Content.Client.CMU14.Radio;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Radio;
using Content.Shared.CMU14.Radio;
using Content.Shared.PowerCell;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.IntegrationTests.CMU14.Radio;

[TestFixture]
public sealed class CMURadioEfficiencyTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task RelayRetuningMovementAndRemovalImmediatelyChangeCoverage()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var range = Server.System<ANPRCRangeSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var receiver = SEntMan.SpawnEntity(null, map.GridCoords);
            var uid = SEntMan.SpawnEntity(null, map.GridCoords);
            var anchor = SEntMan.AddComponent<ANPRCRelayAnchorComponent>(uid);
            ProtoId<RadioChannelPrototype> first = "radioGovforAlpha";
            ProtoId<RadioChannelPrototype> second = "radioGovforCommand";
            anchor.Channels.Add(first);
            range.RefreshAnchorChannels((uid, anchor));
            Assert.That(range.GetRangeTier(receiver, first.Id, out _), Is.EqualTo(ANPRCRangeTier.Full));

            anchor.Channels.Clear();
            anchor.Channels.Add(second);
            range.RefreshAnchorChannels((uid, anchor));
            Assert.That(range.GetRangeTier(receiver, first.Id, out _), Is.EqualTo(ANPRCRangeTier.OutOfRange));
            Assert.That(range.GetRangeTier(receiver, second.Id, out _), Is.EqualTo(ANPRCRangeTier.Full));

            transform.SetCoordinates(uid, map.GridCoords.Offset(new System.Numerics.Vector2(100, 0)));
            Assert.That(range.GetRangeTier(receiver, second.Id, out _), Is.EqualTo(ANPRCRangeTier.OutOfRange));
            transform.SetCoordinates(uid, map.GridCoords);
            Assert.That(range.GetRangeTier(receiver, second.Id, out _), Is.EqualTo(ANPRCRangeTier.Full));
            SEntMan.RemoveComponent<ANPRCRelayAnchorComponent>(uid);
            Assert.That(range.GetRangeTier(receiver, second.Id, out _), Is.EqualTo(ANPRCRangeTier.OutOfRange));
        });
    }

    [Test]
    public async Task TelemetryPreservesConfigurationAndReopeningResynchronizes()
    {
        var map = await Pair.CreateTestMap();
        EntityUid pack = default;
        EntityUid actor = default;
        NetEntity netPack = default;
        ANPRCRadioState initial = null;
        var session = ServerSession!;
        var previous = session.AttachedEntity;
        await Server.WaitPost(() =>
        {
            actor = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            pack = SEntMan.SpawnEntity("ANPRC117GRadio", map.GridCoords);
            netPack = SEntMan.GetNetEntity(pack);
            SEntMan.EnsureComponent<ANPRCRadioUserComponent>(actor);
            Server.PlayerMan.SetAttachedEntity(session, actor);
        });
        try
        {
            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() =>
            {
                var ui = Server.System<SharedUserInterfaceSystem>();
                ui.OpenUi(pack, ANPRCRadioUI.Key, actor);
                Assert.That(ui.TryGetUiState<ANPRCRadioState>(pack, ANPRCRadioUI.Key, out initial), Is.True);
                var telemetry = new ANPRCRadioTelemetryMsg(initial.Revision, initial.BatteryFraction,
                    initial.HasBattery, initial.WearerCallsign, initial.CryptoStale, initial.Info, initial.Expert);
                var serializer = Server.ResolveDependency<IRobustSerializer>();
                using var fullBytes = new MemoryStream();
                using var telemetryBytes = new MemoryStream();
                serializer.Serialize(fullBytes, initial);
                serializer.Serialize(telemetryBytes, telemetry);
                Assert.That(telemetryBytes.Length, Is.LessThan(fullBytes.Length));
                TestContext.Out.WriteLine($"Radio panel payload: full={fullBytes.Length} bytes, telemetry={telemetryBytes.Length} bytes.");
            });
            await Pair.RunUntilSynced();
            await Server.WaitPost(() => SetBattery(0.5f));
            await Pair.RunSeconds(1.2f);
            await Server.WaitAssertion(() =>
            {
                Server.System<SharedUserInterfaceSystem>().TryGetUiState<ANPRCRadioState>(pack, ANPRCRadioUI.Key, out var state);
                Assert.That(state, Is.SameAs(initial), "Gauge drift must not rebuild the configuration state.");
            });
            await Client.WaitAssertion(() =>
            {
                var state = ReadClientState();
                Assert.That(state.BatteryFraction, Is.EqualTo(0.5f).Within(0.01f));
                Assert.That(state.Presets, Is.EquivalentTo(initial.Presets));
                Assert.That(state.ChannelFrequencies, Is.EquivalentTo(initial.ChannelFrequencies));
            });
            await Server.WaitPost(() => Server.System<SharedUserInterfaceSystem>().CloseUi(pack, ANPRCRadioUI.Key, actor));
            await Pair.RunUntilSynced();
            await Server.WaitPost(() =>
            {
                SetBattery(0.25f);
                Server.System<SharedUserInterfaceSystem>().OpenUi(pack, ANPRCRadioUI.Key, actor);
            });
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var state = ReadClientState();
                Assert.That(state.Revision, Is.GreaterThan(initial.Revision));
                Assert.That(state.BatteryFraction, Is.EqualTo(0.25f).Within(0.01f));
            });
            await Server.WaitPost(() => Server.System<SharedUserInterfaceSystem>().ServerSendUiMessage(
                pack, ANPRCRadioUI.Key, new ANPRCRadioTelemetryMsg(initial.Revision, 0f, true,
                    initial.WearerCallsign, initial.CryptoStale, initial.Info, initial.Expert)));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() => Assert.That(ReadClientState().BatteryFraction,
                Is.EqualTo(0.25f).Within(0.01f), "A reading from the closed panel must not overwrite the reopened panel."));
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                Server.System<SharedUserInterfaceSystem>().CloseUi(pack, ANPRCRadioUI.Key, actor);
                Server.PlayerMan.SetAttachedEntity(session, previous);
            });
        }

        void SetBattery(float fraction)
        {
            var cells = Server.System<PowerCellSystem>();
            Assert.That(cells.TryGetBatteryFromSlot(pack, out var cell), Is.True);
            Server.System<Content.Server.Power.EntitySystems.BatterySystem>()
                .SetCharge(cell!.Value.AsNullable(), cell.Value.Comp.MaxCharge * fraction);
        }

        ANPRCRadioState ReadClientState()
        {
            var ui = Client.System<SharedUserInterfaceSystem>();
            Assert.That(ui.TryGetOpenUi<ANPRCRadioBoundUserInterface>(CEntMan.GetEntity(netPack), ANPRCRadioUI.Key, out var bui), Is.True);
            return (ANPRCRadioState) typeof(ANPRCRadioBoundUserInterface)
                .GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bui)!;
        }
    }
}
