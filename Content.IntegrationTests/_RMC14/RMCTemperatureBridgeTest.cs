using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RMC14.Temperature;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;
using Robust.Shared.GameObjects;
using ServerRMCTemperatureSystem = Content.Server._RMC14.Temperature.RMCTemperatureSystem;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
[TestOf(typeof(SharedRMCTemperatureSystem))]
public sealed class RMCTemperatureBridgeTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: RMCTemperatureBridgeTarget
  parent: CMMobHuman
  components:
  - type: TemperatureSpeed
    thresholds:
      273: 0.5
  - type: RMCTemperatureBridgeProbe
";

    [SidedDependency(Side.Server)] private ServerRMCTemperatureSystem _temperature = default!;

    [Test]
    public async Task ForceChangeUsesSharedTemperatureAndReplicatesSlowdown()
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        EntityUid missing = default;
        NetEntity targetNet = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                _ = Server.System<RMCTemperatureBridgeProbeSystem>();
                target = SEntMan.SpawnEntity("RMCTemperatureBridgeTarget", map.GridCoords);
                missing = SEntMan.SpawnEntity(null, map.GridCoords);
                var missingProbe = SEntMan.EnsureComponent<RMCTemperatureBridgeProbeComponent>(missing);
                targetNet = SEntMan.GetNetEntity(target);

                var initialTemperature = _temperature.GetTemperature(target);
                Assert.That(initialTemperature, Is.GreaterThan(273f));

                _temperature.ForceChangeTemperature(target, 272f);
                _ = _temperature.TryGetCurrentTemperature(target, out var tryTemperature);

                var component = SEntMan.GetComponent<TemperatureComponent>(target);
                var probe = SEntMan.GetComponent<RMCTemperatureBridgeProbeComponent>(target);
                var slowdown = SEntMan.GetComponent<TemperatureSpeedComponent>(target);
                Assert.Multiple(() =>
                {
                    Assert.That(component.Temperature, Is.EqualTo(272f));
                    Assert.That(_temperature.GetTemperature(target), Is.EqualTo(272f));
                    Assert.That(tryTemperature, Is.EqualTo(272f));
                    Assert.That(probe.EventCount, Is.EqualTo(1));
                    Assert.That(probe.CurrentTemperature, Is.EqualTo(272f));
                    Assert.That(probe.LastTemperature, Is.EqualTo(initialTemperature));
                    Assert.That(slowdown.CurrentSpeedModifier, Is.EqualTo(0.5f));
                    Assert.That(slowdown.NextSlowdownUpdate, Is.Not.Null);
                });

                _temperature.ForceChangeTemperature(missing, 260f);
                _ = _temperature.TryGetCurrentTemperature(missing, out var missingTryTemperature);
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<TemperatureComponent>(missing), Is.False);
                    Assert.That(_temperature.GetTemperature(missing), Is.Zero);
                    Assert.That(missingTryTemperature, Is.Zero);
                    Assert.That(missingProbe.EventCount, Is.Zero);
                });
            });

            await Pair.RunTicksSync(3);
            await Client.WaitAssertion(() =>
            {
                var clientTarget = CEntMan.GetEntity(targetNet);
                var slowdown = CEntMan.GetComponent<TemperatureSpeedComponent>(clientTarget);
                Assert.Multiple(() =>
                {
                    Assert.That(slowdown.CurrentSpeedModifier, Is.EqualTo(0.5f));
                    Assert.That(slowdown.NextSlowdownUpdate, Is.Not.Null);
                });
            });
        }
        finally
        {
            if (SEntMan.EntityExists(target))
                await Pair.DeleteEntityTreeLeafFirst(target);
            if (SEntMan.EntityExists(missing))
                await Pair.DeleteEntityTreeLeafFirst(missing);
        }
    }
}

[RegisterComponent]
public sealed partial class RMCTemperatureBridgeProbeComponent : Component
{
    public int EventCount;
    public float CurrentTemperature;
    public float LastTemperature;
}

public sealed class RMCTemperatureBridgeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RMCTemperatureBridgeProbeComponent, TemperatureChangedEvent>(OnTemperatureChanged);
    }

    private static void OnTemperatureChanged(
        Entity<RMCTemperatureBridgeProbeComponent> entity,
        ref TemperatureChangedEvent args)
    {
        entity.Comp.EventCount++;
        entity.Comp.CurrentTemperature = args.CurrentTemperature;
        entity.Comp.LastTemperature = args.LastTemperature;
    }
}
