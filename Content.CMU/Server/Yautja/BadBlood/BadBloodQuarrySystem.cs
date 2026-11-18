using Content.Server.Administration.Logs;
using Content.Shared.CMU14.Construction;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Construction;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-512 and FLOW-513: the Lodge quarry and Bad Blood fortifications. Bad Bloods draw bricks from the quarry and
/// build walls, barricades and doors with them within build range of the standing Lodge or of a registered,
/// undesecrated Totem. Offerings, Totem income and sabotage add stone. Everything built crumbles when the Lodge
/// falls, and what stood only by a Totem crumbles with that Totem.
/// </summary>
public sealed class BadBloodQuarrySystem : EntitySystem
{
    private static readonly TimeSpan TotemCacheEvery = TimeSpan.FromSeconds(1);

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private BadBloodCampSystem _camp = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // a Totem is detached from the map before its components shut down, so its last position is kept here
    private readonly Dictionary<EntityUid, MapCoordinates> _totemPositions = new();
    private readonly List<EntityUid> _crumbleScratch = new();
    private TimeSpan _nextTotemCache;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodQuarryComponent, MapInitEvent>(OnQuarryMapInit);
        SubscribeLocalEvent<BadBloodQuarryComponent, ActivateInWorldEvent>(OnQuarryActivate);
        SubscribeLocalEvent<BadBloodQuarryComponent, InteractUsingEvent>(OnQuarryInteractUsing);
        SubscribeLocalEvent<BadBloodQuarryComponent, ExaminedEvent>(OnQuarryExamined);

        SubscribeLocalEvent<BadBloodOfferingCreditedEvent>(OnOfferingCredited);
        SubscribeLocalEvent<BadBloodTotemPaidEvent>(OnTotemPaid);
        SubscribeLocalEvent<BadBloodSiteSabotagedEvent>(OnSiteSabotaged);

        SubscribeLocalEvent<CMUConstructionBuildAttemptEvent>(OnBuildAttempt);
        SubscribeLocalEvent<BadBloodFortificationComponent, ConstructionCompletedEvent>(OnFortificationBuilt);
        SubscribeLocalEvent<BadBloodLodgeDestroyedEvent>(OnLodgeDestroyed);

        SubscribeLocalEvent<BadBloodTotemComponent, ComponentStartup>(OnTotemStartup);
        SubscribeLocalEvent<BadBloodTotemComponent, ComponentShutdown>(OnTotemShutdown);
    }

    public override void Update(float frameTime)
    {
        // Totems do not move once raised, this only keeps the cache honest if an admin moves one
        var now = _timing.CurTime;
        if (now < _nextTotemCache)
            return;

        _nextTotemCache = now + TotemCacheEvery;
        var totems = EntityQueryEnumerator<BadBloodTotemComponent>();
        while (totems.MoveNext(out var uid, out _))
        {
            CacheTotem(uid);
        }
    }

    #region Stone

    /// <summary>
    /// The standing Lodge's quarry.
    /// </summary>
    public bool TryGetQuarry(out Entity<BadBloodQuarryComponent> quarry)
    {
        quarry = default;
        if (!_camp.TryGetPart(BadBloodCampPartKind.Quarry, out var part) ||
            !TryComp(part, out BadBloodQuarryComponent? comp))
        {
            return false;
        }

        quarry = (part, comp);
        return true;
    }

    /// <summary>
    /// Adds <paramref name="amount"/> stone to the standing Lodge's quarry, up to its cap. False without a quarry.
    /// </summary>
    public bool AddStone(int amount, string reason)
    {
        if (amount <= 0 || !TryGetQuarry(out var quarry))
            return false;

        var old = quarry.Comp.Stone;
        quarry.Comp.Stone = Math.Clamp(old + amount, 0, Math.Max(old, quarry.Comp.StoneCap));
        if (quarry.Comp.Stone != old)
        {
            _adminLog.Add(LogType.Action, LogImpact.Low,
                $"BadBlood obj: quarry {ToPrettyString(quarry.Owner):quarry} gained {quarry.Comp.Stone - old} stone from {reason}, now {quarry.Comp.Stone}");
        }

        return true;
    }

    private void OnQuarryMapInit(Entity<BadBloodQuarryComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Stone = Math.Clamp(ent.Comp.StartStone, 0, ent.Comp.StoneCap);
    }

    private void OnOfferingCredited(ref BadBloodOfferingCreditedEvent args)
    {
        if (TryGetQuarry(out var quarry))
            AddStone(quarry.Comp.OfferingStone, "a trophy offering");
    }

    private void OnTotemPaid(ref BadBloodTotemPaidEvent args)
    {
        if (args.WholePoints > 0 && TryGetQuarry(out var quarry))
            AddStone(quarry.Comp.TotemStonePerSp * args.WholePoints, "Totem income");
    }

    private void OnSiteSabotaged(ref BadBloodSiteSabotagedEvent args)
    {
        if (TryGetQuarry(out var quarry))
            AddStone(quarry.Comp.SabotageStone, "sabotage");
    }

    /// <summary>
    /// <paramref name="user"/> draws up to <see cref="BadBloodQuarryComponent.DrawAmount"/> bricks from
    /// <paramref name="quarry"/>, into their hands or at their feet. Returns the brick stack, if any was drawn.
    /// </summary>
    public EntityUid? TryDraw(Entity<BadBloodQuarryComponent> quarry, EntityUid user)
    {
        if (!_badBlood.IsBadBlood(user) || _mobState.IsDead(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-quarry-clan-only"), quarry, user, PopupType.SmallCaution);
            return null;
        }

        var amount = Math.Min(quarry.Comp.Stone, quarry.Comp.DrawAmount);
        if (amount <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-quarry-empty"), quarry, user, PopupType.SmallCaution);
            return null;
        }

        quarry.Comp.Stone -= amount;
        var bricks = Spawn(quarry.Comp.BrickPrototype, Transform(user).Coordinates);
        _stack.SetCount((bricks, null), amount);
        _hands.PickupOrDrop(user, bricks);

        _popup.PopupEntity(
            Loc.GetString("cmu-bad-blood-quarry-drawn", ("count", amount), ("stone", quarry.Comp.Stone)),
            quarry,
            user);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"BadBlood obj: {ToPrettyString(user):player} drew {amount} bricks from the quarry {ToPrettyString(quarry.Owner):quarry}, {quarry.Comp.Stone} left");
        return bricks;
    }

    private void OnQuarryActivate(Entity<BadBloodQuarryComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        TryDraw(ent, args.User);
    }

    // returned bricks go back into the stock, up to the cap. the rest stays in the user's hand
    private void OnQuarryInteractUsing(Entity<BadBloodQuarryComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !HasComp<BadBloodBrickComponent>(args.Used) ||
            !TryComp(args.Used, out StackComponent? stack))
        {
            return;
        }

        args.Handled = true;
        var room = ent.Comp.StoneCap - ent.Comp.Stone;
        var count = _stack.GetCount((args.Used, stack));
        var taken = Math.Min(count, room);
        if (taken <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-quarry-full"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        ent.Comp.Stone += taken;
        _stack.ReduceCount((args.Used, stack), taken);
        _popup.PopupEntity(
            Loc.GetString("cmu-bad-blood-quarry-returned", ("count", taken), ("stone", ent.Comp.Stone)),
            ent,
            args.User);
    }

    private void OnQuarryExamined(Entity<BadBloodQuarryComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("cmu-bad-blood-quarry-examine",
            ("stone", ent.Comp.Stone),
            ("cap", ent.Comp.StoneCap)));
    }

    #endregion

    #region Build range

    /// <summary>
    /// How far from the Lodge or a Totem bricks build: the standing quarry's radius, or the default without one.
    /// </summary>
    public float GetBuildRadius()
    {
        return TryGetQuarry(out var quarry) ? quarry.Comp.BuildRadius : BadBloodQuarryComponent.DefaultBuildRadius;
    }

    /// <summary>
    /// Whether <paramref name="coords"/> is within build range of the standing Lodge or of a registered,
    /// undesecrated Totem other than <paramref name="ignoreTotem"/>.
    /// </summary>
    public bool IsInBuildRange(MapCoordinates coords, EntityUid? ignoreTotem = null)
    {
        if (coords.MapId == MapId.Nullspace)
            return false;

        var radius = GetBuildRadius();
        if (_camp.TryGetLodge(out var lodge) &&
            !lodge.Comp.Destroyed &&
            !TerminatingOrDeleted(lodge.Owner) &&
            Within(_transform.GetMapCoordinates(lodge.Owner), coords, radius))
        {
            return true;
        }

        var totems = EntityQueryEnumerator<BadBloodTotemComponent, TransformComponent>();
        while (totems.MoveNext(out var uid, out var totem, out var xform))
        {
            if (uid == ignoreTotem ||
                !totem.Registered ||
                totem.Desecrated ||
                TerminatingOrDeleted(uid))
            {
                continue;
            }

            if (Within(_transform.GetMapCoordinates(uid, xform), coords, radius))
                return true;
        }

        return false;
    }

    public bool IsInBuildRange(EntityCoordinates coords, EntityUid? ignoreTotem = null)
    {
        return IsInBuildRange(_transform.ToMapCoordinates(coords), ignoreTotem);
    }

    // CMU14 ARCH-604: the Engineer caste's perk, as a marker component
    private bool IsEngineer(EntityUid user)
    {
        return HasComp<BadBloodBuildAnywhereComponent>(user);
    }

    private static bool Within(MapCoordinates a, MapCoordinates b, float radius)
    {
        return a.MapId == b.MapId && (a.Position - b.Position).Length() <= radius;
    }

    private void OnBuildAttempt(ref CMUConstructionBuildAttemptEvent args)
    {
        if (args.Cancelled || !HasComp<BadBloodBrickComponent>(args.Item))
            return;

        if (!_badBlood.IsBadBlood(args.User) || _mobState.IsDead(args.User))
        {
            args.Cancelled = true;
            args.Popup = Loc.GetString("cmu-bad-blood-brick-clan-only");
            return;
        }

        // ARCH-04: an Engineer builds anywhere
        if (!IsInBuildRange(args.Coordinates) && !IsEngineer(args.User))
        {
            args.Cancelled = true;
            args.Popup = Loc.GetString("cmu-bad-blood-brick-out-of-range", ("radius", (int) GetBuildRadius()));
        }
    }

    // the range can lapse during the build's do-after: what finishes out of range crumbles at once
    private void OnFortificationBuilt(Entity<BadBloodFortificationComponent> ent, ref ConstructionCompletedEvent args)
    {
        if (IsInBuildRange(_transform.GetMapCoordinates(ent.Owner)) || IsEngineer(args.Builder))
            return;

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"BadBlood obj: {ToPrettyString(ent.Owner):fortification} built by {ToPrettyString(args.Builder):player} crumbled, out of range of the Lodge and every Totem");
        QueueDel(ent.Owner);
    }

    #endregion

    #region Crumble

    private void OnLodgeDestroyed(ref BadBloodLodgeDestroyedEvent args)
    {
        _crumbleScratch.Clear();

        var fortifications = EntityQueryEnumerator<BadBloodFortificationComponent>();
        while (fortifications.MoveNext(out var uid, out _))
        {
            _crumbleScratch.Add(uid);
        }

        var bricks = EntityQueryEnumerator<BadBloodBrickComponent>();
        while (bricks.MoveNext(out var uid, out _))
        {
            _crumbleScratch.Add(uid);
        }

        foreach (var uid in _crumbleScratch)
        {
            if (!TerminatingOrDeleted(uid))
                QueueDel(uid);
        }

        if (_crumbleScratch.Count > 0)
        {
            _adminLog.Add(LogType.Action, LogImpact.Low,
                $"BadBlood obj: the Lodge {ToPrettyString(args.Lodge):lodge} fell and {_crumbleScratch.Count} fortifications and brick stacks crumbled");
        }

        _crumbleScratch.Clear();
    }

    private void OnTotemStartup(Entity<BadBloodTotemComponent> ent, ref ComponentStartup args)
    {
        CacheTotem(ent.Owner);
    }

    private void CacheTotem(EntityUid totem)
    {
        var coords = _transform.GetMapCoordinates(totem);
        if (coords.MapId != MapId.Nullspace)
            _totemPositions[totem] = coords;
    }

    // what stood within range of the fallen Totem crumbles, unless the Lodge or another Totem still holds it
    private void OnTotemShutdown(Entity<BadBloodTotemComponent> ent, ref ComponentShutdown args)
    {
        var coords = _transform.GetMapCoordinates(ent.Owner);
        if (coords.MapId == MapId.Nullspace && !_totemPositions.TryGetValue(ent.Owner, out coords))
            return;

        _totemPositions.Remove(ent.Owner);

        // nothing to crumble while the whole map goes
        if (!_map.TryGetMap(coords.MapId, out var map) || TerminatingOrDeleted(map.Value))
            return;

        var radius = GetBuildRadius();
        _crumbleScratch.Clear();

        var fortifications = EntityQueryEnumerator<BadBloodFortificationComponent, TransformComponent>();
        while (fortifications.MoveNext(out var uid, out _, out var xform))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            var at = _transform.GetMapCoordinates(uid, xform);
            if (!Within(coords, at, radius) || IsInBuildRange(at, ent.Owner))
                continue;

            _crumbleScratch.Add(uid);
        }

        foreach (var uid in _crumbleScratch)
        {
            QueueDel(uid);
        }

        if (_crumbleScratch.Count > 0)
        {
            _adminLog.Add(LogType.Action, LogImpact.Low,
                $"BadBlood obj: the Totem {ToPrettyString(ent.Owner):totem} fell and {_crumbleScratch.Count} fortifications near it crumbled");
        }

        _crumbleScratch.Clear();
    }

    #endregion
}
