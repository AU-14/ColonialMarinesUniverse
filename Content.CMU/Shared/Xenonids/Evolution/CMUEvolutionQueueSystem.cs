using System.Linq;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.HiveLeader;
using Content.Shared._RMC14.Xenonids.ManageHive;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Prototypes;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Xenonids.Evolution;

/// <summary>
///     Per-hive evolution tier queue. Xenos without enough evolution points can click a
///     caste in the evolutions UI to wait in line for its tier; when the hive's tier cap
///     makes slots scarce, open slots go to waiting xenos in queue order. The Queen or
///     Overmind can reorder and remove entries through hive management, and hive leaders
///     automatically queue ahead of non-leaders.
/// </summary>
public sealed class CMUEvolutionQueueSystem : EntitySystem
{
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedXenoHiveSystem _xenoHive = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ManageHiveComponent, CMUManageHiveEvolutionQueueEvent>(OnManageHiveQueue);
        SubscribeLocalEvent<ManageHiveComponent, CMUEvolutionQueueTierEvent>(OnQueueTier);
        SubscribeLocalEvent<ManageHiveComponent, CMUEvolutionQueueMemberEvent>(OnQueueMember);
        SubscribeLocalEvent<ManageHiveComponent, CMUEvolutionQueueMoveEvent>(OnQueueMove);

        SubscribeLocalEvent<HiveLeaderComponent, ComponentStartup>(OnHiveLeaderStartup);
    }

    /// <summary>
    ///     Joins the tier queue for the chosen caste, updates the wanted caste if already
    ///     queued in that tier, or leaves the queue when the same caste is clicked again.
    /// </summary>
    public void ToggleQueue(Entity<XenoEvolutionComponent> xeno, EntProtoId choice)
    {
        if (_net.IsClient)
            return;

        if (!xeno.Comp.EvolvesTo.Contains(choice) && !xeno.Comp.EarlyEvolvesTo.Contains(choice))
        {
            Log.Warning($"{ToPrettyString(xeno)} sent an invalid evolution queue choice: {choice}.");
            return;
        }

        // with full points the normal evolve flow applies instead
        if (xeno.Comp.Max > FixedPoint2.Zero && xeno.Comp.Points >= xeno.Comp.Max)
            return;

        if (!_prototypes.TryIndex(choice, out var prototype) ||
            !prototype.TryComp(out XenoComponent? targetXeno, _compFactory))
        {
            return;
        }

        if (_xenoHive.GetHive(xeno.Owner) is not { } hive)
            return;

        var comp = EnsureComp<CMUEvolutionQueueComponent>(hive);
        PruneQueues((hive.Owner, comp));

        var tier = targetXeno.Tier;
        var queue = comp.Queues.GetOrNew(tier);
        var index = queue.FindIndex(entry => entry.Xeno == xeno.Owner);
        if (index >= 0)
        {
            if (queue[index].Choice == choice)
            {
                queue.RemoveAt(index);
                _popup.PopupEntity(Loc.GetString("cmu-xeno-evolution-queue-left", ("tier", tier)), xeno, xeno);
                return;
            }

            queue[index] = queue[index] with { Choice = choice };
            _popup.PopupEntity(
                Loc.GetString("cmu-xeno-evolution-queue-joined", ("tier", tier), ("position", index + 1)),
                xeno,
                xeno);
            return;
        }

        // a xeno waits in at most one tier queue at a time
        foreach (var other in comp.Queues.Values)
        {
            other.RemoveAll(entry => entry.Xeno == xeno.Owner);
        }

        var inserted = CMUEvolutionQueueOps.Enqueue(
            queue,
            new CMUEvolutionQueueEntry(xeno.Owner, choice),
            uid => HasComp<HiveLeaderComponent>(uid));

        _popup.PopupEntity(
            Loc.GetString("cmu-xeno-evolution-queue-joined", ("tier", tier), ("position", inserted + 1)),
            xeno,
            xeno);
    }

    /// <summary>
    ///     Whether the xeno must wait because the queued xenos ahead of it that are ready
    ///     to evolve would use up the tier's remaining slots.
    /// </summary>
    public bool IsBlockedByQueue(EntityUid xeno, Entity<HiveComponent> hive, int tier, int freeSlots, out int ahead)
    {
        ahead = 0;
        if (_net.IsClient)
            return false;

        if (!TryComp(hive, out CMUEvolutionQueueComponent? comp) ||
            !comp.Queues.TryGetValue(tier, out var queue) ||
            queue.Count == 0)
        {
            return false;
        }

        PruneQueue(hive, queue);

        foreach (var entry in queue)
        {
            if (entry.Xeno == xeno)
                break;

            if (IsReady(entry.Xeno))
                ahead++;
        }

        return ahead > 0 && ahead >= freeSlots;
    }

    private bool IsReady(EntityUid xeno)
    {
        return TryComp(xeno, out XenoEvolutionComponent? evolution) &&
               evolution.Max > FixedPoint2.Zero &&
               evolution.Points >= evolution.Max;
    }

    private void PruneQueues(Entity<CMUEvolutionQueueComponent> hive)
    {
        foreach (var queue in hive.Comp.Queues.Values)
        {
            PruneQueue(hive.Owner, queue);
        }
    }

    private void PruneQueue(EntityUid hive, List<CMUEvolutionQueueEntry> queue)
    {
        queue.RemoveAll(entry =>
            TerminatingOrDeleted(entry.Xeno) ||
            _mobState.IsDead(entry.Xeno) ||
            !_xenoHive.IsMember(entry.Xeno, hive) ||
            !HasComp<XenoEvolutionComponent>(entry.Xeno));
    }

    private void OnHiveLeaderStartup(Entity<HiveLeaderComponent> leader, ref ComponentStartup args)
    {
        if (_net.IsClient)
            return;

        if (_xenoHive.GetHive(leader.Owner) is not { } hive ||
            !TryComp(hive, out CMUEvolutionQueueComponent? comp))
        {
            return;
        }

        // new leaders jump ahead of waiting non-leaders
        foreach (var queue in comp.Queues.Values)
        {
            var index = queue.FindIndex(entry => entry.Xeno == leader.Owner);
            if (index < 0)
                continue;

            var entry = queue[index];
            queue.RemoveAt(index);
            CMUEvolutionQueueOps.Enqueue(queue, entry, uid => HasComp<HiveLeaderComponent>(uid));
        }
    }

    private void OnManageHiveQueue(Entity<ManageHiveComponent> manage, ref CMUManageHiveEvolutionQueueEvent args)
    {
        if (_net.IsClient)
            return;

        if (_xenoHive.GetHive(manage.Owner) is not { } hive)
            return;

        var options = new List<DialogOption>();
        if (TryComp(hive, out CMUEvolutionQueueComponent? comp))
        {
            PruneQueues((hive.Owner, comp));

            foreach (var (tier, queue) in comp.Queues.OrderBy(kvp => kvp.Key))
            {
                if (queue.Count == 0)
                    continue;

                options.Add(new DialogOption(
                    Loc.GetString("cmu-xeno-evolution-queue-tier-option", ("tier", tier), ("count", queue.Count)),
                    new CMUEvolutionQueueTierEvent(tier)));
            }
        }

        if (options.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-xeno-evolution-queue-empty"), manage, manage);
            return;
        }

        _dialog.OpenOptions(manage,
            Loc.GetString("cmu-xeno-evolution-queue-title"),
            options,
            Loc.GetString("cmu-xeno-evolution-queue-message"));
    }

    private void OnQueueTier(Entity<ManageHiveComponent> manage, ref CMUEvolutionQueueTierEvent args)
    {
        if (_net.IsClient)
            return;

        OpenTierDialog(manage, args.Tier);
    }

    private void OnQueueMember(Entity<ManageHiveComponent> manage, ref CMUEvolutionQueueMemberEvent args)
    {
        if (_net.IsClient)
            return;

        if (GetEntity(args.Xeno) is not { Valid: true } xeno || TerminatingOrDeleted(xeno))
            return;

        var name = Name(xeno);
        var options = new List<DialogOption>
        {
            new(Loc.GetString("cmu-xeno-evolution-queue-move-front"),
                new CMUEvolutionQueueMoveEvent(args.Xeno, args.Tier, CMUEvolutionQueueMove.Front)),
            new(Loc.GetString("cmu-xeno-evolution-queue-move-up"),
                new CMUEvolutionQueueMoveEvent(args.Xeno, args.Tier, CMUEvolutionQueueMove.Up)),
            new(Loc.GetString("cmu-xeno-evolution-queue-move-down"),
                new CMUEvolutionQueueMoveEvent(args.Xeno, args.Tier, CMUEvolutionQueueMove.Down)),
            new(Loc.GetString("cmu-xeno-evolution-queue-remove"),
                new CMUEvolutionQueueMoveEvent(args.Xeno, args.Tier, CMUEvolutionQueueMove.Remove)),
        };

        _dialog.OpenOptions(manage,
            name,
            options,
            Loc.GetString("cmu-xeno-evolution-queue-member-message", ("name", name)));
    }

    private void OnQueueMove(Entity<ManageHiveComponent> manage, ref CMUEvolutionQueueMoveEvent args)
    {
        if (_net.IsClient)
            return;

        if (_xenoHive.GetHive(manage.Owner) is not { } hive ||
            !TryComp(hive, out CMUEvolutionQueueComponent? comp) ||
            !comp.Queues.TryGetValue(args.Tier, out var queue))
        {
            return;
        }

        PruneQueue(hive, queue);

        var xeno = GetEntity(args.Xeno);
        if (CMUEvolutionQueueOps.Move(queue, xeno, args.Move) && args.Move == CMUEvolutionQueueMove.Remove)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-xeno-evolution-queue-removed", ("tier", args.Tier)),
                xeno,
                xeno,
                PopupType.MediumCaution);
        }

        OpenTierDialog(manage, args.Tier);
    }

    private void OpenTierDialog(Entity<ManageHiveComponent> manage, int tier)
    {
        if (_xenoHive.GetHive(manage.Owner) is not { } hive ||
            !TryComp(hive, out CMUEvolutionQueueComponent? comp) ||
            !comp.Queues.TryGetValue(tier, out var queue))
        {
            return;
        }

        PruneQueue(hive, queue);
        if (queue.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-xeno-evolution-queue-tier-empty", ("tier", tier)), manage, manage);
            return;
        }

        var options = new List<DialogOption>();
        for (var i = 0; i < queue.Count; i++)
        {
            var entry = queue[i];
            var caste = _prototypes.TryIndex(entry.Choice, out var choice) ? choice.Name : entry.Choice.Id;
            var text = Loc.GetString("cmu-xeno-evolution-queue-member-option",
                ("position", i + 1),
                ("name", Name(entry.Xeno)),
                ("caste", caste),
                ("leader", HasComp<HiveLeaderComponent>(entry.Xeno)));

            options.Add(new DialogOption(text, new CMUEvolutionQueueMemberEvent(GetNetEntity(entry.Xeno), tier)));
        }

        _dialog.OpenOptions(manage,
            Loc.GetString("cmu-xeno-evolution-queue-tier-title", ("tier", tier)),
            options,
            Loc.GetString("cmu-xeno-evolution-queue-tier-message"));
    }
}
