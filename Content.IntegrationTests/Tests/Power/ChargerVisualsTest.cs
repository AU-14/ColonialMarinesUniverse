using Content.Client.PowerCell;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using ServerBatterySystem = Content.Server.Power.EntitySystems.BatterySystem;

namespace Content.IntegrationTests.Tests.Power;

[TestFixture]
[TestOf(typeof(ChargerSystem))]
public sealed class ChargerVisualsTest : GameTest
{
    private const string BatteryPrototype = "PowerCellSmallPrinted";
    private const string RmcChargerPrototype = "RMCRecharger";
    private const string MinimumStepsChargerPrototype = "ChargerVisualsTestMinimumSteps";
    private const string MaximumStepsChargerPrototype = "ChargerVisualsTestMaximumSteps";
    private const string UnchargeablePrototype = "ChargerVisualsTestUnchargeable";
    private const string FallbackPrototype = "ChargerVisualsTestFallback";
    private const string SlotId = "charger_slot";

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
    };

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  id: {MinimumStepsChargerPrototype}
  components:
  - type: Appearance
  - type: Charger
    slotId: {SlotId}
    portable: true
    chargeRate: 0
    chargeLevelSteps: 1
  - type: ContainerContainer
    containers:
      {SlotId}: !type:ContainerSlot

- type: entity
  id: {MaximumStepsChargerPrototype}
  components:
  - type: Appearance
  - type: Charger
    slotId: {SlotId}
    portable: true
    chargeRate: 0
    chargeLevelSteps: 999
  - type: ContainerContainer
    containers:
      {SlotId}: !type:ContainerSlot

- type: entity
  id: {UnchargeablePrototype}

- type: entity
  id: {FallbackPrototype}
  components:
  - type: Sprite
    sprite: Structures/Power/recharger.rsi
    layers:
    - state: empty
      map: [""enum.PowerChargerVisualLayers.Base""]
    - state: light-off
      map: [""enum.PowerChargerVisualLayers.Light""]
      shader: unshaded
  - type: Appearance
  - type: PowerChargerVisuals
";

    [Test]
    public async Task RmcSixStepChargerUsesCeilingAndNumericSpriteStates()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        EntityUid charger = default;
        EntityUid battery = default;

        await server.WaitPost(() =>
        {
            charger = server.EntMan.SpawnEntity(RmcChargerPrototype, map.GridCoords);
            var chargerComp = server.EntMan.GetComponent<ChargerComponent>(charger);
            Assert.That(chargerComp.ChargeLevelSteps, Is.EqualTo(6),
                "the RMC recharger resource must retain all six charge frames");

            chargerComp.ChargeRate = 0f;
            server.EntMan.Dirty(charger, chargerComp);
        });
        await AssertRmcChargeLevel(charger, 0);

        await server.WaitPost(() =>
        {
            battery = server.EntMan.SpawnEntity(BatteryPrototype, map.GridCoords);
            var containers = server.EntMan.System<SharedContainerSystem>();
            var slot = containers.EnsureContainer<ContainerSlot>(charger, SlotId);
            Assert.That(containers.Insert(battery, slot), Is.True);
        });
        await AssertRmcChargeLevel(charger, 0);

        var levels = new (float Charge, byte Level)[]
        {
            (1f, 1),
            (90f, 1),
            (91f, 2),
            (180f, 2),
            (181f, 3),
            (270f, 3),
            (271f, 4),
            (359f, 4),
            (360f, 5),
        };

        foreach (var (charge, level) in levels)
        {
            await server.WaitPost(() =>
            {
                var batteryComp = server.EntMan.GetComponent<BatteryComponent>(battery);
                server.EntMan.System<ServerBatterySystem>().SetCharge((battery, batteryComp), charge);
            });
            await AssertRmcChargeLevel(charger, level);
        }
    }

    [Test]
    public async Task LiveChargeRateRefreshesIntermediateLevel()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        EntityUid charger = default;
        EntityUid battery = default;

        await server.WaitPost(() =>
        {
            charger = server.EntMan.SpawnEntity(RmcChargerPrototype, map.GridCoords);
            var chargerComp = server.EntMan.GetComponent<ChargerComponent>(charger);
            chargerComp.ChargeRate = 180f;
            server.EntMan.Dirty(charger, chargerComp);

            battery = server.EntMan.SpawnEntity(BatteryPrototype, map.GridCoords);
            var containers = server.EntMan.System<SharedContainerSystem>();
            var slot = containers.EnsureContainer<ContainerSlot>(charger, SlotId);
            Assert.That(containers.Insert(battery, slot), Is.True);
        });

        await Pair.RunTicksSync(Pair.SecondsToTicks(0.3f));
        var firstLevel = await AssertCurrentIntermediateLevel(charger, battery);

        await Pair.RunTicksSync(Pair.SecondsToTicks(0.35f));
        var secondLevel = await AssertCurrentIntermediateLevel(charger, battery);
        Assert.That(secondLevel, Is.GreaterThan(firstLevel),
            "continuous charging should refresh the appearance after crossing a discrete frame boundary");
    }

    [Test]
    public async Task EmptyUnchargeableAndStepBoundsAreClamped()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        EntityUid minimum = default;
        EntityUid maximum = default;

        await server.WaitPost(() =>
        {
            minimum = server.EntMan.SpawnEntity(MinimumStepsChargerPrototype, map.GridCoords);
            maximum = server.EntMan.SpawnEntity(MaximumStepsChargerPrototype, map.GridCoords);
        });
        await Pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(GetChargeLevel(server.EntMan, minimum), Is.EqualTo(0));
            Assert.That(GetChargeLevel(server.EntMan, maximum), Is.EqualTo(0));
        });

        EntityUid unchargeable = default;
        await server.WaitPost(() =>
        {
            unchargeable = server.EntMan.SpawnEntity(UnchargeablePrototype, map.GridCoords);
            var containers = server.EntMan.System<SharedContainerSystem>();
            var slot = containers.EnsureContainer<ContainerSlot>(minimum, SlotId);
            Assert.That(containers.Insert(unchargeable, slot), Is.True);
        });
        await Pair.RunTicksSync(2);
        await server.WaitAssertion(() => Assert.That(GetChargeLevel(server.EntMan, minimum), Is.EqualTo(0)));

        await server.WaitPost(() =>
        {
            var containers = server.EntMan.System<SharedContainerSystem>();
            var slot = containers.EnsureContainer<ContainerSlot>(minimum, SlotId);
            Assert.That(containers.Remove(unchargeable, slot), Is.True);

            var halfBattery = server.EntMan.SpawnEntity(BatteryPrototype, map.GridCoords);
            var halfBatteryComp = server.EntMan.GetComponent<BatteryComponent>(halfBattery);
            server.EntMan.System<ServerBatterySystem>().SetCharge((halfBattery, halfBatteryComp), 180f);
            Assert.That(containers.Insert(halfBattery, slot), Is.True);

            var fullBattery = server.EntMan.SpawnEntity(BatteryPrototype, map.GridCoords);
            var fullBatteryComp = server.EntMan.GetComponent<BatteryComponent>(fullBattery);
            server.EntMan.System<ServerBatterySystem>().SetCharge((fullBattery, fullBatteryComp), 360f);
            var maximumSlot = containers.EnsureContainer<ContainerSlot>(maximum, SlotId);
            Assert.That(containers.Insert(fullBattery, maximumSlot), Is.True);
        });
        await Pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(GetChargeLevel(server.EntMan, minimum), Is.EqualTo(1),
                    "configured steps below three should clamp to one intermediate frame");
                Assert.That(GetChargeLevel(server.EntMan, maximum), Is.EqualTo(byte.MaxValue),
                    "configured steps above 256 should clamp to the largest byte frame index");
            });
        });
    }

    [Test]
    public async Task MissingNumericFormatFallsBackToStatusLight()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        EntityUid fallback = default;

        await server.WaitPost(() =>
        {
            fallback = server.EntMan.SpawnEntity(FallbackPrototype, map.GridCoords);
            var appearance = server.EntMan.System<SharedAppearanceSystem>();
            appearance.SetData(fallback, CellVisual.ChargeLevel, (byte) 5);
            appearance.SetData(fallback, CellVisual.Light, CellChargerStatus.Charged);
        });
        await Pair.RunTicksSync(3);

        var clientFallback = Pair.ToClientUid(fallback);
        await Pair.Client.WaitAssertion(() =>
        {
            Assert.That(Pair.Client.EntMan.HasComponent<PowerChargerVisualsComponent>(clientFallback), Is.True);
            Assert.That(GetLightState(Pair.Client.EntMan, clientFallback), Is.EqualTo("light-charged"));
        });
    }

    private async Task AssertRmcChargeLevel(EntityUid charger, byte expected)
    {
        await Pair.RunTicksSync(3);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(GetChargeLevel(Pair.Server.EntMan, charger), Is.EqualTo(expected));
        });

        var clientCharger = Pair.ToClientUid(charger);
        await Pair.Client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(GetChargeLevel(Pair.Client.EntMan, clientCharger), Is.EqualTo(expected));
                Assert.That(GetLightState(Pair.Client.EntMan, clientCharger), Is.EqualTo($"recharger-{expected}"));
            });
        });
    }

    private async Task<byte> AssertCurrentIntermediateLevel(EntityUid charger, EntityUid battery)
    {
        byte expected = 0;
        await Pair.Server.WaitAssertion(() =>
        {
            var batteryComp = Pair.Server.EntMan.GetComponent<BatteryComponent>(battery);
            var charge = Pair.Server.EntMan.System<ServerBatterySystem>().GetCharge((battery, batteryComp));
            Assert.That(charge, Is.GreaterThan(0f).And.LessThan(batteryComp.MaxCharge));

            expected = ExpectedSixStepLevel(charge / batteryComp.MaxCharge);
            Assert.That(GetChargeLevel(Pair.Server.EntMan, charger), Is.EqualTo(expected));
        });

        var clientCharger = Pair.ToClientUid(charger);
        await Pair.Client.WaitAssertion(() =>
        {
            Assert.That(GetLightState(Pair.Client.EntMan, clientCharger), Is.EqualTo($"recharger-{expected}"));
        });
        return expected;
    }

    private static byte ExpectedSixStepLevel(float fraction)
    {
        if (fraction >= 1f)
            return 5;
        if (fraction <= 0f)
            return 0;
        return (byte) Math.Clamp((int) Math.Ceiling(fraction * 4), 1, 4);
    }

    private static byte GetChargeLevel(IEntityManager entMan, EntityUid charger)
    {
        var appearance = entMan.System<SharedAppearanceSystem>();
        Assert.That(appearance.TryGetData(charger, CellVisual.ChargeLevel, out byte level), Is.True);
        return level;
    }

    private static string? GetLightState(IEntityManager entMan, EntityUid charger)
    {
        var sprite = entMan.GetComponent<SpriteComponent>(charger);
        var spriteSystem = entMan.System<SpriteSystem>();
        var layer = spriteSystem.LayerMapGet((charger, sprite), PowerChargerVisualLayers.Light);
        return spriteSystem.LayerGetRsiState((charger, sprite), layer).Name;
    }
}
