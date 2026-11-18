using Content.Server.Administration.Logs;
using Content.Shared._RMC14.Map;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 ECS-701: the Engineer caste's signature active and trade (<see cref="BadBloodEngineerComponent"/>).
/// <list type="bullet">
/// <item>Raise Bulwark: a line of BulwarkWidth clan barricades on the tiles in front of the Engineer, each paid with
/// BricksPerBarricade bricks from a stack in hand. Blocked or missing tiles are skipped; a bulwark that cannot place
/// anything costs nothing and starts no cooldown.</item>
/// <item>Reconsecration takes ConsecrationTimeMultiplier of its time (<see cref="BadBloodConsecrationTimeEvent"/>).</item>
/// <item>Counts ContestWeight toward seizing a Hunt Ground (<see cref="BadBloodContestWeightEvent"/>).</item>
/// <item>A Repair verb on a damaged Totem or Lodge: RepairTime do-after that heals RepairAmount.</item>
/// </list>
/// </summary>
public sealed class BadBloodEngineerSystem : EntitySystem
{
    [Dependency] private Content.Shared.Actions.SharedActionsSystem _actions = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private SharedStackSystem _stack = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodEngineerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BadBloodEngineerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BadBloodEngineerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BadBloodEngineerComponent, BadBloodRaiseBulwarkActionEvent>(OnRaiseBulwark);
        SubscribeLocalEvent<BadBloodEngineerComponent, BadBloodConsecrationTimeEvent>(OnConsecrationTime);
        SubscribeLocalEvent<BadBloodEngineerComponent, BadBloodContestWeightEvent>(OnContestWeight);
        SubscribeLocalEvent<BadBloodEngineerComponent, BadBloodRepairDoAfterEvent>(OnRepairDoAfter);
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnMapInit(Entity<BadBloodEngineerComponent> ent, ref MapInitEvent args)
    {
        GrantAction(ent);
    }

    // a caste taken up mid-round adds the component after map init
    private void OnStartup(Entity<BadBloodEngineerComponent> ent, ref ComponentStartup args)
    {
        if (MetaData(ent).EntityLifeStage >= EntityLifeStage.MapInitialized)
            GrantAction(ent);
    }

    private void GrantAction(Entity<BadBloodEngineerComponent> ent)
    {
        if (ent.Comp.BulwarkActionEntity != null)
            return;

        _actions.AddAction(ent.Owner, ref ent.Comp.BulwarkActionEntity, ent.Comp.BulwarkAction);
        Dirty(ent);
    }

    private void OnShutdown(Entity<BadBloodEngineerComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _actions.RemoveAction(ent.Owner, ent.Comp.BulwarkActionEntity);
        ent.Comp.BulwarkActionEntity = null;
    }

    private void OnConsecrationTime(Entity<BadBloodEngineerComponent> ent, ref BadBloodConsecrationTimeEvent args)
    {
        if (!args.Desecrate)
            args.Multiplier *= ent.Comp.ConsecrationTimeMultiplier;
    }

    private void OnContestWeight(Entity<BadBloodEngineerComponent> ent, ref BadBloodContestWeightEvent args)
    {
        args.Weight = Math.Max(args.Weight, ent.Comp.ContestWeight);
    }

    #region Raise Bulwark

    private void OnRaiseBulwark(Entity<BadBloodEngineerComponent> ent, ref BadBloodRaiseBulwarkActionEvent args)
    {
        if (args.Handled)
            return;

        // a bulwark that places nothing starts no cooldown
        args.Handled = TryRaiseBulwark(ent) > 0;
    }

    /// <summary>
    /// Raises the bulwark in front of <paramref name="engineer"/> and returns how many barricades went up. Tells the
    /// Engineer why when none did.
    /// </summary>
    public int TryRaiseBulwark(Entity<BadBloodEngineerComponent> engineer)
    {
        var user = engineer.Owner;
        if (!_mobState.IsAlive(user))
            return 0;

        if (FindBricks(user) is not { } bricks)
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-bulwark-no-bricks", ("cost", engineer.Comp.BricksPerBarricade)),
                user, user, PopupType.SmallCaution);
            return 0;
        }

        var xform = Transform(user);
        if (xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-bulwark-no-room"), user, user, PopupType.SmallCaution);
            return 0;
        }

        var cost = Math.Max(1, engineer.Comp.BricksPerBarricade);
        var affordable = _stack.GetCount(bricks) / cost;
        var dir = xform.LocalRotation.GetCardinalDir();
        var forward = dir.ToIntVec();
        var side = new Vector2i(-forward.Y, forward.X);
        var origin = _map.TileIndicesFor(gridUid, grid, xform.Coordinates) + forward;
        var half = Math.Max(engineer.Comp.BulwarkWidth, 1) / 2;

        var placed = 0;
        for (var i = -half; i <= half && placed < affordable; i++)
        {
            var tile = origin + side * i;
            if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            var coords = _map.GridTileToLocal(gridUid, grid, tile);
            if (_rmcMap.IsTileBlocked(coords))
                continue;

            var barricade = SpawnAtPosition(engineer.Comp.BarricadePrototype, coords);
            Transform(barricade).LocalRotation = dir.ToAngle();
            placed++;
        }

        if (placed == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-bulwark-no-room"), user, user, PopupType.SmallCaution);
            return 0;
        }

        _stack.ReduceCount(bricks, placed * cost);
        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-bulwark-raised", ("count", placed)), user, user, PopupType.Medium);
        _adminLog.Add(LogType.Construction, LogImpact.Low, $"{ToPrettyString(user):player} raised a Bad Blood bulwark of {placed} barricades");
        return placed;
    }

    private Entity<StackComponent?>? FindBricks(EntityUid user)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (HasComp<BadBloodBrickComponent>(held) && TryComp(held, out StackComponent? stack))
                return new Entity<StackComponent?>(held, stack);
        }

        return null;
    }

    #endregion

    #region Repair

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess ||
            !args.CanInteract ||
            !TryComp(args.User, out BadBloodEngineerComponent? engineer) ||
            !IsRepairable(args.Target))
        {
            return;
        }

        var user = args.User;
        var target = args.Target;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-bad-blood-engineer-repair-verb"),
            Message = Loc.GetString("cmu-bad-blood-engineer-repair-message", ("seconds", (int) engineer.RepairTime.TotalSeconds)),
            Act = () => TryStartRepair((user, engineer), target),
        });
    }

    // a standing, damaged Totem or Lodge of the clan's
    private bool IsRepairable(EntityUid target)
    {
        if (TryComp(target, out BadBloodTotemComponent? totem))
        {
            if (!totem.Registered || totem.Destroyed)
                return false;
        }
        else if (TryComp(target, out BadBloodCampComponent? lodge))
        {
            if (lodge.Destroyed)
                return false;
        }
        else
        {
            return false;
        }

        return _damageable.GetTotalDamage(target) > FixedPoint2.Zero;
    }

    public bool TryStartRepair(Entity<BadBloodEngineerComponent> engineer, EntityUid target)
    {
        if (!IsRepairable(target))
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-engineer-repair-nothing"), engineer, engineer, PopupType.SmallCaution);
            return false;
        }

        var doAfter = new DoAfterArgs(EntityManager, engineer, engineer.Comp.RepairTime, new BadBloodRepairDoAfterEvent(), engineer, target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };

        return _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnRepairDoAfter(Entity<BadBloodEngineerComponent> ent, ref BadBloodRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        TryCompleteRepair(ent, target);
    }

    /// <summary>
    /// Heals RepairAmount from <paramref name="target"/> at once, checking it is still a damaged Totem or Lodge.
    /// </summary>
    public bool TryCompleteRepair(Entity<BadBloodEngineerComponent> engineer, EntityUid target)
    {
        if (!IsRepairable(target))
            return false;

        _damageable.HealEvenly(target, FixedPoint2.New(-engineer.Comp.RepairAmount));
        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-engineer-repaired"), target, engineer);
        return true;
    }

    #endregion
}
