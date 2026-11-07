using System.Linq;
using Content.Server.Administration.Logs;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Armor.Firewalk;
using Content.Shared._RMC14.Armor.Ghillie;
using Content.Shared._RMC14.Armor.ThermalCloak;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Stamina;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Chemistry.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Storage;
using Content.Shared.Tag;
using Content.Shared.Tools.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// Scour (phase E4, balance A-6): the Joe digests a piece of colony technology and keeps a bonus from
/// it for a while. The item is consumed; whatever it carried drops to the floor. Playtest 3: each kind
/// keeps its own timer and shows its own status alert, and a scout's thermal cloak lets the Joe cloak.
/// Bonuses live on <see cref="EWJoeScourComponent"/>; the armor part is read by the shared armor
/// event, the rest is applied here.
/// </summary>
public sealed partial class EWJoeScourSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private EWJoeMessageSystem _messages = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDestructibleSystem _destructible = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCStaminaSystem _stamina = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private ThermalCloakSystem _thermalCloak = default!;

    private static readonly ProtoId<TagPrototype>[] BatonTags = { "Stunbaton", "AU14TeleBaton" };
    private static readonly ProtoId<TagPrototype> FuelCanTag = "CMUJerryCan";
    private static readonly ProtoId<DamageTypePrototype> TorchDamage = "Heat";

    /// <summary>The status alert each buff kind shows while it lasts.</summary>
    public static readonly Dictionary<EWJoeScourKind, ProtoId<AlertPrototype>> Alerts = new()
    {
        [EWJoeScourKind.Baton] = "CMUEWJoeScourBaton",
        [EWJoeScourKind.Armor] = "CMUEWJoeScourArmor",
        [EWJoeScourKind.Fuel] = "CMUEWJoeScourFuel",
        [EWJoeScourKind.Radio] = "CMUEWJoeScourRadio",
        [EWJoeScourKind.Pyro] = "CMUEWJoeScourPyro",
        [EWJoeScourKind.Cloak] = "CMUEWJoeScourCloak",
    };

    private static readonly Dictionary<EWJoeScourKind, string> KindNames = new()
    {
        [EWJoeScourKind.Baton] = "cmu-ew-joe-scour-kind-baton",
        [EWJoeScourKind.Armor] = "cmu-ew-joe-scour-kind-armor",
        [EWJoeScourKind.Fuel] = "cmu-ew-joe-scour-kind-fuel",
        [EWJoeScourKind.Radio] = "cmu-ew-joe-scour-kind-radio",
        [EWJoeScourKind.Pyro] = "cmu-ew-joe-scour-kind-pyro",
        [EWJoeScourKind.Cloak] = "cmu-ew-joe-scour-kind-cloak",
    };

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeComponent, EWJoeScourActionEvent>(OnScour);
        SubscribeLocalEvent<EWJoeComponent, EWJoeScourDoAfterEvent>(OnScourDoAfter);
        SubscribeLocalEvent<EWJoeComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<EWJoeScourComponent, ComponentShutdown>(OnScourShutdown);
        SubscribeLocalEvent<EWJoeRemovedEvent>(OnJoeRemoved);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + TimeSpan.FromSeconds(1);

        var expired = new List<(EntityUid Joe, EWJoeScourKind Kind)>();
        var query = EntityQueryEnumerator<EWJoeScourComponent, EWJoeComponent>();
        while (query.MoveNext(out var uid, out var scour, out var joe))
        {
            foreach (var (kind, until) in scour.Expiries)
            {
                if (now >= until)
                    expired.Add((uid, kind));
            }

            // ABIL-12: a dead Joe is not mended
            if (scour.Expiries.TryGetValue(EWJoeScourKind.Fuel, out var repairUntil)
                && now < repairUntil && now >= scour.NextRepairTick && !_mobState.IsDead(uid))
            {
                scour.NextRepairTick = now + TimeSpan.FromSeconds(1);
                _damageable.HealEvenly(uid, -joe.ScourRepairPerSecond, origin: uid); // negative heals
            }
        }

        var torches = EntityQueryEnumerator<EWJoeCuttingTorchComponent>();
        while (torches.MoveNext(out var uid, out var torch))
        {
            if (torch.ActiveUntil == TimeSpan.Zero || now < torch.ActiveUntil)
                continue;

            torch.ActiveUntil = TimeSpan.Zero;
            _messages.Notify(uid, Loc.GetString("cmu-ew-joe-xenokit-torch-expired"));
        }

        foreach (var (uid, kind) in expired)
        {
            if (!TryComp(uid, out EWJoeScourComponent? scour))
                continue;

            RemoveKind((uid, scour), kind);
            _messages.Notify(uid, Loc.GetString("cmu-ew-joe-scour-kind-expired", ("kind", Loc.GetString(KindNames[kind]))));

            if (scour.Expiries.Count == 0)
                RemComp<EWJoeScourComponent>(uid);
        }
    }

    #region Action

    private void OnScour(Entity<EWJoeComponent> ent, ref EWJoeScourActionEvent args)
    {
        if (args.Handled)
            return;

        var item = args.Target;
        // 1A: with the Cutting Torch a welder is food too; it is not one of the timed kinds
        if (!IsTorchFood(ent, item) && Classify(item) == null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-scour-invalid"), ent, ent, PopupType.SmallCaution);
            return;
        }

        // ABIL-09: gear in someone's bag or hand is refused now, not after the three-second do-after
        if (_container.IsEntityInContainer(item))
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-scour-taken", ("item", item)), ent, ent, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.ScourDelay, new EWJoeScourDoAfterEvent(), ent, item)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            DistanceThreshold = 1.5f,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-scour-start", ("item", item)), ent, ent);
    }

    private void OnScourDoAfter(Entity<EWJoeComponent> ent, ref EWJoeScourDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target is not { } item)
            return;

        args.Handled = true;

        if (!Exists(item))
            return;

        var torch = IsTorchFood(ent, item);
        var kind = torch ? null : Classify(item);
        if (!torch && kind == null)
            return;

        // someone picked it up during the do-after: the Joe digests gear on the ground, not out of a hand
        if (_container.IsEntityInContainer(item))
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-scour-taken", ("item", item)), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (kind is not { } scourKind)
        {
            ScourTorch(ent, item);
            return;
        }

        Scour(ent, item, scourKind);
    }

    /// <summary>What a piece of colony technology gives the Joe, or null when it is not food for it.</summary>
    public EWJoeScourKind? Classify(EntityUid item)
    {
        if (!Exists(item))
            return null;

        // a scout's thermal cloak is a satchel, so it is checked before anything else. Only cloaks that
        // grant the scout's cloak action count: the yautja harness (grantAction: false) is not a scout cloak.
        if (TryComp(item, out ThermalCloakComponent? itemCloak) && itemCloak.GrantAction)
            return EWJoeScourKind.Cloak;

        foreach (var tag in BatonTags)
        {
            if (_tag.HasTag(item, tag))
                return EWJoeScourKind.Baton;
        }

        if (HasComp<FirewalkArmorComponent>(item))
            return EWJoeScourKind.Pyro;

        // body armor, not helmets or gloves: CM armor worn in the outer-clothing slot
        if (HasComp<CMArmorComponent>(item) && TryComp(item, out ClothingComponent? clothing)
            && (clothing.Slots & SlotFlags.OUTERCLOTHING) != 0)
            return EWJoeScourKind.Armor;

        if (_tag.HasTag(item, FuelCanTag) || TryComp(item, out ReagentTankComponent? tank) && tank.TankType == ReagentTankType.Fuel)
            return EWJoeScourKind.Fuel;

        if (HasComp<HeadsetComponent>(item) || HasComp<EncryptionKeyHolderComponent>(item))
            return EWJoeScourKind.Radio;

        return null;
    }

    /// <summary>
    /// Playtest 4: one-of-a-kind specialist gear, whose own component is whitelisted to a specialist: the
    /// scout's thermal cloak, the pyrotechnician's firewalk armor and the sniper's ghillie suit. What the
    /// Joe takes from these stays for good.
    /// </summary>
    public bool IsOneOfAKind(EntityUid item)
    {
        return TryComp(item, out ThermalCloakComponent? cloak) && cloak.GrantAction
            || HasComp<FirewalkArmorComponent>(item)
            || HasComp<GhillieSuitComponent>(item);
    }

    /// <summary>
    /// Consumes the item and applies its bonus for that kind's duration (a fuel can's is its repair
    /// window), or for good when the item is one of a kind. Scouring a kind already active refreshes only
    /// that kind, and never shortens a permanent one. Public so tests can skip the do-after.
    /// </summary>
    public void Scour(Entity<EWJoeComponent> joe, EntityUid item, EWJoeScourKind kind)
    {
        var now = _timing.CurTime;
        var scour = EnsureComp<EWJoeScourComponent>(joe);
        var permanent = IsOneOfAKind(item) || scour.Permanent.Contains(kind);
        var until = permanent
            ? TimeSpan.MaxValue
            : now + (kind == EWJoeScourKind.Fuel ? joe.Comp.ScourRepairDuration : joe.Comp.ScourDuration);
        var granted = true;

        switch (kind)
        {
            case EWJoeScourKind.Baton:
                scour.PunchStamina = joe.Comp.ScourPunchStamina;
                break;
            case EWJoeScourKind.Armor:
                scour.BulletArmor = joe.Comp.ScourBulletArmor;
                break;
            case EWJoeScourKind.Fuel:
                scour.NextRepairTick = now;
                break;
            case EWJoeScourKind.Radio:
                GrantRadio(joe, joe.Comp.ScourRadioChannels, scour);
                break;
            case EWJoeScourKind.Pyro:
                if (!HasComp<RMCImmuneToIgnitionComponent>(joe))
                {
                    EnsureComp<RMCImmuneToIgnitionComponent>(joe);
                    scour.IgnitionImmunityAdded = true;
                }
                if (!HasComp<RMCFireSlowImmunityComponent>(joe))
                {
                    EnsureComp<RMCFireSlowImmunityComponent>(joe);
                    scour.FireSlowImmunityAdded = true;
                }
                break;
            case EWJoeScourKind.Cloak:
                granted = GrantCloak(joe, item, scour);
                break;
        }

        // a cloak the Joe already has from elsewhere is left alone, and so is the item; a buff component
        // that holds nothing goes again (ABIL-09)
        if (!granted)
        {
            if (scour.Expiries.Count == 0)
                RemComp<EWJoeScourComponent>(joe);
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-scour-invalid"), joe, joe, PopupType.SmallCaution);
            return;
        }

        scour.Expiries[kind] = until;
        if (permanent)
        {
            scour.Permanent.Add(kind);
            // a permanent buff's alert has no timer to show
            _alerts.ClearAlert(joe.Owner, Alerts[kind]);
            _alerts.ShowAlert(joe.Owner, Alerts[kind]);
        }
        else
        {
            _alerts.ShowAlert(joe.Owner, Alerts[kind], cooldown: (now, until), autoRemove: false, showCooldown: true);
        }

        var itemName = Name(item);

        // a satchel, armor with pockets or a holster keeps what was stored in it: it drops where the item
        // lay. Only the storage container: an item's other containers (its actions, a light's cell) go with it.
        if (TryComp(item, out StorageComponent? storage))
            _container.EmptyContainer(storage.Container, true);

        QueueDel(item);

        _messages.Notify(joe, Loc.GetString(permanent ? "cmu-ew-joe-scour-done-permanent" : "cmu-ew-joe-scour-done",
            ("item", itemName), ("kind", Loc.GetString(KindNames[kind]))));
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"Early Warning Joe {ToPrettyString(joe):joe} scoured {itemName} ({kind})");
    }

    /// <summary>1A: a welder lying loose is Scour food for a Joe with the Cutting Torch.</summary>
    public bool IsTorchFood(EntityUid joe, EntityUid item)
    {
        return HasComp<EWJoeCuttingTorchComponent>(joe) && Exists(item) && HasComp<WelderComponent>(item);
    }

    /// <summary>
    /// 1A Cutting Torch: consumes the welder and lights the torch for its duration (refreshing a lit one).
    /// Public so tests can skip the do-after.
    /// </summary>
    public bool ScourTorch(EntityUid joe, EntityUid item)
    {
        if (!TryComp(joe, out EWJoeCuttingTorchComponent? torch))
            return false;

        torch.ActiveUntil = _timing.CurTime + torch.Duration;

        var itemName = Name(item);
        QueueDel(item);

        _messages.Notify(joe, Loc.GetString("cmu-ew-joe-xenokit-torch-done", ("item", itemName)));
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"Early Warning Joe {ToPrettyString(joe):joe} scoured {itemName} (cutting torch)");
        return true;
    }

    public bool IsTorchLit(EntityUid joe)
    {
        return TryComp(joe, out EWJoeCuttingTorchComponent? torch) && _timing.CurTime < torch.ActiveUntil;
    }

    /// <summary>
    /// 1A: a lit torch burns every xenofauna the hit lands on for its bonus and breaks xeno resin
    /// structures and weeds outright; the hive core is never one hit. Called for the Joe's own punches
    /// and from the machete hit. The burn is dealt per target so a swing that also catches a person
    /// does not burn them.
    /// </summary>
    public void ApplyCuttingTorch(EntityUid joe, MeleeHitEvent args)
    {
        if (!args.IsHit || args.Handled || !TryComp(joe, out EWJoeCuttingTorchComponent? torch)
            || _timing.CurTime >= torch.ActiveUntil)
        {
            return;
        }

        var burn = new DamageSpecifier();
        burn.DamageDict[TorchDamage] = torch.BurnBonus;

        foreach (var hit in args.HitEntities)
        {
            if (hit == joe || TerminatingOrDeleted(hit))
                continue;

            if (HasComp<XenoWeedsComponent>(hit) || HasComp<XenoConstructComponent>(hit) && !HasComp<HiveCoreComponent>(hit))
            {
                _destructible.DestroyEntity(hit);
                continue;
            }

            if (torch.BurnBonus > 0 && EWJoeTargetRules.IsXenofauna(EntityManager, hit))
                _damageable.TryChangeDamage(hit, burn, origin: joe, tool: args.Weapon);
        }
    }

    #endregion

    #region Effects

    private void OnMeleeHit(Entity<EWJoeComponent> ent, ref MeleeHitEvent args)
    {
        // the Joe's own fists; the machete forwards its hits from EWJoeArsenalSystem
        ApplyCuttingTorch(ent, args);

        if (!TryComp(ent, out EWJoeScourComponent? scour) || scour.PunchStamina <= 0)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (hit != ent.Owner)
                _stamina.DoStaminaDamage(hit, scour.PunchStamina);
        }
    }

    /// <summary>Ends every buff and undoes everything they added (stand-down).</summary>
    public void Expire(EntityUid joe, string why)
    {
        if (TryComp(joe, out EWJoeCuttingTorchComponent? torch))
            torch.ActiveUntil = TimeSpan.Zero;

        if (!HasComp<EWJoeScourComponent>(joe))
            return;

        RemComp<EWJoeScourComponent>(joe);
        _messages.Notify(joe, Loc.GetString("cmu-ew-joe-scour-expired"));
    }

    /// <summary>Undoes one kind's bonus and clears its alert.</summary>
    public void RemoveKind(Entity<EWJoeScourComponent> ent, EWJoeScourKind kind)
    {
        var scour = ent.Comp;
        switch (kind)
        {
            case EWJoeScourKind.Baton:
                scour.PunchStamina = 0;
                break;
            case EWJoeScourKind.Armor:
                scour.BulletArmor = 0;
                break;
            case EWJoeScourKind.Radio:
                RevokeRadio(ent, scour);
                break;
            case EWJoeScourKind.Pyro:
                if (scour.IgnitionImmunityAdded)
                    RemCompDeferred<RMCImmuneToIgnitionComponent>(ent);
                if (scour.FireSlowImmunityAdded)
                    RemCompDeferred<RMCFireSlowImmunityComponent>(ent);
                scour.IgnitionImmunityAdded = false;
                scour.FireSlowImmunityAdded = false;
                break;
            case EWJoeScourKind.Cloak:
                RemoveCloak(ent, scour);
                break;
        }

        scour.Expiries.Remove(kind);
        scour.Permanent.Remove(kind);
        _alerts.ClearAlert(ent.Owner, Alerts[kind]);
    }

    private void OnScourShutdown(Entity<EWJoeScourComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        foreach (var kind in ent.Comp.Expiries.Keys.ToArray())
        {
            RemoveKind(ent, kind);
        }
    }

    private void OnJoeRemoved(ref EWJoeRemovedEvent args)
    {
        Expire(args.Joe, "stood down");
    }

    /// <summary>
    /// A cloaked Joe is forced out of the cloak when it goes critical. Death is RMC's own forced decloak,
    /// which finds the Joe's own cloak (<c>ThermalCloakSystem.FindWornCloak</c>); handling it here too
    /// would uncloak twice.
    /// </summary>
    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Critical)
            return;

        if (!TryComp(args.Target, out EWJoeScourComponent? scour) || !scour.CloakAdded)
            return;

        if (TryComp(args.Target, out ThermalCloakComponent? cloak) && cloak.Enabled && HasComp<EntityTurnInvisibleComponent>(args.Target))
            _thermalCloak.SetInvisibility((args.Target, cloak), args.Target, false, true);
    }

    /// <summary>
    /// The Joe becomes its own scout cloak: the scoured cloak's settings are copied onto a cloak
    /// component on the Joe (only the Joe passes its whitelist), the wearer half the worn cloak would
    /// add on equip is added by hand, and the RMC cloak action is granted.
    /// </summary>
    /// <returns>True when the Joe holds the scoured cloak's ability (newly granted or refreshed).</returns>
    private bool GrantCloak(EntityUid joe, EntityUid item, EWJoeScourComponent scour)
    {
        if (scour.CloakAdded)
            return true;

        // a cloak from somewhere else is not the Scour buff's to manage
        if (HasComp<ThermalCloakComponent>(joe))
            return false;

        var cloak = EnsureComp<ThermalCloakComponent>(joe);
        if (TryComp(item, out ThermalCloakComponent? source))
        {
            cloak.Cooldown = source.Cooldown;
            cloak.ForcedCooldown = source.ForcedCooldown;
            cloak.Opacity = source.Opacity;
            cloak.MovingOpacity = source.MovingOpacity;
            cloak.OpacityLerpSpeed = source.OpacityLerpSpeed;
            cloak.CloakSound = source.CloakSound;
            cloak.UncloakSound = source.UncloakSound;
            cloak.RestrictWeapons = source.RestrictWeapons;
            cloak.HideNightVision = source.HideNightVision;
            cloak.BlockFriendlyFire = source.BlockFriendlyFire;
            cloak.CloakedHideLayers = new(source.CloakedHideLayers);
            cloak.UncloakWeaponLock = source.UncloakWeaponLock;
            cloak.ActionId = source.ActionId;
            cloak.CloakEffect = source.CloakEffect;
            cloak.UncloakEffect = source.UncloakEffect;
        }

        // only the Joe holds this action; a neutral whitelist keeps the networked cloak from naming the antag
        cloak.Whitelist = new EntityWhitelist { Components = ["MobState"] };
        cloak.GrantAction = false;

        if (!HasComp<EntityTurnInvisibleComponent>(joe))
            scour.TurnInvisibleAdded = true;

        var turn = EnsureComp<EntityTurnInvisibleComponent>(joe);
        turn.RestrictWeapons = cloak.RestrictWeapons;
        turn.UncloakWeaponLock = cloak.UncloakWeaponLock;
        Dirty(joe, turn);

        _actions.AddAction(joe, ref cloak.Action, cloak.ActionId);
        Dirty(joe, cloak);
        scour.CloakAdded = true;
        return true;
    }

    private void RemoveCloak(EntityUid joe, EWJoeScourComponent scour)
    {
        if (!scour.CloakAdded)
            return;

        scour.CloakAdded = false;

        if (TryComp(joe, out ThermalCloakComponent? cloak))
        {
            if (cloak.Enabled && HasComp<EntityTurnInvisibleComponent>(joe))
                _thermalCloak.SetInvisibility((joe, cloak), joe, false, true);

            if (cloak.Action is { } action)
            {
                _actions.RemoveAction(joe, action);
                QueueDel(action);
            }

            RemComp<ThermalCloakComponent>(joe);
        }

        if (scour.TurnInvisibleAdded)
            RemComp<EntityTurnInvisibleComponent>(joe);

        scour.TurnInvisibleAdded = false;
    }

    private void GrantRadio(EntityUid uid, IEnumerable<ProtoId<RadioChannelPrototype>> channels, EWJoeScourComponent scour)
    {
        if (!HasComp<ActiveRadioComponent>(uid))
            scour.AddedActiveRadio = true;
        var active = EnsureComp<ActiveRadioComponent>(uid);

        if (!HasComp<IntrinsicRadioReceiverComponent>(uid))
            scour.AddedReceiver = true;
        EnsureComp<IntrinsicRadioReceiverComponent>(uid);

        foreach (var channel in channels)
        {
            if (active.Channels.Add(channel))
                scour.RadioChannels.Add(channel);
        }
    }

    private void RevokeRadio(EntityUid uid, EWJoeScourComponent scour)
    {
        if (TryComp(uid, out ActiveRadioComponent? active))
        {
            foreach (var channel in scour.RadioChannels)
            {
                active.Channels.Remove(channel);
            }

            // only remove what the buff itself added
            if (active.Channels.Count == 0 && scour.AddedActiveRadio)
                RemCompDeferred<ActiveRadioComponent>(uid);
            if (active.Channels.Count == 0 && scour.AddedReceiver)
                RemCompDeferred<IntrinsicRadioReceiverComponent>(uid);
        }

        scour.RadioChannels.Clear();
        scour.AddedActiveRadio = false;
        scour.AddedReceiver = false;
    }

    #endregion
}
