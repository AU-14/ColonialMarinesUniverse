using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Prototypes;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Xenonids.Hive;

public abstract partial class SharedXenoHiveSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedNightVisionSystem _nightVision = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private XenoSystem _xeno = default!;
    [Dependency] private SharedXenoAnnounceSystem _xenoAnnounce = default!;

    private EntityQuery<HiveComponent> _query;
    private EntityQuery<HiveMemberComponent> _memberQuery;

    public override void Initialize()
    {
        _query = GetEntityQuery<HiveComponent>();
        _memberQuery = GetEntityQuery<HiveMemberComponent>();

        SubscribeLocalEvent<DropshipHijackStartEvent>(OnDropshipHijackStart);

        SubscribeLocalEvent<HiveComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<HiveMemberComponent, ComponentStartup>(OnHiveStartup);

        SubscribeLocalEvent<XenoEvolutionGranterComponent, MobStateChangedEvent>(OnGranterMobStateChanged);

        SubscribeLocalEvent<AutoAssignHiveComponent, ComponentStartup>(OnAutoAssignHiveAdded);

        SubscribeLocalEvent<HiveGunComponent, AmmoShotEvent>(OnHiveGunShot);
    }

    private void OnDropshipHijackStart(ref DropshipHijackStartEvent ev)
    {
        // Evolution boost is xeno-specific; skip for human-vs-human hijacks
        if (ev.IsHumanHijack)
            return;

        var hives = EntityQueryEnumerator<HiveComponent>();
        while (hives.MoveNext(out var uid, out var hive))
        {
            if (hive.HijackSurged)
                continue;

            hive.HijackSurged = true;
            Dirty(uid, hive);

            var boost = Spawn(null, MapCoordinates.Nullspace);
            var evoOverride = EnsureComp<EvolutionOverrideComponent>(boost);
            evoOverride.Amount = 10;
            Dirty(boost, evoOverride);

            EnsureComp<TimedDespawnComponent>(boost).Lifetime = 180;
            break;
        }
    }

    public void OnHiveStartup(Entity<HiveMemberComponent> ent, ref ComponentStartup args)
    {
        var hiveUid = ent.Comp.Hive;
        if (hiveUid == null ||
            !TryComp<HiveComponent>(hiveUid.Value, out var hive))
            return;

        UpdateHiveAppearance(ent.Owner, (hiveUid.Value, hive));
        Dirty(ent);
    }

    private void OnGranterMobStateChanged(Entity<XenoEvolutionGranterComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (GetHive(ent.Owner) is { } hive)
        {
            hive.Comp.LastQueenDeath = _timing.CurTime;
            hive.Comp.CurrentQueen = null;
            hive.Comp.AnnouncedQueenDeathCooldownOver = false;
            hive.Comp.NewQueenAt = _timing.CurTime + hive.Comp.NewQueenCooldown;
            Dirty(hive);
        }
    }

    private void OnMapInit(Entity<HiveComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.AnnouncedUnlocks.Clear();
        ent.Comp.Unlocks.Clear();
        ent.Comp.AnnouncementsLeft.Clear();

        foreach (var prototype in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (!prototype.TryGetComponent(out XenoComponent? xeno, _compFactory))
                continue;

            if (xeno.UnlockAt == TimeSpan.Zero || prototype.HasComponent<XenoHiddenComponent>(_compFactory))
                continue;

            ent.Comp.Unlocks.GetOrNew(xeno.UnlockAt).Add(prototype.ID);

            if (!ent.Comp.AnnouncementsLeft.Contains(xeno.UnlockAt))
                ent.Comp.AnnouncementsLeft.Add(xeno.UnlockAt);
        }

        foreach (var unlock in ent.Comp.Unlocks)
        {
            unlock.Value.Sort();
        }

        ent.Comp.AnnouncementsLeft.Sort();
    }

    /// <summary>
    /// Tries to get the hive from a member, returning null if it has no hive or it is invalid.
    /// </summary>
    public Entity<HiveComponent>? GetHive(Entity<HiveMemberComponent?> member)
    {
        if (!_memberQuery.Resolve(member, ref member.Comp, false))
            return null;

        if (member.Comp.Hive is not { } uid || TerminatingOrDeleted(uid))
            return null;

        if (!_query.TryComp(uid, out var comp))
            return null;

        return (uid, comp);
    }

    public Entity<HiveComponent>? GetHiveByName(string hiveName)
    {
        var query = EntityQueryEnumerator<HiveComponent>();

        while (query.MoveNext(out var uid, out var hive))
        {
            if (MetaData(uid).EntityName == hiveName)
                return (uid, hive);
        }

        return null;
    }

    public bool HasFaction(EntityUid hiveEnt, ProtoId<NpcFactionPrototype> faction)
    {
        TryComp<HiveComponent>(hiveEnt, out var hive);
        if (hive is null)
            return false;
        return hive.Allies.Contains(faction);
    }

    public void SetHiveFactionAlly(ProtoId<NpcFactionPrototype> faction, EntityUid hiveEnt, bool alliance)
    {
        if (!TryComp<HiveComponent>(hiveEnt, out var hive))
            return;
        if (alliance)
        {
            hive.Allies.Add(faction);
        }
        else
        {
            hive.Allies.Remove(faction);
        }
    }
    public void SetHiveIndividualAlly(EntityUid ent, EntityUid hiveEnt, bool alliance)
    {
        if (!TryComp<HiveComponent>(hiveEnt, out var hive))
            return;
        if (alliance)
        {
            hive.IndividualAllies.Add(ent);
        }
        else
        {
            hive.IndividualAllies.Remove(ent);
        }
    }
    public void ClearHiveIndividualAllies(EntityUid hiveEnt)
    {
        if (!TryComp<HiveComponent>(hiveEnt, out var hive))
            return;
        hive.IndividualAllies.Clear();
    }


    /// <summary>
    /// Returns true if the entity uid is at all in any way shape or form considered an "ally" of the hive.
    /// Be it part of the hive, some rando who became besties 4 lyfe with the queen, or the entire CLF faction after
    /// making a diplomatic alliance.
    /// </summary>
    /// <param name="ent"></param>
    /// <returns></returns>
    public bool IsAllyOfHive(EntityUid ent, EntityUid? hiveEnt)
    {
        if (hiveEnt is null)
            return false;
        // if there's no hive comp then just return false
        if (!TryComp<HiveComponent>(hiveEnt, out var hive))
            return false;

        if (TryComp<HiveMemberComponent>(ent, out var hc) && hc.Hive is not null && hc.Hive == hiveEnt)
            return true;

        if (hive.IndividualAllies.Contains(ent))
            return true;

        if (TryComp<NpcFactionMemberComponent>(ent, out var factionComp))
        {
            bool hit = false;
            // O(n) at best i think
            foreach (var fac in factionComp.Factions)
            {
                if (hive.Allies.Contains(fac))
                {
                    hit = true;
                    break;
                }
            }
            return hit;
        }

        return false;
    }

    /// <summary>
    /// Returns true if the entity has a valid hive, i.e. it isn't a rogue xeno.
    /// Only use this if you don't need to use the hive for anything after.
    /// </summary>
    public bool HasHive(Entity<HiveMemberComponent?> member)
    {
        return GetHive(member) != null;
    }

    /// <summary>
    /// Sets the hive for a member, if it's different.
    /// If it does not have HiveMemberComponent this method adds it.
    /// If the new HiveMember is a queen, it is now the new hiveQueen
    /// </summary>
    public void SetHive(Entity<HiveMemberComponent?> member, EntityUid? hive)
    {
        var comp = member.Comp ?? EnsureComp<HiveMemberComponent>(member);

        var old = comp.Hive;
        if (old == hive)
            return;

        Entity<HiveComponent>? hiveEnt = null;
        if (_query.TryComp(hive, out var hiveComp))
            hiveEnt = (hive.Value, hiveComp);
        else if (hive != null)
        {
            Log.Error($"Tried to set hive of {ToPrettyString(member)} to bad hive entity {ToPrettyString(hive)}");
            return; // invalid hive was passed, prevent it breaking anything else
        }

        comp.Hive = hive;
        Dirty(member, comp);
        UpdateHiveAppearance(member.Owner, hiveEnt);

        if (HasComp<XenoEvolutionGranterComponent>(member) && hiveEnt.HasValue)
            SetHiveQueen(member, hiveEnt.Value);

        var ev = new HiveChangedEvent(hiveEnt, old);
        RaiseLocalEvent(member, ref ev);
    }

    /// <summary>
    /// Sets the hive of the destination entity to that of the source entity, if it has one.
    /// If the source has no hive this is a no-op.
    /// If dest does not have HiveMemberComponent this method adds it like with <see cref="SetHive"/>.
    /// </summary>
    public void SetSameHive(Entity<HiveMemberComponent?> src, Entity<HiveMemberComponent?> dest)
    {
        if (GetHive(src) is { } hive)
            SetHive(dest, hive);
    }

    /// <summary>
    /// Returns true if the entities have the same hive and it isn't null.
    /// Rogue xenos don't have the same hive as they aren't in one!
    /// </summary>
    public bool FromSameHive(Entity<HiveMemberComponent?> a, Entity<HiveMemberComponent?> b)
    {
        if (GetHive(a) is not { } aHive)
            return false;

        return IsMember(b, aHive);
    }

    /// <summary>
    /// Returns true if the entity is a member of a specific hive.
    /// If the hive is null this always returns false, you cannot use this to check for rogue xenos.
    /// </summary>
    public bool IsMember(Entity<HiveMemberComponent?> member, EntityUid? hive)
    {
        if (hive == null || GetHive(member) is not { } memberHive)
            return false;

        return memberHive.Owner == hive;
    }

    public bool HasHiveQueen(Entity<HiveComponent> hive)
    {
        return (hive.Comp.CurrentQueen is not null);
    }

    public bool SetHiveQueen(EntityUid queen, Entity<HiveComponent> hive)
    {
        hive.Comp.CurrentQueen = queen;
        Dirty(hive);
        return true;
    }

    public bool HasHiveCore(Entity<HiveComponent> hive)
    {
        return GetHiveCore(hive) is not null;
    }

    public EntityUid? GetHiveCore(Entity<HiveComponent> hive)
    {
        var hiveCoreQuerry = EntityQueryEnumerator<HiveCoreComponent>();
        while (hiveCoreQuerry.MoveNext(out var hiveCoreEnt, out _))
        {
            if (GetHive(hiveCoreEnt) == hive)
            {
                return hiveCoreEnt;
            }
        }

        return null;
    }

    public void ResetHiveCoreCooldown(Entity<HiveComponent> hive)
    {
        hive.Comp.NewCoreAt = _timing.CurTime;
        Dirty(hive);
    }

    public bool TryGetStructureLimit(Entity<HiveComponent> hive, EntProtoId structureProtoId, out int limit)
    {
        return hive.Comp.HiveStructureSlots.TryGetValue(structureProtoId, out limit);
    }

    public void SetSeeThroughContainers(Entity<HiveComponent?> hive, bool see)
    {
        if (!_query.Resolve(hive, ref hive.Comp, false))
            return;

        hive.Comp.SeeThroughContainers = see;
        var xenos = EntityQueryEnumerator<XenoComponent, HiveMemberComponent, NightVisionComponent>();
        while (xenos.MoveNext(out var uid, out _, out var member, out var nv))
        {
            if (member.Hive != hive)
                continue;

            _nightVision.SetSeeThroughContainers((uid, nv), see);
        }
    }

    public void AnnounceNeedsOvipositorToSameHive(Entity<HiveMemberComponent?> xeno)
    {
        if (GetHive(xeno) is not { } hive || hive.Comp.GotOvipositorPopup)
            return;

        hive.Comp.GotOvipositorPopup = true;
        Dirty(hive);

        // TODO: loc
        var msg = "Enough time has passed, we require the Queen in oviposition for evolution.";
        var xenos = EntityQueryEnumerator<XenoComponent, HiveMemberComponent, ActorComponent>();
        while (xenos.MoveNext(out var uid, out _, out var member, out _))
        {
            if (uid == xeno.Owner || member.Hive != hive)
                continue;

            _popup.PopupEntity(msg, uid, uid, PopupType.LargeCaution);
        }

        _xenoAnnounce.AnnounceToHive(default, hive, msg);
    }

    public bool TryGetTierLimit(Entity<HiveComponent?> hive, int tier, out FixedPoint2 value)
    {
        value = default;
        if (!_query.Resolve(hive, ref hive.Comp, false))
            return false;

        return hive.Comp.TierLimits.TryGetValue(tier, out value);
    }

    public bool TryGetFreeSlots(Entity<HiveComponent?> hive, EntProtoId caste, out int value)
    {
        value = default;
        if (!_query.Resolve(hive, ref hive.Comp, false))
            return false;

        return hive.Comp.FreeSlots.TryGetValue(caste, out value);
    }

    public void IncreaseBurrowedLarva(int amount)
    {
        var hives = EntityQueryEnumerator<HiveComponent>();
        while (hives.MoveNext(out var uid, out var hive))
        {
            IncreaseBurrowedLarva((uid, hive), amount);
        }
    }

    public void IncreaseBurrowedLarva(Entity<HiveComponent> hive, int amount)
    {
        SetHiveBurrowedLarva(hive, hive.Comp.BurrowedLarva + amount);
    }

    private void SetHiveBurrowedLarva(Entity<HiveComponent> hive, int larva)
    {
        hive.Comp.BurrowedLarva = larva;
        Dirty(hive);

        var ev = new BurrowedLarvaChangedEvent(larva);
        RaiseLocalEvent(hive, ref ev, true);
    }

    public bool JoinBurrowedLarva(Entity<HiveComponent> hive, ICommonSession session)
    {
        if (_net.IsClient)
            return false;

        if (hive.Comp.BurrowedLarva <= 0)
            return false;

        EntityUid? larva = null;

        bool TrySpawnAt<T>() where T : Component
        {
            var candidates = EntityQueryEnumerator<T, HiveMemberComponent>();
            while (candidates.MoveNext(out var uid, out _, out var member))
            {
                if (member.Hive != hive)
                    continue;

                if (_mobState.IsDead(uid))
                    continue;

                var position = _transform.GetMoverCoordinates(uid);
                larva = Spawn(hive.Comp.BurrowedLarvaId, position);
                _transform.AttachToGridOrMap(larva.Value);
                return true;
            }

            return false;
        }

        if (!TrySpawnAt<HiveCoreComponent>() &&
            !TrySpawnAt<XenoEvolutionGranterComponent>() &&
            !TrySpawnAt<XenoComponent>())
        {
            return false;
        }

        if (larva == null)
            return false;

        IncreaseBurrowedLarva(hive, -1);

        _xeno.MakeXeno(larva.Value);
        SetHive(larva.Value, hive);

        var newMind = _mind.CreateMind(session.UserId,
            Comp<MetaDataComponent>(larva.Value).EntityName);
        _mind.TransferTo(newMind, larva, ghostCheckOverride: true);
        _adminLog.Add(LogType.RMCBurrowedLarva,
            $"{session.Name:player} took a burrowed larva from hive {ToPrettyString(hive):hive}.");

        return true;
    }

    private void OnAutoAssignHiveAdded(Entity<AutoAssignHiveComponent> ent, ref ComponentStartup args)
    {
        var hive = GetHiveByName(ent.Comp.Hive);

        if (hive == null)
        {
            Log.Debug($"Tried to auto assign hive to {ent.Comp.Hive}, but no such hive was found");
            return;
        }

        SetHive(ent.Owner, hive);
    }

    private void OnHiveGunShot(Entity<HiveGunComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var bullet in args.FiredProjectiles)
        {
            SetSameHive(ent.Owner, bullet);
        }
    }

    public bool FromSameHiveOrAlly(Entity<HiveMemberComponent?> a, Entity<HiveMemberComponent?> b)
    {
        // TODO RMC14
        return FromSameHive(a, b);
    }

    private void UpdateHiveAppearance(EntityUid member, Entity<HiveComponent>? hive)
    {
        _appearance.SetData(member, XenoHiveVisuals.Color, hive?.Comp.HiveColor ?? Color.White);
    }
}

/// <summary>
/// Raised on an entity after its hive is changed.
/// </summary>
[ByRefEvent]
public record struct HiveChangedEvent(Entity<HiveComponent>? Hive, EntityUid? OldHive);
