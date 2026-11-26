using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.Deploy;
using Content.Shared.CMU14.Sentry;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Flag;

/// <summary>
///     Planted flags, ported from RMC-14: deployable flags that rally the side that planted them while raised, can
///     be marked for dropships, and can be upgraded like sentries. A wrecked flag comes down and stays down until
///     it's repaired.
/// </summary>
public sealed class CMUPlantedFlagSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedDropshipWeaponSystem _dropshipWeapon = default!;
    [Dependency] private CMUFlagAuraSystem _flagAura = default!;
    [Dependency] private FixtureSystem _fixture = default!;
    [Dependency] private GunIFFSystem _gunIFF = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SentrySystem _sentry = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private readonly HashSet<EntProtoId<IFFFactionComponent>> _factions = new();
    private readonly HashSet<EntProtoId<IFFFactionComponent>> _rallied = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPlantedFlagComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUPlantedFlagComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUPlantedFlagComponent, ActivateInWorldEvent>(OnFlagActivateInWorld);
        SubscribeLocalEvent<CMUPlantedFlagComponent, CMUEquipmentDeployedEvent>(OnDeployed);
        SubscribeLocalEvent<CMUPlantedFlagComponent, CMUEquipmentUnDeployedEvent>(OnUndeployed);
        SubscribeLocalEvent<CMUPlantedFlagComponent, SentryUpgradedEvent>(OnUpgraded);
        SubscribeLocalEvent<CMUPlantedFlagComponent, CMUSentryBrokenEvent>(OnBroken);

        Subs.BuiEvents<CMUPlantedFlagComponent>(SentryUiKey.Key,
            subs =>
            {
                subs.Event<SentryUpgradeBuiMsg>(OnFlagUpgradeBuiMsg);
            });
    }

    private void OnMapInit(Entity<CMUPlantedFlagComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsServer)
        {
            ent.Comp.Id = _dropshipWeapon.ComputeNextId();
            Dirty(ent);
        }

        UpdateState(ent);
    }

    private void OnInteractUsing(Entity<CMUPlantedFlagComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp(args.Used, out SentryUpgradeItemComponent? upgrade))
            return;

        args.Handled = true;

        // Upgrading would swap a wrecked flag for a fresh one.
        if (ent.Comp.Mode == CMUFlagMode.Item && HasComp<CMUSentryBrokenComponent>(ent))
        {
            _popup.PopupClient(Loc.GetString("cmu-planted-flag-broken-cant-upgrade", ("flag", ent.Owner)),
                ent, args.User, PopupType.SmallCaution);
            return;
        }

        OpenUpgradeMenu(ent, (args.Used, upgrade), args.User);
    }

    private void OnFlagActivateInWorld(Entity<CMUPlantedFlagComponent> flag, ref ActivateInWorldEvent args)
    {
        ref var mode = ref flag.Comp.Mode;
        if (args.Handled || mode == CMUFlagMode.Item)
            return;

        args.Handled = true;

        var user = args.User;

        switch (mode)
        {
            case CMUFlagMode.Off:
            {
                if (HasComp<CMUSentryBrokenComponent>(flag))
                {
                    _popup.PopupClient(Loc.GetString("cmu-planted-flag-broken-cant-turn-on", ("flag", flag.Owner)),
                        flag, user, PopupType.SmallCaution);
                    return;
                }

                mode = CMUFlagMode.On;
                // Any side can mark its flag for its own dropships; the creator faction gates which consoles see
                // it. Dropship targets are server-authoritative.
                if (_net.IsServer && flag.Comp.Id is { } id)
                {
                    var abbreviation = _dropshipWeapon.GetUserAbbreviation(user, id);
                    _dropshipWeapon.MakeDropshipTarget(flag, abbreviation, _dropshipWeapon.GetUserFaction(user));
                }

                break;
            }
            default:
            {
                mode = CMUFlagMode.Off;
                RemoveDropshipTarget(flag);
                break;
            }
        }

        Dirty(flag);
        UpdateState(flag);
    }

    private void OnDeployed(Entity<CMUPlantedFlagComponent> ent, ref CMUEquipmentDeployedEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        ent.Comp.Mode = CMUFlagMode.Off;
        Dirty(ent);

        // Rally the deployer's side (GOVFOR/OPFOR) instead of a fixed faction.
        _flagAura.GetFactions(args.User, _factions);
        _gunIFF.ClearUserFactions(ent.Owner);
        foreach (var faction in _factions)
        {
            _gunIFF.AddUserFaction(ent.Owner, faction);
        }

        UpdateState(ent);
    }

    private void OnUndeployed(Entity<CMUPlantedFlagComponent> ent, ref CMUEquipmentUnDeployedEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        ent.Comp.Mode = CMUFlagMode.Item;
        Dirty(ent);

        RemoveDropshipTarget(ent);
        UpdateState(ent);
    }

    /// <summary>
    ///     A wrecked flag comes down and stops marking itself for dropships.
    /// </summary>
    private void OnBroken(Entity<CMUPlantedFlagComponent> flag, ref CMUSentryBrokenEvent args)
    {
        if (flag.Comp.Mode != CMUFlagMode.On)
            return;

        flag.Comp.Mode = CMUFlagMode.Off;
        Dirty(flag);

        RemoveDropshipTarget(flag);
        UpdateState(flag);
    }

    private void OnFlagUpgradeBuiMsg(Entity<CMUPlantedFlagComponent> flag, ref SentryUpgradeBuiMsg args)
    {
        // The menu can outlive the flag being planted.
        if (flag.Comp.Mode != CMUFlagMode.Item)
            return;

        _sentry.TryUpgradeSentry(flag, args.Actor, args.Upgrade, flag.Comp.Upgrades);
    }

    private void OnUpgraded(Entity<CMUPlantedFlagComponent> flag, ref SentryUpgradedEvent args)
    {
        if (!TryComp(args.OldSentry, out CMUPlantedFlagComponent? oldFlagComponent))
            return;

        flag.Comp.Id = oldFlagComponent.Id;
        Dirty(flag);
    }

    /// <summary>
    ///     Dropship targets are removed through their component lifecycle on the server.
    /// </summary>
    private void RemoveDropshipTarget(EntityUid flag)
    {
        if (_net.IsServer)
            RemComp<DropshipTargetComponent>(flag);
    }

    private void UpdateState(Entity<CMUPlantedFlagComponent> flag)
    {
        var fixture = flag.Comp.DeployFixture is { } fixtureId && TryComp(flag, out FixturesComponent? fixtures)
            ? _fixture.GetFixtureOrNull(flag, fixtureId, fixtures)
            : null;

        var mode = flag.Comp.Mode;
        if (fixture != null)
            _physics.SetHard(flag, fixture, mode != CMUFlagMode.Item);

        _appearance.SetData(flag, CMUFlagLayers.Layer, mode);
        _pointLight.SetEnabled(flag, mode == CMUFlagMode.On);
    }

    private void OpenUpgradeMenu(Entity<CMUPlantedFlagComponent> flag, Entity<SentryUpgradeItemComponent> upgrade, EntityUid user)
    {
        if (flag.Comp.Mode != CMUFlagMode.Item)
        {
            var msg = Loc.GetString("rmc-sentry-upgrade-not-item", ("sentry", flag));
            _popup.PopupEntity(msg, user, user, PopupType.SmallCaution);
            return;
        }

        if (!_sentry.CanUpgradePopup(flag, ref upgrade, user, flag.Comp.Upgrades, null))
            return;

        _ui.OpenUi(flag.Owner, SentryUiKey.Key, user);
    }

    public override void Update(float frameTime)
    {
        // Orders are applied by the server; see CMUFlagAuraSystem.
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUPlantedFlagComponent>();
        while (query.MoveNext(out var uid, out var flag))
        {
            if (flag.Mode != CMUFlagMode.On || time < flag.NextOrder)
                continue;

            // Schedule the next pulse even when nobody is in range, and share the order logic with carried and
            // mounted flags.
            flag.NextOrder = time + flag.Cooldown;
            Dirty(uid, flag);

            _gunIFF.TryGetFactions(uid, _factions);
            _flagAura.GetRalliedFactions(uid, _factions, _rallied);
            _flagAura.ApplyOrders(
                _transform.GetMapCoordinates(uid),
                flag.Range,
                flag.Strength,
                flag.Duration,
                flag.Cooldown,
                flag.ApplyFocus,
                flag.ApplyHold,
                flag.ApplyMove,
                _rallied,
                uid);
        }
    }
}
