using Content.Server.Administration.Logs;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;
using Content.Shared._RMC14.Areas;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Gibbing;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Consoles;

/// <summary>
/// Phase E2: the Early Warning Joe's console assimilation. The action runs a do-after on a
/// whitelisted console; completion marks it (<see cref="EWJoeAssimilatedConsoleComponent"/>),
/// closes and locks its UI, applies the per-console effect, announces it to the colony and raises
/// <see cref="EWJoeConsoleAssimilatedEvent"/> for later phases. Colonists repair it in three steps
/// (screwdriver, wirecutters, core shard); the shard drops once from the dead Joe.
/// </summary>
public sealed partial class EWJoeConsoleSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private EWJoeRuleSystem _rule = default!;
    [Dependency] private EWJoeMessageSystem _messages = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    private static readonly ProtoId<ToolQualityPrototype> Screwing = "Screwing";
    private static readonly ProtoId<ToolQualityPrototype> Cutting = "Cutting";

    /// <summary>Number of repair steps; the last one consumes the shard and clears the marker.</summary>
    public const int RepairSteps = 3;

    private TimeSpan _nextSpoofCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeComponent, EWJoeAssimilateActionEvent>(OnAssimilateAction);
        SubscribeLocalEvent<EWJoeComponent, EWJoeAssimilateDoAfterEvent>(OnAssimilateDoAfter);
        SubscribeLocalEvent<EWJoeComponent, MobStateChangedEvent>(OnJoeMobStateChanged);
        SubscribeLocalEvent<EWJoeComponent, BeingGibbedEvent>(OnJoeGibbed);
        SubscribeLocalEvent<EWJoeComponent, EntityTerminatingEvent>(OnJoeTerminating);
        // broadcast: the directed <EWJoeComponent, EWJoeRemovedEvent> slot belongs to the objective ladder
        SubscribeLocalEvent<EWJoeRemovedEvent>(OnJoeRemoved);

        SubscribeLocalEvent<EWJoeAssimilatedConsoleComponent, InteractUsingEvent>(OnConsoleInteractUsing);
        SubscribeLocalEvent<EWJoeAssimilatedConsoleComponent, EWJoeConsoleRepairDoAfterEvent>(OnRepairDoAfter);
        SubscribeLocalEvent<EWJoeAssimilatedConsoleComponent, ExaminedEvent>(OnConsoleExamined);
        SubscribeLocalEvent<EWJoeAssimilatedConsoleComponent, ComponentShutdown>(OnMarkerShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextSpoofCheck)
            return;

        _nextSpoofCheck = now + TimeSpan.FromSeconds(5);

        var query = EntityQueryEnumerator<EWJoeAssimilatedConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextSpoof == TimeSpan.Zero || now < comp.NextSpoof)
                continue;

            comp.NextSpoof = now + comp.SpoofInterval;
            SpoofAnnouncement((uid, comp));
        }
    }

    #region Assimilation

    private void OnAssimilateAction(Entity<EWJoeComponent> ent, ref EWJoeAssimilateActionEvent args)
    {
        if (args.Handled)
            return;

        var console = args.Target;
        if (!CanAssimilate(ent, console, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason, ("console", console)), console, ent, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.AssimilateDelay, new EWJoeAssimilateDoAfterEvent(), ent, console)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            RequireCanInteract = true,
            DistanceThreshold = ent.Comp.AssimilateRange,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-assimilate-start", ("console", console)), console, ent);
    }

    private void OnAssimilateDoAfter(Entity<EWJoeComponent> ent, ref EWJoeAssimilateDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Args.Target is not { } console)
            return;

        args.Handled = true;

        if (!CanAssimilate(ent, console, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason, ("console", console)), console, ent, PopupType.SmallCaution);
            return;
        }

        Assimilate(ent, console);
    }

    /// <summary>
    /// A console can be assimilated when it exists, passes the Joe's console whitelist, is not
    /// assimilated already and (playtest 4) is the console one of the Joe's current assimilate objectives
    /// names. The whitelist is the same one the objective uses (component names only, so embassy consoles
    /// that share the comms parent are never picked).
    /// </summary>
    public bool CanAssimilate(Entity<EWJoeComponent> ent, EntityUid console, out string reason)
    {
        reason = "cmu-ew-joe-assimilate-invalid";
        if (!Exists(console) || TerminatingOrDeleted(console))
            return false;

        if (!_whitelist.IsWhitelistPass(ent.Comp.ConsoleWhitelist, console))
            return false;

        if (HasComp<EWJoeAssimilatedConsoleComponent>(console))
        {
            reason = "cmu-ew-joe-assimilate-already";
            return false;
        }

        if (!IsObjectiveConsole(ent, console))
        {
            reason = "cmu-ew-joe-assimilate-not-called";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Playtest 4: true when one of the Joe's objectives is an assimilate objective pinned to
    /// <paramref name="console"/> (the ladder's current set, or one an admin gave) that the ladder has not
    /// credited yet. Any other console of the right type is refused, so assimilation (and the meld or
    /// respawn it brings) cannot be farmed.
    /// </summary>
    public bool IsObjectiveConsole(Entity<EWJoeComponent> ent, EntityUid console)
    {
        if (!_mind.TryGetMind(ent, out _, out var mind))
            return false;

        foreach (var objective in mind.Objectives)
        {
            if (TryComp(objective, out EWJoeAssimilateConditionComponent? condition) && condition.Target == console && !condition.Spent)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Marks, locks, corrupts and announces the console. Public so tests and later phases can drive
    /// it without the do-after.
    /// </summary>
    public void Assimilate(Entity<EWJoeComponent> ent, EntityUid console)
    {
        var marker = EnsureComp<EWJoeAssimilatedConsoleComponent>(console);
        marker.AssimilatedBy = ent;
        marker.RepairStep = 0;

        _ui.CloseUis(console);
        // the corruption is a child effect, so the console's own sprite is never touched
        marker.Effect = SpawnAttachedTo(marker.EffectPrototype, new EntityCoordinates(console, System.Numerics.Vector2.Zero));

        var effect = ApplyEffect((console, marker));

        ent.Comp.ConsolesAssimilated++;
        _rule.RecordProgress(ent);

        _messages.Notify(ent, Loc.GetString("cmu-ew-joe-assimilate-done", ("console", console)));
        Announce(console, "cmu-ew-joe-console-assimilated-announcement");

        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"Early Warning Joe {ToPrettyString(ent):joe} assimilated {ToPrettyString(console):console} ({effect})");

        var ev = new EWJoeConsoleAssimilatedEvent(ent, console);
        RaiseLocalEvent(ent, ref ev, broadcast: true);
    }

    #endregion

    #region Repair

    private void OnConsoleInteractUsing(Entity<EWJoeAssimilatedConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryStartRepairStep(ent, args.User, args.Used);
    }

    /// <summary>
    /// Starts the repair do-after for the current step if <paramref name="used"/> is the right tool
    /// or item. Returns false (and says why) otherwise.
    /// </summary>
    public bool TryStartRepairStep(Entity<EWJoeAssimilatedConsoleComponent> ent, EntityUid user, EntityUid used)
    {
        var step = ent.Comp.RepairStep;
        var ev = new EWJoeConsoleRepairDoAfterEvent(step);

        switch (step)
        {
            case 0 when _tool.HasQuality(used, Screwing):
                return _tool.UseTool(used, user, ent.Owner, (float) ent.Comp.ToolStepDelay.TotalSeconds, Screwing, ev);
            case 1 when _tool.HasQuality(used, Cutting):
                return _tool.UseTool(used, user, ent.Owner, (float) ent.Comp.ToolStepDelay.TotalSeconds, Cutting, ev);
            case 2 when HasComp<EWJoeCoreShardComponent>(used):
                return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.ShardStepDelay, ev, ent, ent, used)
                {
                    BreakOnMove = true,
                    BreakOnDamage = true,
                    NeedHand = true,
                    BreakOnHandChange = true,
                });
        }

        // only talk when the item looks like a repair attempt, so cash and ID cards stay quiet
        if (_tool.HasQuality(used, Screwing) || _tool.HasQuality(used, Cutting) || HasComp<EWJoeCoreShardComponent>(used))
            _popup.PopupEntity(Loc.GetString(NextStepLoc(step)), ent, user, PopupType.SmallCaution);

        return false;
    }

    private void OnRepairDoAfter(Entity<EWJoeAssimilatedConsoleComponent> ent, ref EWJoeConsoleRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        // the stage must still be the one the do-after started on
        if (args.Step != ent.Comp.RepairStep)
            return;

        if (args.Step == RepairSteps - 1)
        {
            if (args.Used is not { } shard || !HasComp<EWJoeCoreShardComponent>(shard))
                return;

            QueueDel(shard);
            Repair(ent, args.User);
            return;
        }

        ent.Comp.RepairStep++;
        _popup.PopupEntity(Loc.GetString(NextStepLoc(ent.Comp.RepairStep)), ent, args.User);
    }

    /// <summary>Clears the assimilation: marker off, visuals off, console back in the objective pool.</summary>
    public void Repair(Entity<EWJoeAssimilatedConsoleComponent> ent, EntityUid repairer)
    {
        var assimilatedBy = ent.Comp.AssimilatedBy;
        RemComp<EWJoeAssimilatedConsoleComponent>(ent);
        SpendOffLadderObjectives(assimilatedBy, ent);
        if (assimilatedBy is { } joe)
            _messages.Notify(joe, Loc.GetString("cmu-ew-joe-console-lost", ("console", ent.Owner)));

        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-console-repaired", ("console", ent.Owner)), ent, repairer);
        Announce(ent, "cmu-ew-joe-console-repaired-announcement");

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(repairer):user} repaired assimilated console {ToPrettyString(ent):console}");

        var ev = new EWJoeConsoleRepairedEvent(repairer, assimilatedBy);
        RaiseLocalEvent(ent, ref ev);
    }

    /// <summary>
    /// Playtest 4: the ladder spends an assimilate objective when it credits it. One an admin gave outside
    /// the ladder is never credited, so a repair spends it instead; otherwise it would reopen the console
    /// for another meld or respawn. Ladder objectives are left to the ladder, which may still need to
    /// withdraw one whose console was repaired before it was credited.
    /// </summary>
    private void SpendOffLadderObjectives(EntityUid? assimilatedBy, EntityUid console)
    {
        if (!TryComp(assimilatedBy, out EWJoeComponent? joe) || !_mind.TryGetMind(assimilatedBy.Value, out _, out var mind))
            return;

        foreach (var objective in mind.Objectives)
        {
            if (joe.CurrentObjectives.Contains(objective))
                continue;

            if (TryComp(objective, out EWJoeAssimilateConditionComponent? condition) && condition.Target == console)
                condition.Spent = true;
        }
    }

    private void OnConsoleExamined(Entity<EWJoeAssimilatedConsoleComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(EWJoeAssimilatedConsoleComponent)))
        {
            args.PushMarkup(Loc.GetString("cmu-ew-joe-console-examine"));
            if (args.IsInDetailsRange)
                args.PushMarkup(Loc.GetString(NextStepLoc(ent.Comp.RepairStep)));
        }
    }

    /// <summary>Repair, admin removal or console deletion: the effect goes with the marker.</summary>
    private void OnMarkerShutdown(Entity<EWJoeAssimilatedConsoleComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Effect is { } effect && !TerminatingOrDeleted(effect))
            QueueDel(effect);
        ent.Comp.Effect = null;
    }

    private static string NextStepLoc(int step)
    {
        return step switch
        {
            0 => "cmu-ew-joe-console-repair-step-screwdriver",
            1 => "cmu-ew-joe-console-repair-step-wirecutters",
            _ => "cmu-ew-joe-console-repair-step-shard",
        };
    }

    #endregion

    #region Shard

    private void OnJoeMobStateChanged(Entity<EWJoeComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            TryDropShard(ent, "died");
    }

    /// <summary>Gibbing skips the Dead transition and deletes the body; drop before the body goes.</summary>
    private void OnJoeGibbed(Entity<EWJoeComponent> ent, ref BeingGibbedEvent args)
    {
        TryDropShard(ent, "was gibbed");
    }

    /// <summary>
    /// Admin deletion or any other removal without death. Skipped when the parent is going too
    /// (round restart, map unload): there is nothing to repair afterwards.
    /// </summary>
    private void OnJoeTerminating(Entity<EWJoeComponent> ent, ref EntityTerminatingEvent args)
    {
        // the grid or map, not the immediate parent: a Joe deleted inside a locker still owes the shard
        var xform = Transform(ent);
        var ground = xform.GridUid ?? xform.MapUid;
        if (ground is not { } groundUid || TerminatingOrDeleted(groundUid))
            return;

        TryDropShard(ent, "was deleted");
    }

    /// <summary>
    /// Stand-down (D10) strips the antag from a live Joe. Its consoles stay corrupted, so the colony
    /// still needs the shard; drop it only if something is left to repair.
    /// </summary>
    private void OnJoeRemoved(ref EWJoeRemovedEvent args)
    {
        if (!TryComp(args.Joe, out EWJoeComponent? comp))
            return;

        var query = EntityQueryEnumerator<EWJoeAssimilatedConsoleComponent>();
        while (query.MoveNext(out _, out var marker))
        {
            if (marker.AssimilatedBy == args.Joe)
            {
                TryDropShard((args.Joe, comp), "stood down");
                return;
            }
        }
    }

    /// <summary>Spawns the core shard at the Joe's position once per Joe. False if it already dropped or has no position.</summary>
    public bool TryDropShard(Entity<EWJoeComponent> ent, string why)
    {
        if (ent.Comp.ShardDropped)
            return false;

        var xform = Transform(ent);
        if (xform.MapID == MapId.Nullspace)
            return false;

        ent.Comp.ShardDropped = true;
        // SpawnAtPosition resolves to map coordinates, so a Joe inside a container drops the shard on the floor
        var shard = SpawnAtPosition(ent.Comp.CoreShardPrototype, xform.Coordinates);

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"Early Warning Joe {ToPrettyString(ent):joe} {why} and dropped {ToPrettyString(shard):shard}");
        return true;
    }

    #endregion
}
