using Content.Server.CMU14.Radio;
using Content.Server.CMU14.Round;
using Content.Server.Radio.EntitySystems;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared._RMC14.Rules;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Dropship;
using Content.Shared.CMU14.Preferences;
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
    [Dependency] private PlatoonSpawnRuleSystem _platoons = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private RMCPlanetSystem _rmcPlanet = default!;
    [Dependency] private SharedRankSystem _rank = default!;
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
        if (!TryGetJtacChannel(ev.User, out var channel, out var faction))
            return;

        var message = Loc.GetString("cmu-dropship-radio-fulton-deployed",
            ("target", PlainName(ev.Target)),
            ("location", DescribeLocation(_transform.ToMapCoordinates(ev.Location))),
            ("seconds", (int) ev.AvailableFor.TotalSeconds));

        SendClear(ev.User, message, channel, faction);
    }

    private void OnMedevacBeacon(ref CMUMedevacBeaconEvent ev)
    {
        // Strapping someone onto a stretcher only matters if its beacon is already calling for pickup.
        if (!HasComp<DropshipTargetComponent>(ev.Stretcher))
            return;

        TryComp(ev.Stretcher, out StrapComponent? strap);
        EntityUid? patient = null;
        if (strap != null)
        {
            foreach (var buckled in strap.BuckledEntities)
            {
                if (TerminatingOrDeleted(buckled))
                    continue;

                patient = buckled;
                break;
            }
        }

        var factionSource = ev.User ?? patient;
        if (factionSource is not { } source || !TryGetJtacChannel(source, out var channel, out var faction))
            return;

        var location = DescribeLocation(_transform.GetMapCoordinates(ev.Stretcher));
        string message;
        if (ev.PatientStrapped)
        {
            if (patient is not { } strapped)
                return;

            message = Loc.GetString("cmu-dropship-radio-medevac-patient",
                ("patient", PatientName(strapped)), ("status", DescribeStatus(strapped)), ("location", location));
        }
        else
        {
            message = patient is { } aboard
                ? Loc.GetString("cmu-dropship-radio-medevac-ready",
                    ("patient", PatientName(aboard)), ("status", DescribeStatus(aboard)), ("location", location))
                : Loc.GetString("cmu-dropship-radio-medevac-empty", ("location", location));
        }

        SendClear(ev.Stretcher, message, channel, faction);
    }

    /// <summary>
    /// Sends an automated call that no tower, range, jamming or garbling can stop, in the primary language of the
    /// faction's platoon.
    /// </summary>
    private void SendClear(EntityUid source, string message, ProtoId<RadioChannelPrototype> channel, string faction)
    {
        var added = !HasComp<CMUClearRadioTransmissionComponent>(source);
        EnsureComp<CMUClearRadioTransmissionComponent>(source);
        try
        {
            _radio.SendRadioMessage(source, message, channel, source, GetPlatoonLanguage(faction));
        }
        finally
        {
            if (added)
                RemComp<CMUClearRadioTransmissionComponent>(source);
        }
    }

    /// <summary>The patient's first and last name with their short rank in front, if they have one.</summary>
    private string PatientName(EntityUid patient)
    {
        var name = PlainName(patient);
        return _rank.GetRankString(patient, isShort: true) is { } rank ? $"{rank} {name}" : name;
    }

    /// <summary>First and last name only, without the character's nickname.</summary>
    private string PlainName(EntityUid uid)
    {
        var parts = CMUCharacterName.Parse(Name(uid));
        var plain = CMUCharacterName.Compose(parts.First, parts.Last, string.Empty, false);
        return plain.Length > 0 ? plain : Name(uid);
    }

    private string DescribeStatus(EntityUid patient)
    {
        var state = _mobState.IsDead(patient) ? "dead" : _mobState.IsCritical(patient) ? "critical" : "conscious";
        return Loc.GetString("cmu-dropship-radio-patient-status", ("state", state));
    }

    private bool TryGetJtacChannel(EntityUid uid, out ProtoId<RadioChannelPrototype> channel, out string faction)
    {
        channel = default;
        faction = string.Empty;
        if (!TryComp(uid, out MarineComponent? marine) || marine.Faction is not { } marineFaction)
            return false;

        faction = marineFaction.ToLowerInvariant();
        return JtacChannels.TryGetValue(faction, out channel);
    }

    /// <summary>The first language the faction's platoon speaks, or null to fall back to the default.</summary>
    private ProtoId<LanguagePrototype>? GetPlatoonLanguage(string faction)
    {
        var platoon = faction switch
        {
            "govfor" => _platoons.SelectedGovforPlatoon,
            "opfor" => _platoons.SelectedOpforPlatoon,
            _ => null,
        };

        return platoon is { Languages.Count: > 0 } ? (ProtoId<LanguagePrototype>?) platoon.Languages[0] : null;
    }

    private string DescribeLocation(MapCoordinates coordinates)
    {
        var area = _areas.TryGetArea(coordinates, out _, out var areaProto)
            ? areaProto.Name
            : Loc.GetString("cmu-dropship-radio-unknown-area");

        // The same grid the rangefinder reads out and mortars fire at.
        var position = coordinates.Position.Floored();
        if (_rmcPlanet.TryGetOffset(coordinates, out var offset))
            position += offset;

        return Loc.GetString("cmu-dropship-radio-location",
            ("area", area),
            ("x", position.X),
            ("y", position.Y));
    }
}
