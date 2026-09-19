using Content.Server._RMC14.Language.Systems;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Systems;
using Content.Server.DoAfter;
using Content.Server.EUI;
using Content.Server.Ghost;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Language.Prototypes;
using InGameICChatType = Content.Shared.Chat.InGameICChatType;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Synth;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Administration;
using Content.Shared.Alert;
using Content.Shared.Bed.Sleep;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Round.Antags.Rider;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Emoting;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Conditions;
using Content.Shared.Traits.Assorted;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Examine;
using Content.Shared.Eye;
using Content.Shared.Forensics.Components;
using Content.Shared.Ghost.Components;
using Content.Shared.Inventory;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Rounding;
using Content.Shared.Radio;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server.CMU14.Round.Antags.Rider;

/// <summary>
/// The Rider antag: a hatchling that latches onto unconscious or willing hosts,
/// rides them from an internal container, and trades grip for leverage over them.
/// </summary>
public sealed partial class RiderSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly INetManager _netMan = default!;

    [Dependency] private readonly SharedCMChatSystem _chat = default!;
    [Dependency] private readonly ChatSystem _say = default!;
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPainShockSystem _pain = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly DialogSystem _dialog = default!;
    [Dependency] private readonly RMCMapSystem _rmcMap = default!;

    private const string RiderContainerSlot = "rider_hatchling_slot";
    private static readonly ProtoId<DamageTypePrototype> PunishDamage = "Blunt";

    private static readonly (string Reagent, float Dose)[] SurgeMix =
    [
        ("Epinephrine", 8f),
        ("Ephedrine", 10f),
        ("Stimulants", 6f),
        ("Bicaridine", 15f),
        ("Kelotane", 15f),
    ];

    private static readonly (string Reagent, float Dose)[] CoaxMix =
    [
        ("Tricordrazine", 10f),
        ("Bicaridine", 10f),
        ("Dermaline", 8f),
    ];

    // The phantom carries the rider's action bar.
    private static readonly string[] ManifestActions =
    [
        "ActionRiderPunish",
        "ActionRiderExit",
        "ActionRiderSurge",
        "ActionRiderCoax",
        "ActionRiderSustain",
        "ActionRiderMute",
    ];
    private static readonly ProtoId<LanguagePrototype> RiderCantLanguage = "RiderCant";
    private static readonly ProtoId<AlertPrototype> GripAlert = "CMUGrip";

    private int _characterLimit = 1000;

    public override void Initialize()
    {
        Subs.CVar(_config, CCVars.ChatMaxMessageLength, limit => _characterLimit = limit, true);

        SubscribeLocalEvent<RiderComponent, ComponentStartup>(OnHatchlingStartup);
        SubscribeLocalEvent<RiderComponent, RiderLatchActionEvent>(OnLatchAction);
        SubscribeLocalEvent<RiderComponent, RiderLatchDoAfterEvent>(OnLatchDoAfter);
        SubscribeLocalEvent<RiderComponent, StartCollideEvent>(OnSqueezeTouch);
        SubscribeLocalEvent<RiderComponent, EndCollideEvent>(OnSqueezeLeave);
        SubscribeLocalEvent<RiderOfferEvent>(OnRiderOfferAnswer);
        SubscribeLocalEvent<RiderComponent, InGameICMessageAttemptEvent>(OnICMessageAttempt);
        SubscribeAllEvent<PlayEmoteMessage>(OnRiderPlayEmote);
        SubscribeLocalEvent<RiderComponent, EmoteAttemptEvent>(OnRiderEmoteAttempt);
        SubscribeLocalEvent<RiderComponent, BeforeEmoteEvent>(OnRiderBeforeEmote);
        SubscribeLocalEvent<RiderManifestComponent, InGameICMessageAttemptEvent>(OnManifestMessageAttempt);
        SubscribeLocalEvent<RiderSeizeProxyComponent, InGameICMessageAttemptEvent>(OnSeizeProxyMessageAttempt);
        SubscribeLocalEvent<RiderComponent, RiderPunishActionEvent>(OnPunishAction);
        SubscribeLocalEvent<RiderComponent, RiderSeizeActionEvent>(OnSeizeAction);
        SubscribeLocalEvent<RiderComponent, RiderExitActionEvent>(OnExitAction);
        SubscribeLocalEvent<RiderComponent, ComponentShutdown>(OnHatchlingShutdown);
        SubscribeLocalEvent<RiderComponent, MobStateChangedEvent>(OnRiderMobState);

        SubscribeLocalEvent<RiddenComponent, HostResistActionEvent>(OnHostResist);
        SubscribeLocalEvent<RiddenComponent, RiderExitActionEvent>(OnHostExitAction);
        SubscribeLocalEvent<RiddenComponent, MobStateChangedEvent>(OnHostMobState);
        SubscribeLocalEvent<RiddenComponent, ComponentShutdown>(OnHostShutdown);
        SubscribeLocalEvent<RiddenComponent, GetVerbsEvent<Verb>>(OnHostAdminVerbs);
        SubscribeLocalEvent<RiddenComponent, HeadsetRadioReceiveRelayEvent>(OnHostRadio);
        SubscribeLocalEvent<RiddenComponent, InGameICMessageAttemptEvent>(OnHostICMessageAttempt);
        SubscribeLocalEvent<RiddenComponent, TakeGhostRoleEvent>(OnHostTakeGhostRole);
        SubscribeLocalEvent<RiddenComponent, GetVisMaskEvent>(OnHostGetVisMask);

        SubscribeLocalEvent<RiderSurgeryConditionComponent, CMSurgeryValidEvent>(OnParasiteSurgeryValid);
        SubscribeLocalEvent<RiderSurgeryStepEffectComponent, CMSurgeryStepEvent>(OnParasiteSurgeryStep);
        SubscribeLocalEvent<RiddenComponent, ExaminedEvent>(OnRiddenExamined);
        SubscribeLocalEvent<RiderComponent, RiderSurgeActionEvent>(OnSurgeAction);
        SubscribeLocalEvent<RiderComponent, RiderCoaxActionEvent>(OnCoaxAction);
        SubscribeLocalEvent<RiderComponent, RiderSustainActionEvent>(OnSustainAction);
        SubscribeLocalEvent<RiderComponent, RiderMuteActionEvent>(OnMuteAction);
        SubscribeLocalEvent<RiderComponent, RiderManifestActionEvent>(OnManifestAction);
        SubscribeLocalEvent<RiderManifestComponent, RiderWithdrawActionEvent>(OnManifestWithdraw);
        SubscribeLocalEvent<RiderManifestComponent, RiderPunishActionEvent>(OnManifestPunish);
        SubscribeLocalEvent<RiderManifestComponent, RiderExitActionEvent>(OnManifestExit);
        SubscribeLocalEvent<RiderManifestComponent, RiderSurgeActionEvent>(OnManifestSurge);
        SubscribeLocalEvent<RiderManifestComponent, RiderCoaxActionEvent>(OnManifestCoax);
        SubscribeLocalEvent<RiderManifestComponent, RiderSustainActionEvent>(OnManifestSustain);
        SubscribeLocalEvent<RiderManifestComponent, RiderMuteActionEvent>(OnManifestMute);
    }

    private void OnHatchlingStartup(Entity<RiderComponent> ent, ref ComponentStartup args)
    {
        _actions.AddAction(ent, ref ent.Comp.LatchAction, "ActionRiderLatch");
        _actions.AddAction(ent, ref ent.Comp.PunishAction, "ActionRiderPunish");
        _actions.AddAction(ent, ref ent.Comp.SeizeAction, "ActionRiderSeize");
        _actions.AddAction(ent, ref ent.Comp.ExitAction, "ActionRiderExit");
        _actions.AddAction(ent, ref ent.Comp.SurgeAction, "ActionRiderSurge");
        _actions.AddAction(ent, ref ent.Comp.CoaxAction, "ActionRiderCoax");
        _actions.AddAction(ent, ref ent.Comp.SustainAction, "ActionRiderSustain");
        _actions.AddAction(ent, ref ent.Comp.MuteAction, "ActionRiderMute");
        _actions.AddAction(ent, ref ent.Comp.ManifestAction, "ActionRiderManifest");
        ent.Comp.NextCrawlResidueAt = _timing.CurTime + TimeSpan.FromSeconds(8);
        ent.Comp.NextChoirAt = _timing.CurTime + TimeSpan.FromSeconds(30);
        ent.Comp.NextSoothePainAt = _timing.CurTime + ent.Comp.SoothePainRefresh;
    }

    #region Latch

    private void OnLatchAction(Entity<RiderComponent> ent, ref RiderLatchActionEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Host != null)
        {
            _popup.PopupEntity(Loc.GetString("rider-latch-riding"), ent, ent);
            return;
        }

        var target = args.Target;
        if (!IsRideable(target))
        {
            _popup.PopupEntity(Loc.GetString("rider-latch-invalid"), target, ent);
            return;
        }

        // An awake mind gets asked; crit, sleep and the dead are simply taken
        if (_mobState.IsAlive(target) && !IsUnconscious(target))
        {
            args.Handled = true;
            OfferRide(ent, target);
            return;
        }

        args.Handled = true;
        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.LatchDuration,
            new RiderLatchDoAfterEvent(), ent, target: target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        _popup.PopupEntity(Loc.GetString("rider-latch-start"), target, ent);
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnLatchDoAfter(Entity<RiderComponent> ent, ref RiderLatchDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (!IsRideable(target) || (_mobState.IsAlive(target) && !IsUnconscious(target)))
        {
            _popup.PopupEntity(Loc.GetString("rider-latch-failed"), target, ent);
            return;
        }

        LatchOnto(ent, target, willing: false);
    }

    private void LatchOnto(Entity<RiderComponent> ent, EntityUid host, bool willing)
    {
        if (ent.Comp.Host != null || !IsRideable(host) || _mobState.IsDead(ent.Owner))
            return;

        var fromCorpse = _mobState.IsDead(host);

        // Gate the insert before reviving: a corpse ride that fails to insert would
        // orphan the revival and a half-built RiddenComponent on the host
        var container = _container.EnsureContainer<ContainerSlot>(host, RiderContainerSlot);
        if (!_container.CanInsert(ent.Owner, container))
            return;

        if (fromCorpse)
            ReviveCorpse(ent, host);

        if (!_container.Insert(ent.Owner, container))
            return;

        ent.Comp.SqueezingDoor = null;

        var ridden = EnsureComp<RiddenComponent>(host);
        ridden.Rider = ent.Owner;
        ridden.RideStart = _timing.CurTime;
        ridden.Willing = willing;
        _actions.AddAction(host, ref ridden.ResistAction, "ActionHostResist");

        // Admin/ghost tell riding the host. Ghost layer, not Rider: manifesting
        // hands the host the Rider bit, and it must not draw markers
        var marker = Spawn("CMURiderLatchedMarker", _xform.GetMoverCoordinates(host));
        Transform(marker).AttachParent(host);
        _visibility.AddLayer(marker, (int) VisibilityFlags.Ghost, false);
        _visibility.RemoveLayer(marker, (int) VisibilityFlags.Normal, false);
        _visibility.RefreshVisibility(marker);
        ent.Comp.LatchMarker = marker;

        if (_language.TryGetCurrentLanguage(host, out var hostLanguage))
            ridden.PreRideLanguage = hostLanguage;

        ent.Comp.Host = host;
        ent.Comp.Grip = ent.Comp.GripStart;
        UpdateGripAlert(ent);
        ent.Comp.HostsRidden++;
        // A willing host is interaction from the first second; they count
        ent.Comp.RideCredited = willing;
        ent.Comp.NextTellAt = _timing.CurTime + TimeSpan.FromMinutes(10);
        ent.Comp.NextShedAt = _timing.CurTime + TimeSpan.FromMinutes(20);

        _adminLogger.Add(LogType.AntagSelection, LogImpact.High,
            $"{ToPrettyString(ent):rider} latched onto {ToPrettyString(host):host} ({(willing ? "willing" : fromCorpse ? "corpse" : "unconscious")})");

        SendToHost(ent.Owner, host, Loc.GetString("rider-host-latched"),
            Loc.GetString("rider-host-latched-wrap"));
    }

    private bool IsRideable(EntityUid uid)
    {
        // Any mob state rides: alive, critical, or dead
        if (!HasComp<MobStateComponent>(uid))
            return false;

        return HasComp<HumanoidProfileComponent>(uid)
            && !HasComp<SynthComponent>(uid)
            && !HasComp<UnrevivableComponent>(uid)
            && !HasComp<RiddenComponent>(uid)
            && !HasComp<RiderComponent>(uid);
    }

    private bool IsUnconscious(EntityUid uid)
        => HasComp<SleepingComponent>(uid)
           || HasComp<ForcedSleepingStatusEffectComponent>(uid)
           || _mobState.IsCritical(uid);

    private void RiderPopup(Entity<RiderComponent> ent, string key)
    {
        var view = ent.Comp.Manifest ?? ent.Owner;
        _popup.PopupEntity(Loc.GetString(key), view, view);
    }

    // The punish methodology: calm is half strength, harm mode is real.
    private float RideIntensity(Entity<RiderComponent> ent)
        => _combatMode.IsInCombatMode(ent.Comp.Manifest ?? ent.Owner) ? 1f : 0.5f;

    private void ReviveCorpse(Entity<RiderComponent> ent, EntityUid host)
    {
        // The parasite rebuilds the dead flesh whole; rot and injuries reset with it
        RaiseLocalEvent(host, new RejuvenateEvent());
        _mobState.ChangeMobState(host, MobState.Alive);

        _adminLogger.Add(LogType.AntagSelection, LogImpact.High,
            $"{ToPrettyString(ent):rider} revived the corpse of {ToPrettyString(host):host}");

        if (_mind.TryGetMind(host, out var mindId, out var mind))
        {
            if (_players.TryGetSessionById(mind.UserId, out var session))
            {
                if (mind.CurrentEntity != host || mind.IsVisitingEntity)
                {
                    _eui.OpenEui(new ReturnToBodyEui(mind, _mind, _players), session);
                    EnsureComp<RiddenComponent>(host).HostReturnEndsAt =
                        _timing.CurTime + ent.Comp.HostReturnWindow;
                }
                return;
            }

            _mind.TransferTo(mindId, null);
        }

        OpenHostRaffle(host);
    }

    private void OpenHostRaffle(EntityUid host)
    {
        var ghostRole = EnsureComp<GhostRoleComponent>(host);
        ghostRole.RoleName = Loc.GetString("rider-ghost-host-name");
        ghostRole.RoleDescription = Loc.GetString("rider-ghost-host-description");
        ghostRole.RoleRules = Loc.GetString("rider-ghost-host-rules");
        EnsureComp<GhostTakeoverAvailableComponent>(host);
    }

    private void OfferRide(Entity<RiderComponent> ent, EntityUid host)
    {
        // No player, no answer; sleeping NPCs are still taken the normal way
        if (!HasComp<ActorComponent>(host))
        {
            _popup.PopupEntity(Loc.GetString("rider-latch-invalid"), host, ent);
            return;
        }

        if (ent.Comp.OfferedTo == host && _timing.CurTime < ent.Comp.OfferExpiresAt)
        {
            _popup.PopupEntity(Loc.GetString("rider-offer-pending"), ent, ent);
            return;
        }

        ent.Comp.OfferedTo = host;
        ent.Comp.OfferExpiresAt = _timing.CurTime + ent.Comp.OfferCooldown;

        var options = new List<DialogOption>
        {
            new DialogOption(Loc.GetString("rider-offer-accept"), new RiderOfferEvent(GetNetEntity(ent), true)),
            new DialogOption(Loc.GetString("rider-offer-refuse"), new RiderOfferEvent(GetNetEntity(ent), false)),
        };

        _dialog.OpenOptions(host, host, Loc.GetString("rider-offer-title"), options,
            Loc.GetString("rider-offer-message"));
        _popup.PopupEntity(Loc.GetString("rider-offer-sent"), ent, ent);
        _adminLogger.Add(LogType.Chat, LogImpact.Low,
            $"{ToPrettyString(ent):rider} offered itself to {ToPrettyString(host):host}");
    }

    private void OnRiderOfferAnswer(RiderOfferEvent args)
    {
        if (!TryGetEntity(args.Rider, out var riderNet)
            || riderNet is not { } riderUid
            || !TryComp<RiderComponent>(riderUid, out var rider)
            || rider.OfferedTo is not { } host)
            return;

        if (!args.Accept)
        {
            rider.OfferedTo = null;
            _popup.PopupEntity(Loc.GetString("rider-offer-refused"), riderUid, riderUid);
            return;
        }

        if (_timing.CurTime >= rider.OfferExpiresAt)
        {
            _popup.PopupEntity(Loc.GetString("rider-offer-lapsed"), host, host);
            return;
        }

        // A spent dialog answers once; a refused or missed host needs a fresh offer
        rider.OfferedTo = null;

        // They may have walked off while thinking it over
        if (Transform(riderUid).MapID != Transform(host).MapID
            || (_xform.GetWorldPosition(riderUid) - _xform.GetWorldPosition(host)).Length() > 3f)
        {
            _popup.PopupEntity(Loc.GetString("rider-offer-far"), riderUid, riderUid);
            return;
        }

        LatchOnto((riderUid, rider), host, willing: true);
    }

    private void OnHostTakeGhostRole(Entity<RiddenComponent> ent, ref TakeGhostRoleEvent args)
    {
        _chat.ChatMessageToOne(ChatChannel.Local,
            Loc.GetString("rider-ghost-host-takeover"),
            Loc.GetString("rider-ghost-host-takeover"),
            ent,
            false,
            args.Player.Channel);
    }

    private void OnSqueezeTouch(Entity<RiderComponent> ent, ref StartCollideEvent args)
    {
        // Latched riders sit in a container and never touch doors
        if (ent.Comp.Host != null || ent.Comp.SqueezingDoor != null)
            return;

        // Contact only exists for doors that physically block SmallMobMask:
        // welded airlocks (LayerChangeOnWeld swaps the layer to WallLayer),
        // shutters (WallLayer) and poddoors (FullTileLayer).
        if (!TryComp<DoorComponent>(args.OtherEntity, out var door)
            || door.State is not (DoorState.Closed or DoorState.Closing or DoorState.Welded))
            return;

        ent.Comp.SqueezingDoor = args.OtherEntity;
        ent.Comp.SqueezeDoneAt = _timing.CurTime + ent.Comp.SqueezeDuration;
    }

    private void OnSqueezeLeave(Entity<RiderComponent> ent, ref EndCollideEvent args)
    {
        if (ent.Comp.SqueezingDoor != args.OtherEntity)
            return;

        ent.Comp.SqueezingDoor = null;
    }

    private void SqueezeThrough(Entity<RiderComponent> ent, EntityUid door)
    {
        // Corpses do not crawl; a body shoved against a hatch stays put
        if (_mobState.IsDead(ent))
            return;

        var doorPos = _xform.GetWorldPosition(door);
        var delta = doorPos - _xform.GetWorldPosition(ent);
        if (delta.Length() < 0.01f)
            return;

        // Cardinal exit only: a diagonal corner approach must not land the
        // hatchling inside a wall tile flanking the door
        var exit = delta.Normalized();
        if (MathF.Abs(exit.X) >= MathF.Abs(exit.Y))
            exit.Y = 0;
        else
            exit.X = 0;

        // The far tile must be standing room: a wall or vacuum opposite the door
        // would swallow the crossing
        var landing = new MapCoordinates(doorPos + exit, Transform(ent).MapID);
        if (!_rmcMap.TryGetTileDef(landing, out var landingTile)
            || landingTile.ID == ContentTileDefinition.SpaceID
            || _rmcMap.IsTileBlocked(landing))
            return;

        _xform.SetWorldPosition(ent, doorPos + exit);

        // The crossing marks the frame; dusting finds the trail
        var forensics = EnsureComp<ForensicsComponent>(door);
        forensics.Residues.Add(Loc.GetString("rider-crawl-residue"));
        Dirty(door, forensics);

        _popup.PopupEntity(Loc.GetString("rider-squeeze-through"), ent, ent);
    }

    #endregion

    #region Levers

    private void OnICMessageAttempt(Entity<RiderComponent> ent, ref InGameICMessageAttemptEvent args)
    {
        // Hostless the hatchling just talks for itself in RiderCant; only
        // riding hijacks its voice
        if (ent.Comp.Host is not { } host)
            return;

        var raw = args.Message.Trim();
        if (raw.Length > _characterLimit)
            raw = raw[.._characterLimit].Trim();

        if (raw.Length == 0)
        {
            args.Cancelled = true;
            return;
        }

        args.Cancelled = true;

        if (args.Type == InGameICChatType.Speak)
        {
            // Out loud: the words come out of the host's mouth, radio prefixes
            // and all, so ":h" speaks on the host's channels
            if (!_blocker.CanSpeak(host))
            {
                RiderPopup(ent, "rider-speak-blocked");
                return;
            }

            if (!SpendGrip(ent, ent.Comp.SpeakCost, felt: false))
            {
                RiderPopup(ent, "rider-grip-low");
                return;
            }

            // A starving rider cannot hold the throat: one word clicks over
            // into RiderCant, and sharp ears learn what that means
            if (ent.Comp.Grip < ent.Comp.MaskSlipGripBelow
                && _random.Prob(ent.Comp.MaskSlipChance))
            {
                raw = SlipMask(raw);
                RiderPopup(ent, "rider-mask-slip");
            }

            // Mimicry follows the rider's chosen tongue when the host can
            // speak it; downed hosts cannot swap languages themselves
            if (_language.TryGetCurrentLanguage(ent.Owner, out var riderLanguage)
                && _language.CanSpeak(host, riderLanguage)
                && _language.TryGetCurrentLanguage(host, out var hostLanguage)
                && hostLanguage != riderLanguage)
            {
                _language.SetLanguage((host, null), riderLanguage);
            }

            _say.TrySendInGameICMessage(host, raw, InGameICChatType.Speak, hideChat: false);
            _adminLogger.Add(LogType.Chat, LogImpact.Medium,
                $"{ToPrettyString(ent):rider} spoke through {ToPrettyString(host):host}: {raw}");
            return;
        }

        if (args.Type == InGameICChatType.Emote)
        {
            // Emotes come out of the host as well.
            _say.TrySendInGameICMessage(host, raw, InGameICChatType.Emote, hideChat: false);
            return;
        }

        // Whisper: free, private to the host, garbled if they sleep, echoed
        // back to the rider so they can keep track of what they have said
        var heard = HasComp<SleepingComponent>(host)
            ? Garble(raw)
            : raw;
        var shown = FormattedMessage.EscapeText(heard);

        SendToHost(ent.Owner, host, shown, Loc.GetString("rider-whisper-wrap", ("text", shown)));

        NetUserId? author = null;
        if (_mind.TryGetMind(ent.Owner, out _, out var riderMind))
        {
            author = riderMind.UserId;

            if (_players.TryGetSessionById(riderMind.UserId, out var riderSession))
            {
                _chat.ChatMessageToOne(ChatChannel.Local, shown,
                    Loc.GetString("rider-whisper-echo", ("text", shown)), ent.Owner, false, riderSession.Channel);
            }
        }

        // Ghost copies keep aghosts in the conversation; living crew get nothing
        var ghosts = Filter.Empty().AddWhereAttachedEntity(HasComp<GhostComponent>);
        _chat.ChatMessageToMany(shown,
            Loc.GetString("rider-whisper-ghost",
                ("rider", ent.Owner),
                ("host", host),
                ("message", shown)),
            ghosts,
            ChatChannel.Local,
            ent.Owner,
            recordReplay: true,
            author: author);

        _adminLogger.Add(LogType.Chat, LogImpact.Low,
            $"{ToPrettyString(ent):rider} whispered to {ToPrettyString(host):host}: {heard}");
    }

    // The emote menu sends PlayEmoteMessage straight to TryEmoteWithChat and
    // never raises InGameICMessageAttemptEvent, so OnICMessageAttempt never
    // sees menu emotes. Cancel the hatchling's own emote below and replay the
    // chosen emote from the host instead, same as typed emotes do above.
    private void OnRiderPlayEmote(PlayEmoteMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player
            || !_proto.TryIndex(msg.ProtoId, out EmotePrototype? proto)
            || proto.ChatTriggers.Count == 0)
            return;

        // The phantom projects from the same body, so its menu emotes come
        // out of the host exactly like the hatchling's
        Entity<RiderComponent> rider;
        if (TryComp<RiderComponent>(player, out var hatchling))
            rider = new Entity<RiderComponent>(player, hatchling);
        else if (TryComp<RiderManifestComponent>(player, out var manifest)
            && TryComp<RiderComponent>(manifest.Rider, out var projected))
            rider = new Entity<RiderComponent>(manifest.Rider, projected);
        else
            return;

        if (rider.Comp.Host is not { } host)
            return;

        _say.TryEmoteWithChat(host, proto);
    }

    // While riding, the hatchling never emotes under its own name; the emote
    // comes out of the host or not at all
    private void OnRiderEmoteAttempt(Entity<RiderComponent> ent, ref EmoteAttemptEvent args)
    {
        if (ent.Comp.Host is not null)
            args.Cancel();
    }

    // Deathgasp and suffocation gasps ignore action blocker, so they skip the
    // attempt event and land here instead
    private void OnRiderBeforeEmote(Entity<RiderComponent> ent, ref BeforeEmoteEvent args)
    {
        if (ent.Comp.Host is not null)
            args.Cancel();
    }

    private void OnManifestMessageAttempt(Entity<RiderManifestComponent> ent, ref InGameICMessageAttemptEvent args)
    {
        if (!TryComp<RiderComponent>(ent.Comp.Rider, out var rider))
        {
            args.Cancelled = true;
            return;
        }

        // The phantom itself never talks. Whatever it says leaves the host:
        // speech with radio prefixes, whispers, typed emotes
        args.Cancelled = true;
        var relayed = args with { Cancelled = false };
        OnICMessageAttempt((ent.Comp.Rider, rider), ref relayed);
    }

    private void OnSeizeProxyMessageAttempt(Entity<RiderSeizeProxyComponent> ent, ref InGameICMessageAttemptEvent args)
        => args.Cancelled = true;

    // The mute eats the host's own chat input only. The rider's mimicry is
    // forwarded with a null session, so the impostor keeps the mouth while
    // the owner cannot tattle. Never during a seize: the body speaks for the
    // rider then, and the clamped owner is parked on the proxy.
    private void OnHostICMessageAttempt(Entity<RiddenComponent> ent, ref InGameICMessageAttemptEvent args)
    {
        if (args.Cancelled || args.Session == null)
            return;

        if (!TryComp<RiderComponent>(ent.Comp.Rider, out var rider)
            || rider.SeizeActive
            || _timing.CurTime >= rider.MutedUntil)
        {
            return;
        }

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("rider-mute-blocked"), ent, ent);
    }

    private void OnPunishAction(Entity<RiderComponent> ent, ref RiderPunishActionEvent args)
    {
        if (ent.Comp.Host is not { } host)
        {
            RiderPopup(ent, "rider-no-host");
            return;
        }

        if (!SpendGrip(ent, ent.Comp.PunishCost))
        {
            RiderPopup(ent, "rider-grip-low");
            return;
        }

        // The stick. Willingness is a one-way door set at accept. A deep
        // grip pool hits harder, but harm mode is what turns the press lethal.
        var multiplier = RideIntensity(ent);
        var severity = (_random.NextFloat(10, 20) + ent.Comp.Grip * ent.Comp.PunishGripDamageScale) * multiplier;
        // Pressure from inside, not structural trauma: the body-only projection
        // skips part localization, so punish can crit a host but never fracture
        // bones, hit organs, or leave anything that needs a doctor
        _damageable.ApplyBodyDamageProjection(host, new DamageSpecifier(_proto.Index(PunishDamage), severity));
        _popup.PopupEntity(Loc.GetString("rider-punish-host"), host, host, PopupType.LargeCaution);
        _adminLogger.Add(LogType.Damaged, LogImpact.Medium,
            $"{ToPrettyString(ent):rider} punished {ToPrettyString(host):host}");
    }

    private void OnMuteAction(Entity<RiderComponent> ent, ref RiderMuteActionEvent args)
    {
        if (ent.Comp.Host is not { } host)
        {
            RiderPopup(ent, "rider-no-host");
            return;
        }

        if (!SpendGrip(ent, ent.Comp.MuteCost))
        {
            RiderPopup(ent, "rider-grip-low");
            return;
        }

        ent.Comp.MutedUntil = _timing.CurTime + ent.Comp.MuteDuration;
        _popup.PopupEntity(Loc.GetString("rider-mute-host"), host, host, PopupType.MediumCaution);
        RiderPopup(ent, "rider-mute-cast");
        _adminLogger.Add(LogType.Chat, LogImpact.Medium,
            $"{ToPrettyString(ent):rider} muted {ToPrettyString(host):host}");
    }

    private void OnSeizeAction(Entity<RiderComponent> ent, ref RiderSeizeActionEvent args)
    {
        if (ent.Comp.Host is not { } host)
        {
            _popup.PopupEntity(Loc.GetString("rider-no-host"), ent, ent);
            return;
        }

        if (ent.Comp.SeizeActive)
            return;

        if (ent.Comp.Manifest != null)
            return;

        if (ent.Comp.Grip < ent.Comp.SeizeGate)
        {
            _popup.PopupEntity(Loc.GetString("rider-grip-low"), ent, ent);
            return;
        }

        if (!_mind.TryGetMind(ent.Owner, out var riderMindId, out _))
            return;

        if (_mind.TryGetMind(host, out var hostMindId, out _))
        {
            var proxy = EnsureSeizeProxy(ent, host);
            _mind.Visit(hostMindId, proxy);
        }

        _mind.Visit(riderMindId, host);

        ent.Comp.SeizeActive = true;
        ent.Comp.SeizeEndsAt = _timing.CurTime + ent.Comp.SeizeDuration;
        _actions.AddAction(host, ref ent.Comp.SeizeExitAction, "ActionRiderExit");
        _popup.PopupEntity(Loc.GetString("rider-seize-host"), host, host, PopupType.LargeCaution);
        _adminLogger.Add(LogType.AntagSelection, LogImpact.High,
            $"{ToPrettyString(ent):rider} seized {ToPrettyString(host):host}");
    }

    private EntityUid EnsureSeizeProxy(Entity<RiderComponent> ent, EntityUid host)
    {
        if (ent.Comp.SeizeProxy is { } existing && !TerminatingOrDeleted(existing))
            return existing;

        var proxy = Spawn("CMURiderSeizeProxy", _xform.GetMapCoordinates(host));
        ent.Comp.SeizeProxy = proxy;

        EnsureComp<AlertsComponent>(proxy);

        // Ghost layer: living players never draw the spectator
        _visibility.AddLayer(proxy, (int) VisibilityFlags.Ghost, false);
        _visibility.RemoveLayer(proxy, (int) VisibilityFlags.Normal, false);
        _visibility.RefreshVisibility(proxy);
        return proxy;
    }

    private void EndSeize(Entity<RiderComponent> ent, bool natural)
    {
        if (!ent.Comp.SeizeActive || ent.Comp.Host is not { } host)
            return;

        if (_mind.TryGetMind(ent.Owner, out var riderMindId, out _))
            _mind.UnVisit(riderMindId);

        if (_mind.TryGetMind(host, out var hostMindId, out _))
            _mind.UnVisit(hostMindId);

        if (ent.Comp.SeizeProxy is { } proxy)
        {
            Del(proxy);
            ent.Comp.SeizeProxy = null;
        }

        if (ent.Comp.SeizeExitAction is { } seizeExit)
        {
            _actions.RemoveAction(host, seizeExit);
            ent.Comp.SeizeExitAction = null;
        }

        ent.Comp.SeizeActive = false;
        ent.Comp.Grip = MathF.Max(ent.Comp.Grip - ent.Comp.SeizeCost, ent.Comp.SeizeFloor);
        UpdateGripAlert(ent);

        if (natural)
            _popup.PopupEntity(Loc.GetString("rider-seize-end-host"), host, host, PopupType.Medium);

        _adminLogger.Add(LogType.AntagSelection, LogImpact.Medium,
            $"{ToPrettyString(ent):rider} ended a seizure of {ToPrettyString(host):host}");
    }

    private bool SpendGrip(Entity<RiderComponent> ent, float cost, bool felt = true)
    {
        if (ent.Comp.Grip < ent.Comp.GripDisableBelow
            || ent.Comp.Grip < cost)
            return false;

        ent.Comp.Grip -= cost;

        // The host feels the leash tug on every spend. Speech opts out: the
        // host already watches their own mouth move in the chat log
        if (felt && ent.Comp.Host is { } feelHost)
            _popup.PopupEntity(Loc.GetString("rider-grip-feel"), feelHost, feelHost);

        UpdateGripAlert(ent);
        return true;
    }

    // One word of the line goes over to the rider's own wet clicking, drawn
    // from RiderCant's own syllables so crew can learn to recognize it
    private string SlipMask(string raw)
    {
        var words = raw.Split(' ');

        // The leading ":u" style token is a channel key, never a word to garble
        var first = raw.StartsWith(':') ? 1 : 0;
        if (words.Length <= first)
            return raw;

        var pick = _random.Next(first, words.Length);
        words[pick] = _language.ObfuscateMessage(words[pick], RiderCantLanguage);
        return string.Join(' ', words);
    }

    private void UpdateGripAlert(Entity<RiderComponent> ent)
    {
        if (ent.Comp.GripMax == 0)
            return;

        if (_mobState.IsDead(ent.Owner))
        {
            _alerts.ClearAlert((ent.Owner, null), GripAlert);
            return;
        }

        var max = _alerts.GetMaxSeverity(GripAlert);
        var severity = max - ContentHelpers.RoundToLevels(ent.Comp.Grip, ent.Comp.GripMax, max + 1);
        var message = $"{(int) ent.Comp.Grip} / {(int) ent.Comp.GripMax}";
        _alerts.ShowAlert((ent.Owner, null), GripAlert, (short) severity, dynamicMessage: message);

        if (ent.Comp.SeizeActive
            && ent.Comp.SeizeProxy is { } proxy
            && !TerminatingOrDeleted(proxy))
            _alerts.ShowAlert((proxy, null), GripAlert, (short) severity, dynamicMessage: message);

        if (ent.Comp.Manifest is { } manifest
            && !TerminatingOrDeleted(manifest))
            _alerts.ShowAlert((manifest, null), GripAlert, (short) severity, dynamicMessage: message);

        // A willing partner reads the same gauge. An unwilling host never
        // gets to count the cards
        if (ent.Comp.Host is { } bonded
            && !TerminatingOrDeleted(bonded)
            && CompOrNull<RiddenComponent>(bonded)?.Willing == true)
            _alerts.ShowAlert((bonded, null), GripAlert, (short) severity, dynamicMessage: message);
    }

    #endregion

    #region Exits

    private void OnExitAction(Entity<RiderComponent> ent, ref RiderExitActionEvent args)
    {
        if (ent.Comp.Host is not { } host)
        {
            RiderPopup(ent, "rider-no-host");
            return;
        }

        EndSeize(ent, true);
        Eject(ent, host, loud: !IsUnconscious(host));
    }

    private void OnHostMobState(Entity<RiddenComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || !TryComp<RiderComponent>(ent.Comp.Rider, out var rider))
            return;

        // The rider cannot die on host death; it is ejected alive and stunned
        var riderEnt = new Entity<RiderComponent>(ent.Comp.Rider, rider);
        EndSeize(riderEnt, false);
        Eject(riderEnt, ent.Owner, loud: true, stunned: true);
    }

    // Symmetry with host death: a dead parasite left inside would keep the
    // voice hijack and block re-latching forever
    private void OnRiderMobState(Entity<RiderComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || ent.Comp.Host is not { } host)
            return;

        Eject(ent, host, loud: true);
    }

    private void OnHostShutdown(Entity<RiddenComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<RiderComponent>(ent.Comp.Rider, out var rider))
            return;

        // Host body going away (cryo, deletion); leave at its last position
        var riderEnt = new Entity<RiderComponent>(ent.Comp.Rider, rider);
        EndSeize(riderEnt, false);
        Eject(riderEnt, ent.Owner, loud: false);
    }

    private void OnHostGetVisMask(Entity<RiddenComponent> ent, ref GetVisMaskEvent args)
    {
        if (TryComp<RiderComponent>(ent.Comp.Rider, out var rider)
            && rider.Manifest != null)
            args.VisibilityMask |= (int) VisibilityFlags.Rider;
    }

    private void OnManifestAction(Entity<RiderComponent> ent, ref RiderManifestActionEvent args)
    {
        if (ent.Comp.Host is not { } host)
        {
            _popup.PopupEntity(Loc.GetString("rider-no-host"), ent, ent);
            return;
        }

        if (ent.Comp.SeizeActive || ent.Comp.Manifest != null)
            return;

        if (!_mind.TryGetMind(ent.Owner, out var riderMindId, out _))
            return;

        var manifest = Spawn("CMURiderManifest", _xform.GetMoverCoordinates(host));
        Comp<RiderManifestComponent>(manifest).Rider = ent.Owner;

        _visibility.AddLayer(manifest, (int) VisibilityFlags.Rider, false);
        _visibility.RemoveLayer(manifest, (int) VisibilityFlags.Normal, false);
        _visibility.RefreshVisibility(manifest);
        EnsureComp<AlertsComponent>(manifest);
        _actions.AddAction(manifest, ref ent.Comp.WithdrawAction, "ActionRiderWithdraw");

        // Minds eye is the same body in a different skin: the whole bar comes
        // along, and abilities run on the buried body via the manifest relays
        foreach (var action in ManifestActions)
            _actions.AddAction(manifest, action);

        ent.Comp.Manifest = manifest;
        _mind.Visit(riderMindId, manifest);
        _eye.RefreshVisibilityMask(host);
        _adminLogger.Add(LogType.AntagSelection, LogImpact.Low,
            $"{ToPrettyString(ent):rider} manifested to {ToPrettyString(host):host}");
    }

    // The withdraw action lives on the manifest, so the event is raised there
    private void OnManifestWithdraw(Entity<RiderManifestComponent> ent, ref RiderWithdrawActionEvent args)
    {
        if (!TryComp<RiderComponent>(ent.Comp.Rider, out var rider))
            return;

        EndManifest(new Entity<RiderComponent>(ent.Comp.Rider, rider));
    }

    // Manifest actions belong to the phantom; the ability itself runs on the
    // buried body, whose handlers aim feedback at the player via RiderPopup
    private bool RelayManifest<T>(Entity<RiderManifestComponent> ent, out Entity<T> rider) where T : IComponent
    {
        if (TryComp<T>(ent.Comp.Rider, out var comp))
        {
            rider = new Entity<T>(ent.Comp.Rider, comp);
            return true;
        }

        rider = default;
        return false;
    }

    private void OnManifestPunish(Entity<RiderManifestComponent> ent, ref RiderPunishActionEvent args)
    {
        if (RelayManifest(ent, out Entity<RiderComponent> rider))
            OnPunishAction(rider, ref args);
    }

    private void OnManifestExit(Entity<RiderManifestComponent> ent, ref RiderExitActionEvent args)
    {
        if (RelayManifest(ent, out Entity<RiderComponent> rider))
            OnExitAction(rider, ref args);
    }

    private void OnManifestSurge(Entity<RiderManifestComponent> ent, ref RiderSurgeActionEvent args)
    {
        if (RelayManifest(ent, out Entity<RiderComponent> rider))
            OnSurgeAction(rider, ref args);
    }

    private void OnManifestCoax(Entity<RiderManifestComponent> ent, ref RiderCoaxActionEvent args)
    {
        if (RelayManifest(ent, out Entity<RiderComponent> rider))
            OnCoaxAction(rider, ref args);
    }

    private void OnManifestSustain(Entity<RiderManifestComponent> ent, ref RiderSustainActionEvent args)
    {
        if (RelayManifest(ent, out Entity<RiderComponent> rider))
            OnSustainAction(rider, ref args);
    }

    private void OnManifestMute(Entity<RiderManifestComponent> ent, ref RiderMuteActionEvent args)
    {
        if (RelayManifest(ent, out Entity<RiderComponent> rider))
            OnMuteAction(rider, ref args);
    }

    private void EndManifest(Entity<RiderComponent> ent)
    {
        if (ent.Comp.Manifest is not { } manifest)
            return;

        if (_mind.TryGetMind(ent.Owner, out var riderMindId, out _))
            _mind.UnVisit(riderMindId);

        // Clear the link before refreshing: the vis mask handler reads Manifest,
        // and refreshing first would leave the Rider bit stuck on the host
        Del(manifest);
        ent.Comp.Manifest = null;
        ent.Comp.WithdrawAction = null;

        if (ent.Comp.Host is { } endedHost)
            _eye.RefreshVisibilityMask(endedHost);
    }

    private void RemoveLatchMarker(Entity<RiderComponent> ent)
    {
        if (ent.Comp.LatchMarker is { } marker)
            QueueDel(marker);

        ent.Comp.LatchMarker = null;
    }

    private void Eject(Entity<RiderComponent> ent, EntityUid host, bool loud, bool stunned = false)
    {
        if (ent.Comp.Host != host)
            return;

        EndSeize(ent, false);
        EndManifest(ent);
        RemoveLatchMarker(ent);

        RestoreHostLanguage(host);

        if (TerminatingOrDeleted(host) || TerminatingOrDeleted(ent.Owner))
        {
            // The host body is going away and would delete its transform children.
            // Detach first; its components die with it and touching them would re-enter shutdown.
            _xform.AttachToGridOrMap(ent.Owner);
            _alerts.ClearAlert((ent.Owner, null), GripAlert);
            ent.Comp.Host = null;
            return;
        }

        if (_container.TryGetContainer(host, RiderContainerSlot, out var container))
            _container.Remove(ent.Owner, container, force: true);

        if (loud)
            _popup.PopupEntity(Loc.GetString("rider-eject-loud", ("host", host)),
                host, Filter.PvsExcept(ent.Owner), true);

        // Disabled: guaranteed 3-second stun on every host death is too
        // much until the hatchling's survival rate is known.
        // The stunned parameter and the _stun dependency stay for this block
        // if (stunned)
        //     _stun.TryStun(ent.Owner, TimeSpan.FromSeconds(3), true);

        _alerts.ClearAlert((ent.Owner, null), GripAlert);
        ent.Comp.Host = null;
        if (ent.Comp.RideCredited)
            ent.Comp.CreditedHosts++;

        ent.Comp.RideCredited = false;
        _adminLogger.Add(LogType.AntagSelection, LogImpact.Medium,
            $"{ToPrettyString(ent):rider} left {ToPrettyString(host):host} ({(loud ? "loud" : "silent")})");

        if (TryComp<RiddenComponent>(host, out var ridden))
        {
            if (ridden.ResistAction is { } action)
                _actions.RemoveAction(host, action);

            // The partner's gauge goes dark with the ride
            _alerts.ClearAlert((host, null), GripAlert);

            RemComp<RiddenComponent>(host);
        }
    }

    #endregion

    #region Host side

    private void OnHatchlingShutdown(Entity<RiderComponent> ent, ref ComponentShutdown args)
    {
        EndSeize(ent, false);
        EndManifest(ent);
        RemoveLatchMarker(ent);

        if (ent.Comp.Host is not { } host)
            return;

        RestoreHostLanguage(host);

        // Component removal on a live rider must free it from the host's slot;
        // a contained hatchling has no exit once RiddenComponent is gone
        if (!TerminatingOrDeleted(host)
            && !TerminatingOrDeleted(ent.Owner)
            && _container.TryGetContainer(host, RiderContainerSlot, out var container))
            _container.Remove(ent.Owner, container, force: true);

        ent.Comp.Host = null;

        if (TryComp<RiddenComponent>(host, out var ridden))
        {
            if (ridden.ResistAction is { } action)
                _actions.RemoveAction(host, action);

            _alerts.ClearAlert((host, null), GripAlert);

            RemComp<RiddenComponent>(host);
        }
    }

    // Mimicry may have left the host speaking a tongue they never chose
    private void RestoreHostLanguage(EntityUid host)
    {
        if (TryComp<RiddenComponent>(host, out var ridden)
            && ridden.PreRideLanguage is { } preRide)
            _language.SetLanguage((host, null), preRide);
    }

    private void OnHostResist(Entity<RiddenComponent> ent, ref HostResistActionEvent args)
    {
        if (TryComp<RiderComponent>(ent.Comp.Rider, out var rider) && rider.SeizeActive)
            return;

        ent.Comp.ResistActive = !ent.Comp.ResistActive;

        // The button must show the state it is in, or hosts fight blind
        _actions.SetToggled(ent.Comp.ResistAction, ent.Comp.ResistActive);

        if (!ent.Comp.ResistActive)
            return;

        _popup.PopupEntity(Loc.GetString("rider-resist-start", ("host", ent.Owner)),
            ent.Owner, Filter.PvsExcept(ent.Owner), true);
        _stamina.TakeStaminaDamage(ent.Owner, 4);
    }

    private void OnHostExitAction(Entity<RiddenComponent> ent, ref RiderExitActionEvent args)
    {
        if (!TryComp<RiderComponent>(ent.Comp.Rider, out var rider)
            || !rider.SeizeActive)
            return;

        EndSeize((ent.Comp.Rider, rider), true);
    }

    private void OnHostRadio(Entity<RiddenComponent> ent, ref HeadsetRadioReceiveRelayEvent args)
    {
        // Sensory parity: what the host's headset receives, the rider hears too.
        if (!TryComp<RiderComponent>(ent.Comp.Rider, out var rider)
            || !_mind.TryGetMind(ent.Comp.Rider, out _, out var riderMind)
            || !_players.TryGetSessionById(riderMind.UserId, out var session))
            return;

        _netMan.ServerSendMessage(args.RelayedEvent.ChatMsg, session.Channel);
    }

    private void OnHostAdminVerbs(Entity<RiddenComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!_admin.HasAdminFlag(args.User, AdminFlags.Admin))
            return;

        var riderUid = ent.Comp.Rider;
        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("rider-verb-inspect"),
            Category = VerbCategory.Admin,
            Act = () =>
            {
                if (!TryComp<RiderComponent>(riderUid, out var rider))
                    return;

                var minutes = (int) rider.TotalRideTime.TotalMinutes;
                var message = Loc.GetString("rider-admin-inspect",
                    ("rider", riderUid),
                    ("host", ent.Owner),
                    ("grip", (int) rider.Grip),
                    ("hosts", rider.HostsRidden),
                    ("minutes", minutes));
                _popup.PopupCursor(message, user);
            },
        });

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("rider-verb-eject"),
            Category = VerbCategory.Admin,
            Act = () =>
            {
                if (TryComp<RiderComponent>(riderUid, out var rider))
                {
                    EndSeize((riderUid, rider), false);
                    Eject((riderUid, rider), ent.Owner, loud: true);
                }
            },
        });
    }

    #endregion

    #region Tells and medicine (phase 2)

    private void OnParasiteSurgeryValid(Entity<RiderSurgeryConditionComponent> ent, ref CMSurgeryValidEvent args)
    {
        if (!HasComp<RiddenComponent>(args.Body))
            args.Cancelled = true;
    }

    private void OnParasiteSurgeryStep(Entity<RiderSurgeryStepEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!TryComp<RiddenComponent>(args.Body, out var ridden)
            || !TryComp<RiderComponent>(ridden.Rider, out var rider))
            return;

        var riderEnt = new Entity<RiderComponent>(ridden.Rider, rider);
        EndSeize(riderEnt, false);
        Eject(riderEnt, args.Body, loud: true);
        _popup.PopupEntity(Loc.GetString("rider-extracted"), args.Body, Filter.Pvs(args.Body), true);
        _adminLogger.Add(LogType.AntagSelection, LogImpact.High,
            $"{ToPrettyString(args.User):surgeon} cut {ToPrettyString(ridden.Rider):rider} out of {ToPrettyString(args.Body):host}");
    }

    private void OnRiddenExamined(Entity<RiddenComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !TryComp<RiderComponent>(ent.Comp.Rider, out var rider))
            return;

        var ride = _timing.CurTime - ent.Comp.RideStart;
        if (ride >= TimeSpan.FromMinutes(45))
            args.PushText(Loc.GetString("rider-examine-heavy"));
        else if (ride >= TimeSpan.FromMinutes(25))
            args.PushText(Loc.GetString("rider-examine-medium"));
        else if (ride >= TimeSpan.FromMinutes(10))
            args.PushText(Loc.GetString("rider-examine-mild"));
    }

    private void OnSurgeAction(Entity<RiderComponent> ent, ref RiderSurgeActionEvent args)
    {
        if (ent.Comp.Host is not { } host)
        {
            RiderPopup(ent, "rider-no-host");
            return;
        }

        if (CompOrNull<RiddenComponent>(host) is not { } ridden)
            return;

        if (!ridden.Willing)
        {
            RiderPopup(ent, "rider-surge-refused");
            return;
        }

        if (!_solutions.TryGetSolution(host, "bloodstream", out var soln)
            || !PourMix(soln.Value, SurgeMix, RideIntensity(ent)))
            return;

        // Spend only after the dose lands; a full bloodstream must not eat grip
        if (!SpendGrip(ent, ent.Comp.SurgeCost))
        {
            RiderPopup(ent, "rider-grip-low");
            return;
        }

        _popup.PopupEntity(Loc.GetString("rider-surge-host"), host, host);
        _adminLogger.Add(LogType.ChemicalReaction, LogImpact.Low,
            $"{ToPrettyString(ent):rider} granted resilience to {ToPrettyString(host):host}");
    }

    private bool PourMix(Entity<SolutionComponent> soln, (string Reagent, float Dose)[] mix, float scale)
    {
        var poured = false;
        foreach (var (reagent, dose) in mix)
            poured |= _solutions.TryAddReagent(soln, reagent, FixedPoint2.New(dose * scale), out _);

        return poured;
    }

    private void OnCoaxAction(Entity<RiderComponent> ent, ref RiderCoaxActionEvent args)
    {
        if (ent.Comp.Host is not { } host)
        {
            RiderPopup(ent, "rider-no-host");
            return;
        }

        // The carrot for a host who has not accepted you: real medicine, no
        // stims, visible to any scanner. The dose lands before grip is spent.
        if (!_solutions.TryGetSolution(host, "bloodstream", out var soln)
            || !PourMix(soln.Value, CoaxMix, RideIntensity(ent)))
            return;

        if (!SpendGrip(ent, ent.Comp.CoaxCost))
        {
            RiderPopup(ent, "rider-grip-low");
            return;
        }

        _popup.PopupEntity(Loc.GetString("rider-coax-host"), host, host);
        _adminLogger.Add(LogType.Healed, LogImpact.Low,
            $"{ToPrettyString(ent):rider} coaxed {ToPrettyString(host):host}");
    }

    private void OnSustainAction(Entity<RiderComponent> ent, ref RiderSustainActionEvent args)
    {
        if (ent.Comp.Host is not { } host)
        {
            RiderPopup(ent, "rider-no-host");
            return;
        }

        if (!SpendGrip(ent, ent.Comp.SustainCost))
        {
            RiderPopup(ent, "rider-grip-low");
            return;
        }

        var heal = new DamageSpecifier
        {
            DamageDict =
            {
                ["Blunt"] = -35,
                ["Slash"] = -15,
                ["Burn"] = -10,
            },
        };
        _damageable.TryChangeDamage(host, heal);
        _popup.PopupEntity(Loc.GetString("rider-sustain-host"), host, host);
        _adminLogger.Add(LogType.Healed, LogImpact.Low,
            $"{ToPrettyString(ent):rider} sustained {ToPrettyString(host):host}");
    }

    private void DepositCrawlResidue(EntityUid hatchling)
    {
        foreach (var uid in _lookup.GetEntitiesInRange(hatchling, 0.75f))
        {
            // FixturesComponent: shared-side stand-in for the client-only sprite check
            if (uid == hatchling || HasComp<MobStateComponent>(uid) || !HasComp<FixturesComponent>(uid))
                continue;

            var forensics = EnsureComp<ForensicsComponent>(uid);
            forensics.Residues.Add(Loc.GetString("rider-crawl-residue"));
            Dirty(uid, forensics);
            break;
        }
    }

    private void ShedResidue(EntityUid host)
    {
        if (!_inventory.TryGetSlotEntity(host, "outerClothing", out var clothing)
            && !_inventory.TryGetSlotEntity(host, "jumpsuit", out clothing))
        {
            return;
        }

        var forensics = EnsureComp<ForensicsComponent>(clothing.Value);
        forensics.Residues.Add(Loc.GetString("rider-crawl-residue"));
        Dirty(clothing.Value, forensics);
    }

    #endregion

    public override void Update(float frameTime)
    {
        var enumerator = EntityQueryEnumerator<RiderComponent>();
        while (enumerator.MoveNext(out var uid, out var comp))
        {
            if (comp.Host == null)
            {
                if (comp.SqueezingDoor is { } door)
                {
                    if (TerminatingOrDeleted(door)
                        || !TryComp<DoorComponent>(door, out var doorComp)
                        || doorComp.State is not (DoorState.Closed or DoorState.Closing or DoorState.Welded))
                        comp.SqueezingDoor = null;
                    else if (_timing.CurTime >= comp.SqueezeDoneAt)
                    {
                        comp.SqueezingDoor = null;
                        SqueezeThrough((uid, comp), door);
                    }
                }

                // Crawl residue: the hatchling's trail feeds the scanner's unused
                // Residues field and gives marshals a colony-first discovery path
                if (_timing.CurTime >= comp.NextCrawlResidueAt)
                {
                    comp.NextCrawlResidueAt = _timing.CurTime + TimeSpan.FromSeconds(8);
                    DepositCrawlResidue(uid);
                }

                // The choir helps riders find each other before any host exists
                if (_timing.CurTime >= comp.NextChoirAt)
                {
                    comp.NextChoirAt = _timing.CurTime + TimeSpan.FromSeconds(30);
                    ChoirHum((uid, comp));
                }

                continue;
            }

            var host = comp.Host.Value;
            if (TerminatingOrDeleted(host))
                continue;

            if (comp.SeizeActive)
            {
                if (_timing.CurTime >= comp.SeizeEndsAt)
                    EndSeize((uid, comp), true);
                else if (comp.SeizeProxy is { } proxy && !TerminatingOrDeleted(proxy))
                {
                    // The spectator drifts with the body; pull it back inside the leash
                    var hostPos = _xform.GetWorldPosition(host);
                    var delta = _xform.GetWorldPosition(proxy) - hostPos;
                    if (Transform(proxy).MapID != Transform(host).MapID)
                        _xform.SetCoordinates(proxy, _xform.GetMoverCoordinates(host));
                    else if (delta.Length() > comp.SeizeProxyLeash)
                        _xform.SetWorldPosition(proxy, hostPos + delta.Normalized() * comp.SeizeProxyLeash);
                }
            }

            if (comp.Manifest is { } phantom)
            {
                if (TerminatingOrDeleted(phantom))
                {
                    comp.Manifest = null;
                    comp.WithdrawAction = null;
                    _eye.RefreshVisibilityMask(host);
                }
                else
                {
                    // The phantom drifts free but may not leave the host's side
                    var phantomDelta = _xform.GetWorldPosition(phantom) - _xform.GetWorldPosition(host);
                    if (Transform(phantom).MapID != Transform(host).MapID)
                        _xform.SetCoordinates(phantom, _xform.GetMoverCoordinates(host));
                    else if (phantomDelta.Length() > comp.ManifestLeash)
                        _xform.SetWorldPosition(phantom, _xform.GetWorldPosition(host) + phantomDelta.Normalized() * comp.ManifestLeash);
                }
            }

            comp.TotalRideTime += TimeSpan.FromSeconds(frameTime);

            if (!IsUnconscious(host))
                comp.RideCredited = true;

            var ridden = CompOrNull<RiddenComponent>(host);
            var resisting = ridden?.ResistActive == true;

            var baseRegen = ridden?.Willing == true
                ? comp.GripCoopRegenPerMinute
                : comp.GripRegenPerMinute;

            // Resisting strangles the refill by how empty the tank is: a full
            // hold barely notices, a starving one barely refills at all
            var fill = comp.GripMax > 0 ? comp.Grip / comp.GripMax : 1f;
            var suppression = resisting ? comp.GripResistRegenSuppression * (1f - fill) : 0f;

            comp.GripAccumulator += baseRegen * (1f - suppression) * frameTime / 60f;
            if (comp.GripAccumulator >= 1)
            {
                var whole = MathF.Floor(comp.GripAccumulator);
                comp.GripAccumulator -= whole;
                comp.Grip = MathF.Min(comp.GripMax, comp.Grip + whole);
            }

            if (resisting)
            {
                // Drain is a fraction of the pool per second, so resistance
                // is a percentage war, not a flat trickle
                comp.Grip -= comp.GripMax / comp.ResistDrainSeconds * frameTime;
                _stamina.TakeStaminaDamage(host, 5 * frameTime, visual: false);
                if (comp.Grip <= 0)
                {
                    comp.Grip = 0;
                    Eject((uid, comp), host, loud: true);
                    continue;
                }
            }

            if (comp.MutedUntil != TimeSpan.Zero && _timing.CurTime >= comp.MutedUntil)
            {
                comp.MutedUntil = TimeSpan.Zero;
                _popup.PopupEntity(Loc.GetString("rider-mute-end"), host, host);
            }

            // An owner who never reclaimed the revived body loses it to the raffle
            if (ridden is { HostReturnEndsAt: { } returnBy } && _timing.CurTime >= returnBy)
            {
                ridden.HostReturnEndsAt = null;

                var claimed = false;
                if (_mind.TryGetMind(host, out var mindId, out var mind))
                {
                    if (mind.CurrentEntity == host && !mind.IsVisitingEntity)
                        claimed = true;
                    else
                        _mind.TransferTo(mindId, null);
                }

                if (!claimed)
                    OpenHostRaffle(host);
            }

            if (comp.Grip >= comp.SootheThreshold)
            {
                _stamina.TakeStaminaDamage(host, -0.5f * frameTime, visual: false);
                if (!comp.Soothing)
                {
                    comp.Soothing = true;
                    _popup.PopupEntity(Loc.GetString("rider-soothe"), host, host);
                }
            }
            else if (comp.Soothing)
            {
                comp.Soothing = false;
                _popup.PopupEntity(Loc.GetString("rider-soothe-end"), host, host);
            }

            if (comp.Soothing || ridden?.Willing == true)
            {
                if (_timing.CurTime >= comp.NextSoothePainAt)
                {
                    comp.NextSoothePainAt = _timing.CurTime + comp.SoothePainRefresh;
                    _pain.AddAdditivePainSuppressionProfile(host,
                        comp.SoothePainAccumulation,
                        comp.SoothePainTier,
                        comp.SoothePainDecayBonus,
                        comp.SoothePainRefresh);
                }
            }

            if (_timing.CurTime >= comp.NextTellAt && ridden != null)
            {
                // Tells describe this host's wear, not the rider's career total
                var ride = _timing.CurTime - ridden.RideStart;
                if (ride >= TimeSpan.FromMinutes(45))
                {
                    // TODO: fauna agitation
                    _popup.PopupEntity(Loc.GetString("rider-tell-heavy", ("entName", host)), host, Filter.Pvs(host), true);
                    comp.NextTellAt = _timing.CurTime + TimeSpan.FromSeconds(25);
                }
                else if (ride >= TimeSpan.FromMinutes(25))
                {
                    _popup.PopupEntity(Loc.GetString("rider-tell-medium", ("entName", host)), host, Filter.Pvs(host), true);
                    comp.NextTellAt = _timing.CurTime + TimeSpan.FromSeconds(40);
                }
                else
                {
                    _popup.PopupEntity(Loc.GetString("rider-tell-mild", ("entName", host)), host, Filter.Pvs(host), true);
                    comp.NextTellAt = _timing.CurTime + TimeSpan.FromSeconds(60);
                }
            }

            // Bedding and clothing traces after twenty minutes
            if (_timing.CurTime >= comp.NextShedAt)
            {
                comp.NextShedAt = _timing.CurTime + TimeSpan.FromMinutes(2);
                ShedResidue(host);
            }

            // Grip regens continuously, so the alert refreshes on a cadence
            // instead of on every point of change
            if (_timing.CurTime >= comp.NextGripPushAt)
            {
                comp.NextGripPushAt = _timing.CurTime + TimeSpan.FromSeconds(1);
                UpdateGripAlert((uid, comp));
            }

            // The host reads grip qualitatively and only hears when the band changes
            if (ridden == null)
                continue;

            int band;
            if (comp.Grip >= comp.GripTightThreshold)
                band = 2;
            else if (comp.Grip < comp.GripDisableBelow)
                band = 0;
            else
                band = 1;

            if (band == ridden.GripBand)
                continue;

            ridden.GripBand = band;
            var key = band switch
            {
                2 => "rider-grip-tight",
                0 => "rider-grip-thread",
                _ => "rider-grip-loose",
            };
            _popup.PopupEntity(Loc.GetString(key), host, host);
        }
    }

    #region Choir and objectives (phase 3)

    private void ChoirHum(Entity<RiderComponent> ent)
    {
        var myPos = _xform.GetWorldPosition(ent);
        var myMap = Transform(ent).MapID;

        EntityUid? nearest = null;
        var best = float.MaxValue;
        var others = EntityQueryEnumerator<RiderComponent, TransformComponent>();
        while (others.MoveNext(out var other, out _, out var xform))
        {
            if (other == ent.Owner || xform.MapID != myMap)
                continue;

            var distance = (_xform.GetWorldPosition(other) - myPos).Length();
            if (distance < best)
            {
                best = distance;
                nearest = other;
            }
        }

        if (nearest is not { } otherRider)
            return;

        var delta = _xform.GetWorldPosition(otherRider) - myPos;
        var angle = Math.Atan2(delta.Y, delta.X) * 180 / Math.PI;
        var sector = ((int) Math.Round(angle / 45) + 8) % 8;
        var direction = sector switch
        {
            0 => "rider-choir-east",
            1 => "rider-choir-northeast",
            2 => "rider-choir-north",
            3 => "rider-choir-northwest",
            4 => "rider-choir-west",
            5 => "rider-choir-southwest",
            6 => "rider-choir-south",
            _ => "rider-choir-southeast",
        };

        var distanceKey = best switch
        {
            < 15 => "rider-choir-close",
            < 60 => "rider-choir-near",
            _ => "rider-choir-far",
        };

        _popup.PopupEntity(Loc.GetString("rider-choir-hum",
            ("direction", Loc.GetString(direction)),
            ("distance", Loc.GetString(distanceKey))), ent, ent);
    }

    /// <summary>
    /// Round-end win check by flavor: still riding (Hitchhiker), three credited
    /// hosts (Leapfrog), or riding a host who carries the target (Puppeteer).
    /// </summary>
    public bool EvaluateWin(Entity<RiderComponent> ent)
        => ent.Comp.Flavor switch
        {
            RiderFlavor.Leapfrog => ent.Comp.CreditedHosts >= 3,
            RiderFlavor.Puppeteer => ent.Comp.Host is { } host && HostCarries(host, ent.Comp.PuppeteerItem),
            _ => ent.Comp.Host != null,
        };

    private bool HostCarries(EntityUid host, EntProtoId? item)
    {
        if (item is not { } proto)
            return false;

        var target = _proto.Index(proto);
        var slots = _inventory.GetSlotEnumerator(host);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is { } contained
                && CarriesAnywhere(contained, target))
                return true;
        }

        foreach (var held in _hands.EnumerateHeld(host))
        {
            if (CarriesAnywhere(held, target))
                return true;
        }

        return false;
    }

    private bool CarriesAnywhere(EntityUid uid, EntityPrototype target)
    {
        if (MetaData(uid).EntityPrototype == target)
            return true;

        // The puppeteer goal counts inside backpacks and nested containers too
        foreach (var container in _container.GetAllContainers(uid))
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (CarriesAnywhere(contained, target))
                    return true;
            }
        }

        return false;
    }

    #endregion

    private void SendToHost(EntityUid rider, EntityUid host, string message, string wrapped)
    {
        if (!_mind.TryGetMind(host, out _, out var mind)
            || !_players.TryGetSessionById(mind.UserId, out var session))
            return;

        _chat.ChatMessageToOne(ChatChannel.Local, message, wrapped, rider, false, session.Channel);
    }

    /// <summary>
    /// Dream garble: sleepers get fragments, not transcripts. Sleep must never
    /// be a perfectly silent, perfectly readable channel.
    /// </summary>
    private string Garble(string text)
    {
        var kept = text.Split(' ').Where(w => w.Length > 0 && _random.Prob(0.4f)).ToArray();
        return kept.Length == 0 ? "..." : string.Join(" ... ", kept);
    }
}
