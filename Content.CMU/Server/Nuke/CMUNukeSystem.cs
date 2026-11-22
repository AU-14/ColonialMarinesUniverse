using Content.Server.CMU14.Round.Objectives;
using Content.Server._RMC14.Power;
using Content.Server._RMC14.Nuke;
using Content.Shared.CMU14.Nuke;
using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Sensor;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared.Administration.Logs;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._RMC14.Rules;
using Content.Shared.Construction.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Xenonids.Hive;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Nuke;

public sealed partial class CMUNukeSystem : EntitySystem
{
    private static readonly TimeSpan DecryptionDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMinutes(5);

    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private ObjectiveControlSystem _objectiveControl = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCPowerSystem _rmcPower = default!;
    [Dependency] private RMCNukeSystem _rmcNuke = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedXenoAnnounceSystem _xenoAnnounce = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    private readonly Random _random = new Random(67);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUNukeComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CMUNukeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUNukeComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<CMUNukeComponent, AnchorAttemptEvent>(OnAnchorAttempt);
        SubscribeLocalEvent<CMUNukeComponent, CMUNukeControlDoAfterEvent>(OnControlDoAfter);
        SubscribeLocalEvent<CMUNukeComponent, InteractHandEvent>(OnXenoSabotageInteract);
        SubscribeLocalEvent<CMUNukeComponent, CMUNukeXenoSabotageDoAfterEvent>(OnXenoSabotageDoAfter);
        SubscribeLocalEvent<CommunicationsTowerStateChangedEvent>(OnTowerStateChanged);
        SubscribeLocalEvent<SensorTowerStateChangedEvent>(OnSensorStateChanged);

        Subs.BuiEvents<CMUNukeComponent>(CMUNukeUiKey.Key, subs =>
        {
            subs.Event<CMUNukeAnchorMessage>(OnAnchorMessage);
            subs.Event<CMUNukeStartDecryptionMessage>(OnStartDecryptionMessage);
            subs.Event<CMUNukeActivateMessage>(OnActivateMessage);
            subs.Event<CMUNukeToggleSafetyMessage>(OnToggleSafetyMessage);
            subs.Event<CMUNukeToggleCommandLockoutMessage>(OnToggleCommandLockoutMessage);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUNukeComponent>();
        while (query.MoveNext(out var uid, out var nuke))
        {
            if (!nuke.Decrypted && nuke.DecryptionEndsAt is { } decryptionEnd)
            {
                if (!IsGroundLevel(uid) || !Transform(uid).Anchored || !AllTowersOnline(uid) || !AllSensorsOnline(uid))
                {
                    AbortDecryption(uid, nuke);
                    continue;
                }

                while (nuke.NextProgressAnnouncementAt is { } next && now >= next && next < decryptionEnd)
                {
                    var elapsedMinutes = (int)(next - (decryptionEnd - DecryptionDuration)).TotalMinutes;
                    Announce(
                        Loc.GetString("cmu-nuke-marine-decryption-progress", ("minutes", elapsedMinutes)),
                        Loc.GetString("cmu-nuke-xeno-decryption-progress", ("minutes", elapsedMinutes)));
                    nuke.NextProgressAnnouncementAt = next + ProgressInterval;
                }

                if (now >= decryptionEnd)
                {
                    nuke.DecryptionEndsAt = null;
                    nuke.NextProgressAnnouncementAt = null;
                    nuke.Decrypted = true;
                    nuke.LastDecodeSeconds = -1;
                    Announce(Loc.GetString("cmu-nuke-marine-decryption-complete"),
                        Loc.GetString("cmu-nuke-xeno-decryption-complete"));
                    UpdateUserInterface(uid, nuke, true);
                    continue;
                }

                UpdateUserInterface(uid, nuke);
            }

            if (nuke.DetonationAt is { } detonationAt)
            {
                if (now >= detonationAt)
                {
                    nuke.DetonationAt = null;
                    nuke.Exploded = true;
                    nuke.SafetyEnabled = true;
                    AnnounceToMarines(Loc.GetString("cmu-nuke-marine-detonated"));

                    _rmcNuke.NukeMap(Transform(uid).MapID);
                    UpdateUserInterface(uid, nuke, true);
                    _objectiveControl.DeclareObjectiveVictory("govfor", Loc.GetString("cmu-nuke-marine-detonated"));
                    continue;
                }

                var timeLeft = detonationAt - now;
                if (!nuke.DetonationSoundPlayed && timeLeft <= TimeSpan.FromSeconds(12))
                {
                    nuke.DetonationSoundPlayed = true;
                    _audio.PlayGlobal(
                        new SoundPathSpecifier($"/Audio/CMU14/Machines/nuclear_detonation{(_random.Next(0, 10) > 5 ? 2 : 1)}.ogg"),
                        Filter.Broadcast(),
                        true);
                }

                if (nuke.DetonationDelay >= TimeSpan.FromSeconds(150) &&
                    (int)nuke.WarningStatus < 1 && timeLeft <= TimeSpan.FromSeconds(150))
                {
                    nuke.WarningStatus++;
                    Announce(Loc.GetString("cmu-nuke-marine-detonation-halfway"),
                        Loc.GetString("cmu-nuke-xeno-detonation-halfway"));
                }
                else if (nuke.DetonationDelay >= TimeSpan.FromMinutes(1) &&
                        (int)nuke.WarningStatus < 2 && timeLeft <= TimeSpan.FromMinutes(1))
                {
                    nuke.WarningStatus++;
                    Announce(Loc.GetString("cmu-nuke-marine-detonation-one-minute"),
                        Loc.GetString("cmu-nuke-xeno-detonation-one-minute"),
                        Loc.GetString("cmu-nuke-yautja-detonation-one-minute"));
                }
                else if ((int)nuke.WarningStatus < 3 && timeLeft <= TimeSpan.FromSeconds(10))
                {
                    nuke.WarningStatus++;
                    Announce(Loc.GetString("cmu-nuke-marine-detonation-ten-seconds"),
                        Loc.GetString("cmu-nuke-xeno-detonation-ten-seconds"));
                }

                UpdateUserInterface(uid, nuke);
            }

        }
    }

    private void OnUiOpened(EntityUid uid, CMUNukeComponent component, BoundUIOpenedEvent args)
    {
        if (args.UiKey.Equals(CMUNukeUiKey.Key))
        {
            _adminLog.Add(LogType.Action, LogImpact.Low,
                $"{ToPrettyString(args.Actor):user} opened the controls for {ToPrettyString(uid):nuke}");
            UpdateUserInterface(uid, component, true);
        }
    }

    private void OnStartup(Entity<CMUNukeComponent> ent, ref ComponentStartup args)
    {
        UpdateVisuals(ent, ent.Comp);
    }

    private void OnAnchorChanged(EntityUid uid, CMUNukeComponent component, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored && component.DecryptionEndsAt != null)
            AbortDecryption(uid, component);
        else
            UpdateUserInterface(uid, component, true);
    }

    private void OnAnchorAttempt(EntityUid uid, CMUNukeComponent component, ref AnchorAttemptEvent args)
    {
        if (IsGroundLevel(uid))
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("cmu-nuke-ground-level-only"), uid, args.User, PopupType.MediumCaution);
    }

    private void OnXenoSabotageInteract(Entity<CMUNukeComponent> ent, ref InteractHandEvent args)
    {
        var nuke = ent.Comp;
        if (args.Handled || !CanSabotageAsXeno(args.User) ||
            nuke.DecryptionEndsAt == null && nuke.DetonationAt == null)
        {
            return;
        }

        args.Handled = true;
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.User):user} started Xeno sabotage on {ToPrettyString(ent):nuke}");

        var doAfter = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(20),
            new CMUNukeXenoSabotageDoAfterEvent(), ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(args.User):user} could not start Xeno sabotage on {ToPrettyString(ent):nuke}");
            return;
        }

        _popup.PopupEntity(Loc.GetString("cmu-nuke-xeno-sabotage-started"), ent, args.User);
    }

    private void OnXenoSabotageDoAfter(Entity<CMUNukeComponent> ent, ref CMUNukeXenoSabotageDoAfterEvent args)
    {
        var nuke = ent.Comp;
        if (args.Cancelled || !CanSabotageAsXeno(args.User) ||
            nuke.DecryptionEndsAt == null && nuke.DetonationAt == null)
        {
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(args.User):user} had Xeno sabotage interrupted on {ToPrettyString(ent):nuke}");
            return;
        }

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.User):user} sabotaged {ToPrettyString(ent):nuke}");

        if (nuke.DecryptionEndsAt != null)
        {
            AbortDecryption(ent, nuke);
            return;
        }

        nuke.DetonationAt = null;
        nuke.LastDetonationSeconds = -1;
        nuke.WarningStatus = 0;
        Announce(Loc.GetString("cmu-nuke-marine-detonation-cancelled"),
            Loc.GetString("cmu-nuke-xeno-sabotage-detonation-cancelled"),
            Loc.GetString("cmu-nuke-yautja-detonation-cancelled"));
        UpdateUserInterface(ent, nuke, true);
    }

    private bool CanSabotageAsXeno(EntityUid user)
        => TryComp<XenoComponent>(user, out var xeno) &&
           (xeno.Role == "CMXenoQueen" || xeno.Role == "RMCXenoKing" || xeno.Tier > 2);

    private void OnTowerStateChanged(ref CommunicationsTowerStateChangedEvent args)
    {
        var towerMap = Transform(args.Tower.Owner).MapUid;
        if (towerMap == null)
            return;

        UpdateNukeReadinessForMap(towerMap.Value);
    }

    private void OnSensorStateChanged(SensorTowerStateChangedEvent args)
    {
        var sensorMap = Transform(args.TowerUid).MapUid;
        if (sensorMap == null)
            return;

        UpdateNukeReadinessForMap(sensorMap.Value);
    }

    private void UpdateNukeReadinessForMap(EntityUid infrastructureMap)
    {
        var nukes = EntityQueryEnumerator<CMUNukeComponent, TransformComponent>();
        while (nukes.MoveNext(out var nukeUid, out var nuke, out var xform))
        {
            if (xform.MapUid == null || !_zLevels.IsSameZNetwork(infrastructureMap, xform.MapUid.Value))
                continue;

            if (!nuke.Decrypted && nuke.DecryptionEndsAt != null &&
                (!AllTowersOnline(nukeUid) || !AllSensorsOnline(nukeUid)))
                AbortDecryption(nukeUid, nuke);
            else
                UpdateUserInterface(nukeUid, nuke, true);
        }
    }

    private void OnAnchorMessage(Entity<CMUNukeComponent> ent, ref CMUNukeAnchorMessage args)
    {
        TryStartControlAction(ent, args.Actor, CMUNukeControlAction.ToggleAnchor);
    }

    private void OnStartDecryptionMessage(Entity<CMUNukeComponent> ent, ref CMUNukeStartDecryptionMessage args)
    {
        TryStartControlAction(ent, args.Actor,
            ent.Comp.DecryptionEndsAt != null
                ? CMUNukeControlAction.StopDecryption
                : CMUNukeControlAction.StartDecryption);
    }

    private void OnActivateMessage(Entity<CMUNukeComponent> ent, ref CMUNukeActivateMessage args)
    {
        TryStartControlAction(ent, args.Actor,
            ent.Comp.DetonationAt != null
                ? CMUNukeControlAction.Deactivate
                : CMUNukeControlAction.Activate);
    }

    private void OnToggleSafetyMessage(Entity<CMUNukeComponent> ent, ref CMUNukeToggleSafetyMessage args)
    {
        TryStartControlAction(ent, args.Actor, CMUNukeControlAction.ToggleSafety);
    }

    private void OnToggleCommandLockoutMessage(Entity<CMUNukeComponent> ent, ref CMUNukeToggleCommandLockoutMessage args)
    {
        var nuke = ent.Comp;
        if (nuke.BeingUsed || !CanUseControls(ent, nuke, args.Actor, enablingLockout: !nuke.CommandLockout))
        {
            _popup.PopupEntity(Loc.GetString("cmu-nuke-access-denied"), ent, args.Actor, PopupType.MediumCaution);
            return;
        }

        nuke.CommandLockout = !nuke.CommandLockout;
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):user} {(nuke.CommandLockout ? "enabled" : "disabled")} command lockout on {ToPrettyString(ent):nuke}");
        UpdateUserInterface(ent, nuke, true);
    }

    private void TryStartControlAction(Entity<CMUNukeComponent> ent, EntityUid user, CMUNukeControlAction action)
    {
        var nuke = ent.Comp;
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):user} attempted nuke action {action} on {ToPrettyString(ent):nuke}");
        if (nuke.BeingUsed || !CanUseControls(ent, nuke, user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-nuke-access-denied"), ent, user, PopupType.MediumCaution);
            return;
        }

        if (!CanBeginAction(ent, nuke, action))
        {
            return;
        }

        var doAfterEvent = new CMUNukeControlDoAfterEvent(action);
        var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(5), doAfterEvent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            return;
        }

        nuke.BeingUsed = true;

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):user} started {action}:action for the {nuke}:nuke");
        UpdateUserInterface(ent, nuke, true);
    }

    private bool CanUseControls(EntityUid uid, CMUNukeComponent nuke, EntityUid user, bool enablingLockout = false)
        => (!nuke.CommandLockout && !enablingLockout) || _accessReader.IsAllowed(user, uid);

    private bool CanBeginAction(EntityUid uid, CMUNukeComponent nuke, CMUNukeControlAction action)
    {
        if (!IsGroundLevel(uid))
        {
            _popup.PopupEntity(Loc.GetString("cmu-nuke-ground-level-only"), uid);
            return false;
        }

        switch (action)
        {
            case CMUNukeControlAction.ToggleSafety:
                if (nuke.DecryptionEndsAt != null || nuke.DetonationAt != null || nuke.Exploded)
                {
                    _popup.PopupEntity(Loc.GetString("cmu-nuke-controls-locked"), uid);
                    return false;
                }
                return true;
            case CMUNukeControlAction.ToggleAnchor:
                if (nuke.DecryptionEndsAt != null || nuke.DetonationAt != null || nuke.Exploded)
                {
                    _popup.PopupEntity(Loc.GetString("cmu-nuke-controls-locked"), uid);
                    return false;
                }
                if (!Transform(uid).Anchored && Transform(uid).GridUid == null)
                {
                    _popup.PopupEntity(Loc.GetString("cmu-nuke-cant-anchor-here"), uid);
                    return false;
                }
                return true;
            case CMUNukeControlAction.StartDecryption:
                if (!Transform(uid).Anchored)
                {
                    _popup.PopupEntity(Loc.GetString("cmu-nuke-must-be-anchored"), uid);
                    return false;
                }
                if (nuke.SafetyEnabled)
                {
                    _popup.PopupEntity(Loc.GetString("cmu-nuke-safety-enabled"), uid);
                    return false;
                }
                if (!CanStartDecryption(uid, nuke))
                {
                    _popup.PopupEntity(Loc.GetString("cmu-nuke-comms-not-operational"), uid);
                    return false;
                }
                return true;
            case CMUNukeControlAction.StopDecryption:
                return nuke.DecryptionEndsAt != null;
            case CMUNukeControlAction.Activate:
                if (!Transform(uid).Anchored || !nuke.Decrypted || nuke.DetonationAt != null || nuke.Exploded || nuke.SafetyEnabled)
                {
                    _popup.PopupEntity(Loc.GetString("cmu-nuke-not-ready"), uid);
                    return false;
                }
                return true;
            case CMUNukeControlAction.Deactivate:
                return nuke.DetonationAt != null && !nuke.Exploded;
            default:
                return false;
        }
    }

    private void OnControlDoAfter(Entity<CMUNukeComponent> ent, ref CMUNukeControlDoAfterEvent args)
    {
        var nuke = ent.Comp;
        nuke.BeingUsed = false;
        var completed = false;

        if (!args.Cancelled && CanUseControls(ent, nuke, args.User) && CanBeginAction(ent, nuke, args.Action))
        {
            switch (args.Action)
            {
                case CMUNukeControlAction.ToggleSafety:
                    nuke.SafetyEnabled = !nuke.SafetyEnabled;
                    break;
                case CMUNukeControlAction.ToggleAnchor:
                    var xform = Transform(ent);
                    if (xform.Anchored)
                        _transform.Unanchor(ent, xform);
                    else
                    {
                        _transform.AnchorEntity(ent, xform);
                    }
                    break;
                case CMUNukeControlAction.StartDecryption:
                    var now = _timing.CurTime;
                    nuke.DecryptionEndsAt = now + DecryptionDuration;
                    nuke.NextProgressAnnouncementAt = now + ProgressInterval;
                    nuke.LastDecodeSeconds = -1;
                    Announce(Loc.GetString("cmu-nuke-marine-decryption-started"),
                        Loc.GetString("cmu-nuke-xeno-decryption-started"));
                    break;
                case CMUNukeControlAction.StopDecryption:
                    AbortDecryption(ent, nuke);
                    break;
                case CMUNukeControlAction.Activate:
                    nuke.DetonationAt = _timing.CurTime + nuke.DetonationDelay;
                    nuke.LastDetonationSeconds = -1;
                    nuke.DetonationSoundPlayed = false;
                    nuke.WarningStatus = 0;
                    Announce(
                        Loc.GetString("cmu-nuke-marine-detonation-countdown", ("time", nuke.DetonationDelay.ToString(@"m\:ss"))),
                        Loc.GetString("cmu-nuke-xeno-detonation-countdown", ("time", nuke.DetonationDelay.ToString(@"m\:ss"))),
                        Loc.GetString("cmu-nuke-yautja-detonation-countdown", ("time", nuke.DetonationDelay.ToString(@"m\:ss"))));
                    break;
                case CMUNukeControlAction.Deactivate:
                    nuke.DetonationAt = null;
                    nuke.LastDetonationSeconds = -1;
                    Announce(Loc.GetString("cmu-nuke-marine-detonation-cancelled"),
                        Loc.GetString("cmu-nuke-xeno-detonation-cancelled"),
                        Loc.GetString("cmu-nuke-yautja-detonation-cancelled"));
                    break;
            }

            completed = true;
        }

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.User):user} {(completed ? "completed" : args.Cancelled ? "was interrupted" : "did not complete")} {args.Action}:action for the {nuke}:nuke");
        UpdateUserInterface(ent, nuke, true);

        UpdateUserInterface(ent, nuke, true);
    }

    private void AbortDecryption(EntityUid uid, CMUNukeComponent nuke, bool isPowerCut = true)
    {
        if (nuke.DecryptionEndsAt == null)
            return;

        nuke.DecryptionEndsAt = null;
        nuke.NextProgressAnnouncementAt = null;
        nuke.LastDecodeSeconds = -1;
        if (isPowerCut)
        {
            AnnounceToMarines(Loc.GetString("cmu-nuke-marine-decryption-interrupted"));
        }
        else
        {
            AnnounceToMarines(Loc.GetString("cmu-nuke-marine-decryption-interrupted-powercut"));
        }
        AnnounceToHive(Loc.GetString("cmu-nuke-xeno-decryption-interrupted"));
        UpdateUserInterface(uid, nuke, true);
    }

    private bool CanStartDecryption(EntityUid uid, CMUNukeComponent nuke)
        => IsGroundLevel(uid) &&
           Transform(uid).Anchored &&
           !nuke.SafetyEnabled &&
           !nuke.Decrypted &&
           nuke.DecryptionEndsAt == null &&
           nuke.DetonationAt == null &&
           !nuke.Exploded &&
           AllTowersOnline(uid) &&
           AllSensorsOnline(uid);

    private bool AllTowersOnline(EntityUid nukeUid)
    {
        var nukeMap = Transform(nukeUid).MapUid;
        if (nukeMap == null)
            return false;

        var foundTower = false;
        var towers = EntityQueryEnumerator<CommunicationsTowerComponent, TransformComponent>();
        while (towers.MoveNext(out _, out var tower, out var xform))
        {
            if (!_zLevels.IsSameZNetwork(xform.MapUid, nukeMap.Value))
                continue;

            foundTower = true;
            if (tower.State != CommunicationsTowerState.On || !xform.Anchored || !_rmcPower.IsPowered(xform.Owner))
                return false;
        }

        return foundTower;
    }

    private bool AllSensorsOnline(EntityUid nukeUid)
    {
        var nukeMap = Transform(nukeUid).MapUid;
        if (nukeMap == null)
            return false;

        var foundSensor = false;
        var sensors = EntityQueryEnumerator<SensorTowerComponent, TransformComponent>();
        while (sensors.MoveNext(out _, out var sensor, out var xform))
        {
            if (!_zLevels.IsSameZNetwork(xform.MapUid, nukeMap.Value))
                continue;

            foundSensor = true;
            if (sensor.State != SensorTowerState.On || !xform.Anchored)
                return false;
        }

        return foundSensor;
    }

    private bool IsGroundLevel(EntityUid uid)
    {
        var xform = Transform(uid);
        return xform.GridUid is { } grid && HasComp<RMCPlanetComponent>(grid) ||
            xform.MapUid is { } map && HasComp<RMCPlanetComponent>(map);
    }

    private void UpdateUserInterface(EntityUid uid, CMUNukeComponent nuke, bool force = false)
    {
        UpdateVisuals(uid, nuke);

        var now = _timing.CurTime;
        var decodeSeconds = nuke.DecryptionEndsAt is { } decodeAt
            ? Math.Max(0, (int)Math.Ceiling((decodeAt - now).TotalSeconds))
            : 0;
        var detonationSeconds = nuke.DetonationAt is { } detonationAt
            ? Math.Max(0, (int)Math.Ceiling((detonationAt - now).TotalSeconds))
            : 0;
        var status = nuke.DetonationAt != null
            ? CMUNukeStatus.DetonationCountdown
            : nuke.DecryptionEndsAt != null
                ? CMUNukeStatus.Decrypting
                : nuke.Decrypted
                    ? CMUNukeStatus.Decrypted
                    : CMUNukeStatus.AwaitDecode;

        if (!force && nuke.LastDecodeSeconds == decodeSeconds &&
            nuke.LastDetonationSeconds == detonationSeconds)
        {
            return;
        }

        nuke.LastDecodeSeconds = decodeSeconds;
        nuke.LastDetonationSeconds = detonationSeconds;

        var canStart = CanStartDecryption(uid, nuke);
        _ui.SetUiState(uid, CMUNukeUiKey.Key, new CMUNukeUiState
        {
            Status = status,
            DecodeTime = decodeSeconds,
            CooldownTime = detonationSeconds,
            IsAnchored = Transform(uid).Anchored,
            IsPublic = !nuke.CommandLockout,
            AllowArm = IsGroundLevel(uid) && Transform(uid).Anchored && nuke.Decrypted && !nuke.SafetyEnabled && !nuke.Exploded,
            CanStartDecryption = canStart,
            CanAnchor = IsGroundLevel(uid) && nuke.DecryptionEndsAt == null && nuke.DetonationAt == null && !nuke.Exploded,
            SafetyEnabled = nuke.SafetyEnabled,
            CommandLockout = nuke.CommandLockout,
            BeingUsed = nuke.BeingUsed,
        });
    }

    private void UpdateVisuals(EntityUid uid, CMUNukeComponent nuke)
    {
        _appearance.SetData(uid, CMUNukeVisuals.Deployed, Transform(uid).Anchored);
        _appearance.SetData(uid, CMUNukeVisuals.Decrypting, nuke.DecryptionEndsAt != null);
        _appearance.SetData(uid, CMUNukeVisuals.Activated, nuke.DetonationAt != null);
    }

    private void AnnounceToYautja(string message)
    {
        var query = EntityQueryEnumerator<YautjaComponent>();
        while (query.MoveNext(out var uid, out _) && !Deleted(uid))
            _popup.PopupEntity(message, uid, uid, PopupType.Medium);
    }

    private void AnnounceToMarines(string marineMessage)
    {
        _marineAnnounce.AnnounceHighCommand(marineMessage, sound: new SoundPathSpecifier("/Audio/Misc/notice1.ogg"));
    }

    private void AnnounceToHive(string xenoMessage)
    {
        var hives = EntityQueryEnumerator<HiveComponent>();
        while (hives.MoveNext(out var hiveUid, out var hive))
        {
            if (hive.Corrupted)
                continue;

            _xenoAnnounce.AnnounceToHive(EntityUid.Invalid, hiveUid, xenoMessage, hive.AnnounceSound);
        }
    }

    private void Announce(string marineMessage = "", string xenoMessage = "", string yautjaMessage = "")
    {
        if (!string.IsNullOrWhiteSpace(marineMessage))
            AnnounceToMarines(marineMessage);
        if (!string.IsNullOrWhiteSpace(xenoMessage))
            AnnounceToHive(xenoMessage);
        if (!string.IsNullOrWhiteSpace(yautjaMessage))
            AnnounceToYautja(yautjaMessage);
    }
}
