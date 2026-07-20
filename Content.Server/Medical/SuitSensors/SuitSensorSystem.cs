using Content.Server.DeviceNetwork.Systems;
using Content.Server.Medical.CrewMonitoring;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Medical.SuitSensors;
using Robust.Shared.Timing;

namespace Content.Server.Medical.SuitSensors;

public sealed partial class SuitSensorSystem : EntitySystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DeviceNetworkSystem _deviceNetworkSystem = default!;
    [Dependency] private IdCardSystem _idCardSystem = default!;
    [Dependency] private MobStateSystem _mobStateSystem = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private SingletonDeviceNetServerSystem _singletonServerSystem = default!;
    [Dependency] private MobThresholdSystem _mobThresholdSystem = default!;
    [Dependency] private SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<SuitSensorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SuitSensorComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<SuitSensorComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<SuitSensorComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<SuitSensorComponent, GetVerbsEvent<Verb>>(OnVerb);
        SubscribeLocalEvent<SuitSensorComponent, EntGotInsertedIntoContainerMessage>(OnInsert);
        SubscribeLocalEvent<SuitSensorComponent, EntGotRemovedFromContainerMessage>(OnRemove);
        SubscribeLocalEvent<SuitSensorComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<SuitSensorComponent, EmpDisabledRemoved>(OnEmpFinished);
        SubscribeLocalEvent<SuitSensorComponent, SuitSensorChangeDoAfterEvent>(OnSuitSensorDoAfter);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _gameTiming.CurTime;
        var sensors = EntityQueryEnumerator<SuitSensorComponent, DeviceNetworkComponent>();

        while (sensors.MoveNext(out var uid, out var sensor, out var device))
        {
            if (device.TransmitFrequency is null)
                continue;

            // check if sensor is ready to update
            if (curTime < sensor.NextUpdate)
                continue;
            sensor.NextUpdate += sensor.UpdateRate;

            // TODO: This would cause imprecision at different tick rates.
            sensor.NextUpdate = curTime + sensor.UpdateRate;

            if (!CheckSensorAssignedStation(uid, sensor))
                continue;

            // get sensor status
            var status = GetSensorState((uid, sensor));
            if (status == null)
                continue;

            //Retrieve active server address if the sensor isn't connected to a server
            if (sensor.ConnectedServer == null)
            {
                if (!_singletonServerSystem.TryGetActiveServerAddress<CrewMonitoringServerComponent>(sensor.StationId!.Value, out var address))
                    continue;

                sensor.ConnectedServer = address;
            }

            // Send it to the connected server
            var payload = SuitSensorToPacket(status);

            // Clear the connected server if its address isn't on the network
            if (!_deviceNetworkSystem.IsAddressPresent(device.DeviceNetId, sensor.ConnectedServer))
            {
                sensor.ConnectedServer = null;
                continue;
            }

            _deviceNetworkSystem.QueuePacket(uid, sensor.ConnectedServer, payload, device: device);
        }
    }
}
