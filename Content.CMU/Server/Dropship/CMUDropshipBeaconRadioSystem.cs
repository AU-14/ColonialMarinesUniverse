using System.Linq;
using Content.Server.Radio.EntitySystems;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Dropship;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Radio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Dropship;

/// <summary>
/// Calls fultons and medevac beacons in over the deploying faction's JTAC channel: what was deployed, where, how long
/// it stays available and whether there's anything for the dropship to pick up.
/// </summary>
public sealed partial class CMUDropshipBeaconRadioSystem : EntitySystem
{
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly Dictionary<string, ProtoId<RadioChannelPrototype>> JtacChannels = new()
    {
        ["govfor"] = "radioGovforJTAC",
        ["opfor"] = "radioOpforJTAC",
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUFultonDeployedEvent>(OnFultonDeployed);
        SubscribeLocalEvent<CMUMedevacBeaconEvent>(OnMedevacBeacon);
    }

    private void OnFultonDeployed(ref CMUFultonDeployedEvent ev)
    {
        if (!TryGetJtacChannel(ev.User, out var channel))
            return;

        var message = Loc.GetString("cmu-dropship-radio-fulton-deployed",
            ("target", Name(ev.Target)),
            ("location", DescribeLocation(_transform.ToMapCoordinates(ev.Location))),
            ("seconds", (int) ev.AvailableFor.TotalSeconds));

        _radio.SendRadioMessage(ev.User, message, channel, ev.User);
    }

    private void OnMedevacBeacon(ref CMUMedevacBeaconEvent ev)
    {
        // Strapping someone onto a stretcher only matters if its beacon is already calling for pickup.
        if (!HasComp<DropshipTargetComponent>(ev.Stretcher))
            return;

        TryComp(ev.Stretcher, out StrapComponent? strap);
        var patient = strap?.BuckledEntities.FirstOrDefault();
        var factionSource = ev.User ?? patient;
        if (factionSource is not { } source || !TryGetJtacChannel(source, out var channel))
            return;

        var location = DescribeLocation(_transform.GetMapCoordinates(ev.Stretcher));
        string message;
        if (ev.PatientStrapped)
        {
            if (patient is not { } strapped)
                return;

            message = Loc.GetString("cmu-dropship-radio-medevac-patient",
                ("patient", Name(strapped)), ("status", DescribeStatus(strapped)), ("location", location));
        }
        else
        {
            message = patient is { } aboard
                ? Loc.GetString("cmu-dropship-radio-medevac-ready",
                    ("patient", Name(aboard)), ("status", DescribeStatus(aboard)), ("location", location))
                : Loc.GetString("cmu-dropship-radio-medevac-empty", ("location", location));
        }

        _radio.SendRadioMessage(ev.Stretcher, message, channel, ev.Stretcher);
    }

    private string DescribeStatus(EntityUid patient)
    {
        var state = _mobState.IsDead(patient) ? "dead" : _mobState.IsCritical(patient) ? "critical" : "conscious";
        return Loc.GetString("cmu-dropship-radio-patient-status", ("state", state));
    }

    private bool TryGetJtacChannel(EntityUid uid, out ProtoId<RadioChannelPrototype> channel)
    {
        channel = default;
        return TryComp(uid, out MarineComponent? marine) &&
               marine.Faction is { } faction &&
               JtacChannels.TryGetValue(faction.ToLowerInvariant(), out channel);
    }

    private string DescribeLocation(MapCoordinates coordinates)
    {
        var area = _areas.TryGetArea(coordinates, out _, out var areaProto)
            ? areaProto.Name
            : Loc.GetString("cmu-dropship-radio-unknown-area");

        return Loc.GetString("cmu-dropship-radio-location",
            ("area", area),
            ("x", (int) MathF.Floor(coordinates.X)),
            ("y", (int) MathF.Floor(coordinates.Y)));
    }
}
