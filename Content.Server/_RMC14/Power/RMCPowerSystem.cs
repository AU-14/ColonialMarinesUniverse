using System.Runtime.InteropServices;
using Content.Server._CMU14.ZLevels.Core;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Power;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Profiling;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.Power;

public sealed partial class RMCPowerSystem : SharedRMCPowerSystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private PowerCellSystem _cell = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private ProfManager _prof = default!;

    [ViewVariables]
    private TimeSpan _nextUpdate;

    [ViewVariables]
    private TimeSpan _updateEvery;

    [ViewVariables]
    private float _powerLoadMultiplier;

    private EntityQuery<RMCApcComponent> _apcQuery;
    private EntityQuery<AppearanceComponent> _appearanceQuery;
    private EntityQuery<RMCAreaPowerComponent> _areaPowerQuery;
    private EntityQuery<BatteryComponent> _batteryQuery;

    private readonly Dictionary<EntityUid, List<(Entity<RMCApcComponent, TransformComponent> Apc, Entity<BatteryComponent>? Cell)>> _apcs = new();
    private readonly List<EntityUid> _toRemove = new();
    private bool _profileTopologyRefresh;

    public override void Initialize()
    {
        base.Initialize();

        _apcQuery = GetEntityQuery<RMCApcComponent>();
        _appearanceQuery = GetEntityQuery<AppearanceComponent>();
        _areaPowerQuery = GetEntityQuery<RMCAreaPowerComponent>();
        _batteryQuery = GetEntityQuery<BatteryComponent>();

        SubscribeLocalEvent<RMCPowerReceiverComponent, PowerChangedEvent>(OnReceiverPowerChanged);
        SubscribeLocalEvent<RMCPowerUsageDisplayComponent, ExaminedEvent>(OnUsageDisplayEvent);
        SubscribeLocalEvent<CMUZLevelNetworkUpdatedEvent>(OnZLevelNetworkUpdated);

        Subs.CVar(_config, RMCCVars.RMCPowerUpdateEverySeconds, v => _updateEvery = TimeSpan.FromSeconds(v), true);
        Subs.CVar(_config, RMCCVars.RMCPowerLoadMultiplier, v => _powerLoadMultiplier = v, true);
    }

    private void OnZLevelNetworkUpdated(ref CMUZLevelNetworkUpdatedEvent args)
    {
        _profileTopologyRefresh = true;
        QueueReactorPowerGroupRefresh(args.Network.Owner);
    }

    private void OnUsageDisplayEvent(Entity<RMCPowerUsageDisplayComponent> ent, ref ExaminedEvent args)
    {
        if (!_cell.TryGetBatteryFromSlot(ent.Owner, out var battery) || !TryComp<PowerCellDrawComponent>(ent, out var draw))
            return;

        var maxUses = (int)(battery.Value.Comp.MaxCharge / draw.UseCharge);
        var uses = (int)(_battery.GetCharge(battery.Value.AsNullable()) / draw.UseCharge);

        args.PushMarkup(Loc.GetString(ent.Comp.PowerText, ("uses", uses), ("maxuses", maxUses)));
    }

    private void OnReceiverPowerChanged(Entity<RMCPowerReceiverComponent> ent, ref PowerChangedEvent args)
    {
        ent.Comp.Mode = args.Powered ? RMCPowerMode.Active : RMCPowerMode.Off;
        ToUpdate.Add(ent);
    }

    protected override void OnReceiverMapInit(Entity<RMCPowerReceiverComponent> ent, ref MapInitEvent args)
    {
        base.OnReceiverMapInit(ent, ref args);

        if (!TryComp(ent, out ApcPowerReceiverComponent? receiver))
            return;

        if (receiver.NeedsPower)
            return;

        receiver.Powered = true;

        Dirty(ent, ent.Comp);
        // the networked Powered flag lives on the apc receiver, without this the
        // client examine shows unpowered until the server examine text arrives
        Dirty(ent.Owner, receiver);

        var ev = new PowerChangedEvent(true, 0);
        RaiseLocalEvent(ent, ref ev);

        if (_appearanceQuery.TryComp(ent, out var appearance))
            _appearance.SetData(ent, PowerDeviceVisuals.Powered, true, appearance);
    }

    protected override void PowerUpdated(Entity<RMCAreaPowerComponent> area, RMCPowerChannel channel, bool on)
    {
        base.PowerUpdated(area, channel, on);

        var receivers = GetAreaReceivers(area, channel);
        var ev = new PowerChangedEvent(on, 0);
        foreach (var receiver in receivers)
        {
            UpdateReceiverPower(receiver, ref ev);
        }
    }

    public override bool IsPowered(EntityUid ent)
    {
        return TryComp(ent, out ApcPowerReceiverComponent? receiver) && receiver.Powered;
    }

    public override void Update(float frameTime)
    {
        if (_profileTopologyRefresh)
        {
            _profileTopologyRefresh = false;
            using var profile = _prof.Group("CMU Z Power Topology Refresh");
            var pendingBefore = ToUpdate.Count;

            base.Update(frameTime);

            if (_prof.IsEnabled)
                _prof.WriteValue("CMU Z Power Pending Before Refresh", pendingBefore);
        }
        else
        {
            base.Update(frameTime);
        }

        if (_nextUpdate > _timing.CurTime)
            return;

        _nextUpdate = _timing.CurTime + _updateEvery;
        UpdateOverloadedReactorFeedback(_timing.CurTime);

        _toRemove.Clear();
        foreach (var (map, apcs) in _apcs)
        {
            if (TerminatingOrDeleted(map))
                _toRemove.Add(map);

            apcs.Clear();
        }

        foreach (var remove in _toRemove)
        {
            _apcs.Remove(remove);
        }

        _toRemove.Clear();

        var power = new Dictionary<EntityUid, float>();
        var generators = EntityQueryEnumerator<RMCFusionReactorComponent, TransformComponent>();
        while (generators.MoveNext(out var generator, out var xform))
        {
            if (generator.State != RMCFusionReactorState.Working ||
                !TryGetPowerGroup(xform.MapUid, out var powerGroup))
            {
                continue;
            }

            ref var mapPower = ref CollectionsMarshal.GetValueRefOrAddDefault(power, powerGroup, out _);
            mapPower += generator.Watts;
        }

        var areas = EntityQueryEnumerator<RMCAreaPowerComponent>();
        while (areas.MoveNext(out var uid, out var areaPower))
        {
            foreach (var apc in areaPower.Apcs)
            {
                if (TerminatingOrDeleted(apc) ||
                    !TryComp(apc, out TransformComponent? xform))
                {
                    _toRemove.Add(apc);
                    continue;
                }

                if (!TryGetPowerGroup(xform.MapUid, out var powerGroup))
                    continue;

                if (!_apcQuery.TryComp(apc, out var apcComp))
                    continue;

                Entity<BatteryComponent>? cell = null;
                if (_container.TryGetContainer(apc, apcComp.CellContainerSlot, out var container) &&
                    container.ContainedEntities.TryFirstOrNull(out var cellId) &&
                    _batteryQuery.TryComp(cellId, out var battery))
                {
                    cell = (cellId.Value, battery);
                }

                _apcs.GetOrNew(powerGroup).Add(((apc, apcComp, xform), cell));
            }

            foreach (var remove in _toRemove)
            {
                areaPower.Apcs.Remove(remove);
            }

            if (_toRemove.Count > 0)
                Dirty(uid, areaPower);

            _toRemove.Clear();
        }

        foreach (var (map, apcList) in _apcs)
        {
            var wattsPer = 0f;
            if (power.TryGetValue(map, out var watts))
                wattsPer = watts / apcList.Count;

            var apcs = CollectionsMarshal.AsSpan(apcList);
            foreach (ref var tuple in apcs)
            {
                ref var apc = ref tuple.Apc;
                ref var cell = ref tuple.Cell;
                var apcComp = apc.Comp1;
                if (cell == null)
                {
                    apcComp.ExternalPower = false;
                    apcComp.ChargeStatus = RMCApcChargeStatus.NotCharging;
                    var channels = apcComp.Channels.AsSpan();
                    foreach (ref var channel in channels)
                    {
                        channel.Watts = 0;
                    }

                    Dirty(apc, apcComp);
                }

                if (!_areaPowerQuery.TryComp(apcComp.Area, out var areaComp))
                    continue;

                var area = new Entity<RMCAreaPowerComponent>(apcComp.Area.Value, areaComp);
                if (apcComp.Broken)
                {
                    UpdateApcChannel(apc, area, RMCPowerChannel.Equipment, false);
                    UpdateApcChannel(apc, area, RMCPowerChannel.Lighting, false);
                    UpdateApcChannel(apc, area, RMCPowerChannel.Environment, false);
                    _light.SetEnabled(apc, false);
                    continue;
                }

                var loadSpan = area.Comp.Load.AsSpan();
                var totalLoad = 0;
                for (var i = 0; i < loadSpan.Length; i++)
                {
                    var load = (int) (loadSpan[i] * _powerLoadMultiplier);
                    totalLoad += load;
                    apcComp.Channels[i].Watts = load;
                }

                if (cell == null)
                {
                    apcComp.ChargePercentage = 0;
                }
                else
                {
                    var battery = new Entity<BatteryComponent>(cell.Value, cell.Value.Comp);
                    var drawn = wattsPer;
                    drawn -= totalLoad;
                    if (drawn <= 0)
                    {
                        apcComp.ChargeStatus = RMCApcChargeStatus.NotCharging;
                        _battery.UseCharge(battery.AsNullable(), -drawn);
                    }
                    else
                    {
                        _battery.SetCharge(battery.AsNullable(), _battery.GetCharge(battery.AsNullable()) + drawn);

                        apcComp.ChargeStatus = _battery.IsFull(battery.AsNullable())
                            ? RMCApcChargeStatus.FullCharge
                            : RMCApcChargeStatus.Charging;
                    }

                    apcComp.ChargePercentage = _battery.GetCharge(battery.AsNullable()) / battery.Comp.MaxCharge;
                }

                switch (apcComp.ChargePercentage)
                {
                    case > 0.33f:
                        UpdateApcChannel(apc, area, RMCPowerChannel.Equipment, true);
                        UpdateApcChannel(apc, area, RMCPowerChannel.Lighting, true);
                        UpdateApcChannel(apc, area, RMCPowerChannel.Environment, true);
                        break;
                    case > 0.16f:
                        UpdateApcChannel(apc, area, RMCPowerChannel.Equipment, false);
                        UpdateApcChannel(apc, area, RMCPowerChannel.Lighting, true);
                        UpdateApcChannel(apc, area, RMCPowerChannel.Environment, true);
                        break;
                    case > 0.01f:
                        UpdateApcChannel(apc, area, RMCPowerChannel.Equipment, false);
                        UpdateApcChannel(apc, area, RMCPowerChannel.Lighting, false);
                        UpdateApcChannel(apc, area, RMCPowerChannel.Environment, true);
                        break;
                    default:
                        UpdateApcChannel(apc, area, RMCPowerChannel.Equipment, false);
                        UpdateApcChannel(apc, area, RMCPowerChannel.Lighting, false);
                        UpdateApcChannel(apc, area, RMCPowerChannel.Environment, false);
                        break;
                }

                _appearance.SetData(apc, RMCApcVisualsLayers.Power, apcComp.ChargeStatus);
                _light.SetEnabled(apc, true);
                _light.SetColor(apc,
                    apcComp.ChargeStatus switch
                    {
                        RMCApcChargeStatus.FullCharge => Color.FromHex("#64C864"),
                        RMCApcChargeStatus.Charging => Color.FromHex("#6496FA"),
                        RMCApcChargeStatus.NotCharging => Color.FromHex("#ff3b3b"),
                        _ => Color.White,
                    });

                Dirty(apc, apcComp);
            }
        }
    }

    private void UpdateOverloadedReactorFeedback(TimeSpan time)
    {
        var reactors = EntityQueryEnumerator<RMCFusionReactorComponent, TransformComponent>();
        while (reactors.MoveNext(out var uid, out var reactor, out var xform))
        {
            if (!reactor.Overloaded)
            {
                reactor.OverloadNextFeedbackAt = TimeSpan.Zero;
                continue;
            }

            if (reactor.State != RMCFusionReactorState.Working || xform.MapUid == null)
                continue;

            if (reactor.OverloadNextFeedbackAt == TimeSpan.Zero)
            {
                reactor.OverloadNextFeedbackAt = time + GetOverloadFeedbackDelay(reactor);
                continue;
            }

            if (time < reactor.OverloadNextFeedbackAt)
                continue;

            reactor.OverloadNextFeedbackAt = time + GetOverloadFeedbackDelay(reactor);

            var hiss = _random.Prob(0.4f);
            _popup.PopupEntity(
                Loc.GetString(hiss
                    ? "rmc-fusion-reactor-overload-feedback-hiss"
                    : "rmc-fusion-reactor-overload-feedback-hum",
                    ("reactor", uid)),
                uid,
                PopupType.SmallCaution);
            _audio.PlayPvs(hiss ? reactor.OverloadHissSound : reactor.OverloadHumSound, uid);
        }
    }

    private TimeSpan GetOverloadFeedbackDelay(RMCFusionReactorComponent reactor)
    {
        var min = Math.Max(0, reactor.OverloadFeedbackMinDelay.TotalSeconds);
        var max = Math.Max(min, reactor.OverloadFeedbackMaxDelay.TotalSeconds);
        if (max <= min)
            return TimeSpan.FromSeconds(min);

        return TimeSpan.FromSeconds(_random.NextFloat((float) min, (float) max));
    }
}
