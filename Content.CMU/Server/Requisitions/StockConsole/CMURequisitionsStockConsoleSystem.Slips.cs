using System.Linq;
using Content.Server.Popups;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.Requisitions.StockConsole;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Server.CMU14.Ops.ForceOnForce;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Roles;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Content.Shared.GameTicking;
using Content.Shared.Paper;
using Robust.Shared.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Requisitions.StockConsole;

/// <summary>
/// Order slips: pick up to a handful of items on the stock console, it prints a slip into your hand, the requisitions
/// technician stamps it approved and uses it on the requisitions vendor, which hands out what it has of the order.
/// </summary>
public sealed partial class CMURequisitionsStockConsoleSystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedCMAutomatedVendorSystem _vendors = default!;

    /// <summary>How far from the vendor a dispense table can be and still be used.</summary>
    private const float DispenseTableRange = 6f;

    private readonly HashSet<string> _paidRequesters = new();

    private void InitializeSlips()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _paidRequesters.Clear());
        SubscribeLocalEvent<CMURequisitionsStockConsoleComponent, CMURequisitionsStockConsolePrintSlipBuiMsg>(OnPrintSlip);
        SubscribeLocalEvent<CMURequisitionsVendorComponent, InteractUsingEvent>(OnVendorInteractUsing,
            before: [typeof(SharedCMAutomatedVendorSystem)]);
    }

    /// <summary>Squad leaders and command staff, including their OPFOR mirrors, can order more on one slip.</summary>
    private int GetMaxSlipItems(Entity<CMURequisitionsStockConsoleComponent> ent, EntityUid user)
    {
        if (HasComp<SquadLeaderComponent>(user))
            return ent.Comp.CommandMaxSlipItems;

        if (_mind.TryGetMind(user, out var mindId, out _) &&
            _jobs.MindTryGetJobId(mindId, out var job) &&
            job is { } jobId &&
            _prototypes.Index(ent.Comp.CommandJobGroup).TryGetComponent(out JobGroupComponent? group, _compFactory) &&
            (group.Jobs.Contains(jobId) || FofJobs.Mirror(jobId.Id) is { } mirror && group.Jobs.Contains(mirror)))
        {
            return ent.Comp.CommandMaxSlipItems;
        }

        return ent.Comp.MaxSlipItems;
    }

    private static bool IsOrderable(CMVendorEntry entry)
    {
        return entry.Points == null && entry.Amount is not <= 0;
    }

    private void OnPrintSlip(Entity<CMURequisitionsStockConsoleComponent> ent, ref CMURequisitionsStockConsolePrintSlipBuiMsg args)
    {
        var user = args.Actor;
        var now = _timing.CurTime;
        if (now < ent.Comp.NextPrint)
            return;

        // Only what the vendors on this ship can actually hand out right now, capped at the slip's size and at the
        // stock left. Null stock is unlimited.
        var orderable = new Dictionary<string, string>();
        var stock = new Dictionary<string, int?>();
        var map = _transform.GetMapId(ent.Owner);
        var vendors = EntityQueryEnumerator<CMURequisitionsVendorComponent, CMAutomatedVendorComponent>();
        while (vendors.MoveNext(out var uid, out _, out var vendor))
        {
            if (_transform.GetMapId(uid) != map)
                continue;

            foreach (var entry in vendor.Sections.SelectMany(s => s.Entries))
            {
                if (!IsOrderable(entry))
                    continue;

                orderable.TryAdd(entry.Id.Id, GetEntryName(entry));
                stock[entry.Id.Id] = stock.TryGetValue(entry.Id.Id, out var known) && (known == null || entry.Amount == null)
                    ? null
                    : (known ?? 0) + entry.Amount;
            }
        }

        var lines = new Dictionary<string, int>();
        var total = 0;
        var maxItems = GetMaxSlipItems(ent, user);
        foreach (var line in args.Lines)
        {
            if (line.Count <= 0 || !orderable.ContainsKey(line.Id))
                continue;

            var count = Math.Min(line.Count, maxItems - total);
            if (stock[line.Id] is { } left)
                count = Math.Min(count, left - lines.GetValueOrDefault(line.Id));
            if (count <= 0)
                continue;

            lines[line.Id] = lines.GetValueOrDefault(line.Id) + count;
            total += count;
        }

        if (total == 0)
            return;

        ent.Comp.NextPrint = now + ent.Comp.PrintCooldown;

        var slip = Spawn(ent.Comp.SlipPrototype, Transform(ent).Coordinates);
        var comp = EnsureComp<CMURequisitionSlipComponent>(slip);
        comp.Remaining = lines;
        comp.Requester = Name(user);
        comp.RequesterKey = TryComp(user, out ActorComponent? actor) ? actor.PlayerSession.UserId.ToString() : user.ToString();
        foreach (var (id, count) in lines)
        {
            comp.Ordered[orderable[id]] = comp.Ordered.GetValueOrDefault(orderable[id]) + count;
        }

        _paper.SetContent(slip, Loc.GetString("cmu-req-slip-content",
            ("requester", comp.Requester),
            ("items", FormatLines(comp.Ordered)),
            ("total", total)));

        _audio.PlayPvs(ent.Comp.PrintSound, ent);
        _hands.PickupOrDrop(user, slip);
        _popup.PopupEntity(Loc.GetString("cmu-req-slip-printed", ("console", ent.Owner)), ent, user);
    }

    /// <summary>
    ///     The vendor marks the filled slip complete. Already stamped approved, it now pays out like a crate invoice
    ///     when it's sent down the ASRS lift; before this it's just a request and pays nothing.
    /// </summary>
    private void CompleteSlip(Entity<CMURequisitionSlipComponent> slip, EntityUid vendor, EntityUid user)
    {
        // Each person's orders only pay out once a round, so printing a pile of one-item slips earns nothing.
        var pays = _paidRequesters.Add(slip.Comp.RequesterKey);
        if (pays)
            EntityManager.AddComponents(slip, _prototypes.Index(slip.Comp.PayoutTemplate), removeExisting: false);

        _paper.SetContent(slip, Loc.GetString("cmu-req-slip-completed-content",
            ("requester", slip.Comp.Requester),
            ("filler", Name(user)),
            ("items", FormatLines(slip.Comp.Ordered)),
            ("total", slip.Comp.Ordered.Values.Sum()),
            ("pays", pays ? "yes" : "no")));

        _audio.PlayPvs(slip.Comp.CompletedSound, vendor);
    }

    private EntityCoordinates? FindDispenseTable(EntityUid vendor, EntityUid user)
    {
        var vendorCoords = _transform.GetMapCoordinates(vendor);
        var userCoords = _transform.GetMapCoordinates(user);
        EntityCoordinates? best = null;
        var bestDistance = float.MaxValue;
        var tables = EntityQueryEnumerator<CMURequisitionsDispenseTableComponent, TransformComponent>();
        while (tables.MoveNext(out var uid, out _, out var xform))
        {
            var coords = _transform.GetMapCoordinates(uid, xform);
            if (coords.MapId != vendorCoords.MapId ||
                (coords.Position - vendorCoords.Position).Length() > DispenseTableRange)
            {
                continue;
            }

            var distance = (coords.Position - userCoords.Position).Length();
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = xform.Coordinates;
        }

        return best;
    }

    private string FormatLines(Dictionary<string, int> lines)
    {
        var list = new FormattedMessage();
        foreach (var (item, count) in lines)
        {
            list.AddText(Loc.GetString("cmu-req-slip-line", ("count", count), ("item", item)));
            list.PushNewline();
        }

        return list.ToMarkup();
    }

    private void OnVendorInteractUsing(Entity<CMURequisitionsVendorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !TryComp(args.Used, out CMURequisitionSlipComponent? slip) ||
            !TryComp(args.Used, out PaperComponent? paper) ||
            !TryComp(ent, out CMAutomatedVendorComponent? vendor))
        {
            return;
        }

        args.Handled = true;
        var user = args.User;
        if (slip.Remaining.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-req-slip-already-filled"), ent, user);
            return;
        }

        if (paper.StampState != slip.RequiredStamp)
        {
            _popup.PopupEntity(Loc.GetString("cmu-req-slip-not-approved"), ent, user);
            return;
        }

        if (!_access.IsAllowed(user, ent))
        {
            _popup.PopupEntity(Loc.GetString("cmu-req-slip-no-access"), ent, user);
            return;
        }

        // The order goes on the dispense table nearest whoever handed the slip in, or to them if there isn't one.
        var dropAt = FindDispenseTable(ent, user);
        var vended = 0;
        foreach (var id in slip.Remaining.Keys.ToList())
        {
            var owed = slip.Remaining[id];
            for (var s = 0; s < vendor.Sections.Count && owed > 0; s++)
            {
                var entries = vendor.Sections[s].Entries;
                for (var e = 0; e < entries.Count && owed > 0; e++)
                {
                    if (entries[e].Id.Id != id || !IsOrderable(entries[e]))
                        continue;

                    while (owed > 0 && _vendors.TryVendFromStock((ent, vendor), user, s, e, dropAt))
                    {
                        owed--;
                        vended++;
                    }
                }
            }

            if (owed > 0)
                slip.Remaining[id] = owed;
            else
                slip.Remaining.Remove(id);
        }

        var left = slip.Remaining.Values.Sum();
        if (vended == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-req-slip-none-in-stock"), ent, user);
            return;
        }

        _audio.PlayPvs(vendor.Sound, ent);

        if (left == 0)
        {
            CompleteSlip((args.Used, slip), ent, user);
            _popup.PopupEntity(Loc.GetString("cmu-req-slip-filled", ("count", vended), ("vendor", ent.Owner)), ent, user);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("cmu-req-slip-partly-filled", ("count", vended), ("left", left)), ent, user);
        }
    }
}
