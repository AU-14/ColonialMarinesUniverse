using Content.IntegrationTests.Fixtures;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._RMC14.Power;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Power;

[TestFixture]
[TestOf(typeof(PowerNetSystem))]
public sealed class PowerNetMergeRegressionTest : GameTest
{
    private static readonly EntProtoId DualReceiver = "PowerNetMergeDualReceiver";
    private static readonly EntProtoId ZeroLoadReceiver = "PowerNetMergeZeroLoadReceiver";
    private static readonly EntProtoId NoPowerNeededReceiver = "PowerNetMergeNoPowerNeededReceiver";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: PowerNetMergeDualReceiver
  components:
  - type: ApcPowerReceiver
    powerLoad: 10
  - type: RMCPowerReceiver
    idleLoad: 10
    activeLoad: 10
    channel: Equipment
  - type: ApcPowerReceiverBattery
    enabled: true
    idleLoad: 5
    batteryRechargeRate: 20
  - type: Battery
    netSync: false
    maxCharge: 100
    startingCharge: 10
  - type: PowerNetMergeProbe

- type: entity
  id: PowerNetMergeZeroLoadReceiver
  components:
  - type: ApcPowerReceiver
    needsPower: true
    powerLoad: 0
  - type: PowerNetMergeProbe

- type: entity
  id: PowerNetMergeNoPowerNeededReceiver
  components:
  - type: ApcPowerReceiver
    needsPower: false
    powerLoad: 0
  - type: PowerNetMergeProbe
";

    [Test]
    public async Task RmcReceiverSkipsVanillaCalculationBatteryAndEvents()
    {
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<PowerNetMergeProbeSystem>();
            var powerNet = Server.System<PowerNetSystem>();
            var batterySystem = Server.System<BatterySystem>();

            var dual = SEntMan.SpawnEntity(DualReceiver, MapCoordinates.Nullspace);
            var zeroLoad = SEntMan.SpawnEntity(ZeroLoadReceiver, MapCoordinates.Nullspace);
            var noPowerNeeded = SEntMan.SpawnEntity(NoPowerNeededReceiver, MapCoordinates.Nullspace);

            var dualReceiver = SEntMan.GetComponent<ApcPowerReceiverComponent>(dual);
            var dualBattery = SEntMan.GetComponent<BatteryComponent>(dual);
            var dualProbe = SEntMan.GetComponent<PowerNetMergeProbeComponent>(dual);
            var zeroReceiver = SEntMan.GetComponent<ApcPowerReceiverComponent>(zeroLoad);
            var zeroProbe = SEntMan.GetComponent<PowerNetMergeProbeComponent>(zeroLoad);
            var noPowerReceiver = SEntMan.GetComponent<ApcPowerReceiverComponent>(noPowerNeeded);
            var noPowerProbe = SEntMan.GetComponent<PowerNetMergeProbeComponent>(noPowerNeeded);

            // Model the RMC network having just declared its dual receiver powered.
            dualReceiver.Powered = true;
            dualReceiver.Load = 10;
            batterySystem.SetCharge((dual, dualBattery), 10);
            var rmcPower = new PowerChangedEvent(true, 0);
            SEntMan.EventBus.RaiseLocalEvent(dual, ref rmcPower);

            // Force both vanilla positive controls to cross a state boundary on this exact update.
            zeroReceiver.Powered = true;
            noPowerReceiver.Powered = false;
            dualProbe.Reset();
            zeroProbe.Reset();
            noPowerProbe.Reset();

            // Without the early RMC guard this duration empties the dual receiver's internal battery,
            // rewrites its load, clears Powered, and raises both battery and power events.
            powerNet.Update(3f);

            Assert.Multiple(() =>
            {
                Assert.That(dualReceiver.Powered, Is.True,
                    "vanilla PowerNet must not overwrite RMC-owned power state");
                Assert.That(dualReceiver.Load, Is.EqualTo(10),
                    "the skip must happen before vanilla substitutes the internal battery idle load");
                Assert.That(batterySystem.GetCharge((dual, dualBattery)), Is.EqualTo(10),
                    "vanilla PowerNet must not drain an RMC receiver's internal battery");
                Assert.That(dualProbe.PowerChangedEvents, Is.Zero);
                Assert.That(dualProbe.BatteryChangedEvents, Is.Zero);

                Assert.That(zeroReceiver.Powered, Is.False,
                    "an ordinary needsPower receiver with zero load must be unpowered");
                Assert.That(zeroProbe.PowerChangedEvents, Is.EqualTo(1));
                Assert.That(zeroProbe.LastPowered, Is.False);

                Assert.That(noPowerReceiver.Powered, Is.True,
                    "needsPower:false must remain the always-powered upstream control");
                Assert.That(noPowerProbe.PowerChangedEvents, Is.EqualTo(1));
                Assert.That(noPowerProbe.LastPowered, Is.True);
            });
        });
    }
}

[RegisterComponent]
public sealed partial class PowerNetMergeProbeComponent : Component
{
    public int PowerChangedEvents;
    public int BatteryChangedEvents;
    public bool? LastPowered;

    public void Reset()
    {
        PowerChangedEvents = 0;
        BatteryChangedEvents = 0;
        LastPowered = null;
    }
}

public sealed class PowerNetMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PowerNetMergeProbeComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<PowerNetMergeProbeComponent, ApcPowerReceiverBatteryChangedEvent>(OnBatteryChanged);
    }

    private static void OnPowerChanged(
        Entity<PowerNetMergeProbeComponent> ent,
        ref PowerChangedEvent args)
    {
        ent.Comp.PowerChangedEvents++;
        ent.Comp.LastPowered = args.Powered;
    }

    private static void OnBatteryChanged(
        Entity<PowerNetMergeProbeComponent> ent,
        ref ApcPowerReceiverBatteryChangedEvent args)
    {
        ent.Comp.BatteryChangedEvents++;
    }
}
