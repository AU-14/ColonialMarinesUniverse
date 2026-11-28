using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Orders;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.Threats.Mobs.CLF;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared._RMC14.Roles;
using Content.Shared.Roles;
using Content.Shared.Mobs.Systems;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Flag;

/// <summary>
///     Applies flag order auras. Orders are server-authoritative: running the scan on clients would only
///     mispredict order components on other players in PVS.
/// </summary>
public sealed partial class CMUFlagAuraSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMarineOrdersSystem _marineOrders = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private VehicleTopologySystem _topology = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>
    ///     How often an inactive flag checks whether it is carried, planted, or mounted again.
    /// </summary>
    private static readonly TimeSpan IdleRecheck = TimeSpan.FromSeconds(0.5);

    /// <summary>
    ///     How long a pulse's orders outlast the next pulse. Without it, orders expire on the tick before they are
    ///     refreshed (or before the refresh reaches the client), and the buffs flicker.
    /// </summary>
    private static readonly TimeSpan PulseOverlap = TimeSpan.FromSeconds(0.5);

    private readonly HashSet<EntProtoId<IFFFactionComponent>> _allyFactions = new();
    private readonly HashSet<EntProtoId<IFFFactionComponent>> _flagFactions = new();
    private readonly HashSet<EntProtoId<IFFFactionComponent>> _owners = new();
    private readonly HashSet<EntProtoId<IFFFactionComponent>> _rallied = new();
    private readonly HashSet<Entity<MarineComponent>> _marines = new();

    private EntityQuery<CLFMemberComponent> _clfMemberQuery;
    private EntityQuery<OriginalRoleComponent> _originalRoleQuery;
    private EntityQuery<CMUFlagAllegianceComponent> _allegianceQuery;
    private EntityQuery<UserIFFComponent> _userIFFQuery;

    public override void Initialize()
    {
        _clfMemberQuery = GetEntityQuery<CLFMemberComponent>();
        _allegianceQuery = GetEntityQuery<CMUFlagAllegianceComponent>();
        _originalRoleQuery = GetEntityQuery<OriginalRoleComponent>();
        _userIFFQuery = GetEntityQuery<UserIFFComponent>();

        SubscribeLocalEvent<CMUFlagAuraComponent, GotEquippedHandEvent>(OnGotEquippedHand);

        InitializeCarriedFlag();
        InitializeTankFlag();
        InitializeReinforcements();
    }

    private void OnGotEquippedHand(Entity<CMUFlagAuraComponent> ent, ref GotEquippedHandEvent args)
    {
        if (_net.IsClient)
            return;

        // Planting requires holding the flag, so the last bearer is the side a planted flag rallies.
        GetFactions(args.User, ent.Comp.Factions);
    }

    /// <summary>
    ///     Collects the IFF factions of an entity: its own <see cref="UserIFFComponent"/>, its ID card, and INSFOR
    ///     membership, which some insurgents (such as CLF commandos) have without a CLF ID card.
    /// </summary>
    public void GetFactions(EntityUid uid, HashSet<EntProtoId<IFFFactionComponent>> factions)
    {
        factions.Clear();
        if (_userIFFQuery.TryComp(uid, out var userIFF))
            factions.UnionWith(userIFF.Factions);

        if (_clfMemberQuery.TryComp(uid, out var clf))
            factions.Add(clf.IFF);

        var ev = new GetIFFFactionEvent(SlotFlags.IDCARD, factions);
        RaiseLocalEvent(uid, ref ev);
    }

    /// <summary>
    ///     Works out which sides a flag rallies, given the factions of whoever carries, planted, or drives it.
    /// </summary>
    /// <remarks>
    ///     A flag tied to allegiances only rallies the sides fielding them. When both sides field the same
    ///     allegiance (UA against UA), it rallies whichever of them bears it. Flags without allegiances, and rounds
    ///     without known sides, rally the bearer's side, except reinforcement-only flags (the UN's), which rally no
    ///     side at all.
    /// </remarks>
    public void GetRalliedFactions(
        EntityUid flag,
        IReadOnlySet<EntProtoId<IFFFactionComponent>> bearer,
        HashSet<EntProtoId<IFFFactionComponent>> rallied)
    {
        rallied.Clear();
        if (!_allegianceQuery.TryComp(flag, out var allegiance) || allegiance.Allegiances.Count == 0)
        {
            if (allegiance?.Reinforcements == null)
                rallied.UnionWith(bearer);

            return;
        }

        _owners.Clear();
        var ev = new CMUGetFlagOwnersEvent(allegiance.Allegiances, _owners);
        RaiseLocalEvent(ref ev);
        if (!ev.Handled)
        {
            rallied.UnionWith(bearer);
            return;
        }

        rallied.UnionWith(_owners);
        if (rallied.Overlaps(bearer))
            rallied.IntersectWith(bearer);
    }

    /// <summary>
    ///     Gives every living marine within <paramref name="range"/> of <paramref name="center"/> who shares one of
    ///     <paramref name="factions"/> the requested orders. When <paramref name="flag"/> lists its nation's
    ///     reinforcements, those are rallied too and every other third party is left out; see
    ///     <see cref="ShouldRally"/>.
    /// </summary>
    /// <param name="pulseInterval">
    ///     How often the caller reapplies these orders. They last at least this long plus <see cref="PulseOverlap"/>,
    ///     so marines that stay in range keep their buffs without a gap.
    /// </param>
    /// <remarks>
    ///     Hold-order pain suppression refreshes instead of stacking, because flags reapply orders before the
    ///     previous application expires.
    /// </remarks>
    public void ApplyOrders(
        MapCoordinates center,
        float range,
        int strength,
        TimeSpan duration,
        TimeSpan pulseInterval,
        bool focus,
        bool hold,
        bool move,
        IReadOnlySet<EntProtoId<IFFFactionComponent>> factions,
        EntityUid? flag = null)
    {
        if (_net.IsClient)
            return;

        var reinforcements = GetReinforcements(flag);
        if (reinforcements == null && factions.Count == 0)
            return;

        var minimumDuration = pulseInterval + PulseOverlap;
        if (duration < minimumDuration)
            duration = minimumDuration;

        _marines.Clear();
        _lookup.GetEntitiesInRange(center, range, _marines);
        foreach (var marine in _marines)
        {
            if (_mobState.IsDead(marine))
                continue;

            GetFactions(marine, _allyFactions);
            if (!ShouldRally(marine, _allyFactions, factions, reinforcements))
                continue;

            if (focus)
                _marineOrders.AddOrder<FocusOrderComponent>(marine, strength, duration, stackPainSuppression: false);
            if (hold)
                _marineOrders.AddOrder<HoldOrderComponent>(marine, strength, duration, stackPainSuppression: false);
            if (move)
                _marineOrders.AddOrder<MoveOrderComponent>(marine, strength, duration, stackPainSuppression: false);
        }
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUFlagAuraComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var flag, out var xform))
        {
            if (flag.RequiresRaising)
                UpdateRaisedFlag((uid, flag, xform));

            if (time < flag.NextOrder)
                continue;

            if (GetActiveAura((uid, flag, xform), _flagFactions) is not { } aura)
            {
                flag.NextOrder = time + IdleRecheck;
                continue;
            }

            flag.NextOrder = time + aura.Cooldown;
            GetRalliedFactions(uid, _flagFactions, _rallied);
            ApplyOrders(
                _transform.GetMapCoordinates(uid, xform),
                aura.Range,
                aura.Strength,
                aura.Duration,
                aura.Cooldown,
                aura.ApplyFocus,
                aura.ApplyHold,
                aura.ApplyMove,
                _rallied,
                uid);
        }
    }

    private CMUFlagAuraStats? GetActiveAura(
        Entity<CMUFlagAuraComponent, TransformComponent> flag,
        HashSet<EntProtoId<IFFFactionComponent>> factions)
    {
        factions.Clear();

        if (flag.Comp2.Anchored)
        {
            factions.UnionWith(flag.Comp1.Factions);
            return flag.Comp1.Planted;
        }

        if (!_container.TryGetContainingContainer((flag.Owner, flag.Comp2, null), out var container))
            return null;

        var holder = container.Owner;
        if (flag.Comp1.Carried is { } carried && _hands.IsHolding(holder, flag.Owner))
        {
            GetFactions(holder, factions);
            return carried;
        }

        if (flag.Comp1.Mounted is { } mounted &&
            (!flag.Comp1.RequiresRaising || flag.Comp1.Raised) &&
            GetMountedVehicle(flag) is { } vehicle)
        {
            GetVehicleFactions(vehicle, flag.Comp1, factions);
            return mounted;
        }

        return null;
    }

    /// <summary>
    ///     A vehicle rallies its driver's side. With nobody at the wheel it keeps rallying the last driver's side,
    ///     falling back to the side whose ship supplied it.
    /// </summary>
    private void GetVehicleFactions(
        EntityUid vehicle,
        CMUFlagAuraComponent flag,
        HashSet<EntProtoId<IFFFactionComponent>> factions)
    {
        if (TryComp(vehicle, out VehicleComponent? driven) && driven.Operator is { } driver)
        {
            GetFactions(driver, factions);
            if (factions.Count > 0)
            {
                flag.Factions.Clear();
                flag.Factions.UnionWith(factions);
                return;
            }
        }

        if (flag.Factions.Count > 0)
        {
            factions.UnionWith(flag.Factions);
            return;
        }

        if (TryComp(vehicle, out VehicleEnterComponent? entrance) &&
            entrance.InteriorFaction is { } owner &&
            _prototypes.HasIndex<EntityPrototype>(owner.ToUpperInvariant()))
        {
            factions.Add(owner.ToUpperInvariant());
        }
    }
}
