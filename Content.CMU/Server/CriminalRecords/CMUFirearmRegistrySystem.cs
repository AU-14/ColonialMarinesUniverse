using System.Linq;
using Content.Server.CMU14.Requisitions;
using Content.Server.GameTicking;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.CriminalRecords;
using Content.Shared.CMU14.Weapons.SerialNumber;
using Content.Shared.CriminalRecords;
using Content.Shared.CriminalRecords.Components;
using Content.Shared.Forensics;
using Content.Shared.Forensics.Components;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server.CMU14.CriminalRecords;

/// <summary>
///     Logs every firearm colony logistics buys through the ASRS, with its serial number, firing-pin signature and who
///     ordered it, and keeps the colony's criminal records consoles up to date with it. Only orders bought on a colony
///     console and delivered by a colony elevator count; GOVFOR, OPFOR and corporate purchases leave no record.
///     Using a casing, a gun or a forensic scanner that just scanned one on a records console compares its firing-pin
///     signature against the registry.
/// </summary>
public sealed partial class CMUFirearmRegistrySystem : EntitySystem
{
    private const string ColonyFaction = "colony";

    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private readonly List<CMUFirearmRegistryEntry> _entries = new();

    // Who bought each order still waiting on an elevator, by its place in the queue.
    private readonly Dictionary<(EntityUid Elevator, int Index), EntityUid> _buyers = new();

    // The firing-pin signature each forensic scanner last picked up, from a casing or a gun.
    private readonly Dictionary<EntityUid, string> _scannedSignatures = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _entries.Clear();
            _buyers.Clear();
            _scannedSignatures.Clear();
        });
        SubscribeLocalEvent<ForensicScannerScannedEvent>(OnScanned);
        SubscribeLocalEvent<CriminalRecordsConsoleComponent, InteractUsingEvent>(OnConsoleInteractUsing);
        SubscribeLocalEvent<CMURequisitionsOrderBoughtEvent>(OnOrderBought);
        SubscribeLocalEvent<CMURequisitionsCrateDeliveredEvent>(OnCrateDelivered);
        SubscribeLocalEvent<CriminalRecordsConsoleComponent, CMUFirearmRegistryRequestMsg>(OnRequest);
    }

    private void OnOrderBought(ref CMURequisitionsOrderBoughtEvent args)
    {
        // Dropped for anything else, so a non-colony order can't take over the slot's old buyer either.
        if (IsColony(args.ConsoleFaction) && IsColony(args.Elevator.Comp.Faction))
            _buyers[(args.Elevator.Owner, args.Index)] = args.Buyer;
        else
            _buyers.Remove((args.Elevator.Owner, args.Index));
    }

    private void OnCrateDelivered(ref CMURequisitionsCrateDeliveredEvent args)
    {
        // Removed either way, so a free crate later in the same slot isn't put down to the last buyer.
        if (!_buyers.Remove((args.Elevator.Owner, args.Index), out var buyer) || !IsColony(args.Elevator.Comp.Faction))
            return;

        var buyerName = Exists(buyer) ? Name(buyer) : Loc.GetString("cmu-firearm-registry-unknown-buyer");
        var time = _gameTicker.RoundDuration();
        var added = false;
        foreach (var gun in FindGuns(args.Crate))
        {
            var serial = gun.Comp;
            var meta = MetaData(gun);
            _entries.Add(new CMUFirearmRegistryEntry(
                serial.Serial,
                serial.Signature,
                meta.EntityName,
                meta.EntityPrototype?.ID,
                buyerName,
                args.OrderName,
                time));
            added = true;
        }

        if (added)
            SendToOpenConsoles();
    }

    private void OnScanned(ref ForensicScannerScannedEvent args)
    {
        if (GetSignature(args.Target) is { } signature)
            _scannedSignatures[args.Scanner] = signature;
        else
            _scannedSignatures.Remove(args.Scanner);
    }

    private string? GetSignature(EntityUid uid)
    {
        if (TryComp(uid, out CMUBallisticMarksComponent? marks) && marks.Signature.Length > 0)
            return marks.Signature;

        if (TryComp(uid, out CMUGunSerialNumberComponent? gun) && gun.Signature.Length > 0)
            return gun.Signature;

        return null;
    }

    private void OnConsoleInteractUsing(Entity<CriminalRecordsConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var signature = GetSignature(args.Used);
        if (signature == null && HasComp<ForensicScannerComponent>(args.Used))
            signature = _scannedSignatures.GetValueOrDefault(args.Used);

        if (signature == null)
            return;

        args.Handled = true;
        var match = _entries.FirstOrDefault(e => e.Signature == signature);
        _popup.PopupEntity(match != null
                ? Loc.GetString("cmu-firearm-registry-compare-match",
                    ("name", match.Name), ("serial", match.Serial), ("buyer", match.Buyer))
                : Loc.GetString("cmu-firearm-registry-compare-none", ("signature", signature)),
            ent,
            args.User,
            match != null ? PopupType.Medium : PopupType.Small);

        // The result popup is free, but showing the registry still takes the access the console normally asks for.
        if (!_access.IsAllowed(args.User, ent))
            return;

        _ui.OpenUi(ent.Owner, CriminalRecordsConsoleKey.Key, args.User);
        _ui.ServerSendUiMessage(ent.Owner, CriminalRecordsConsoleKey.Key, new CMUFirearmRegistryMsg(_entries), args.User);
        _ui.ServerSendUiMessage(ent.Owner, CriminalRecordsConsoleKey.Key, new CMUFirearmRegistryCompareMsg(signature), args.User);
    }

    private void OnRequest(Entity<CriminalRecordsConsoleComponent> ent, ref CMUFirearmRegistryRequestMsg args)
    {
        _ui.ServerSendUiMessage(ent.Owner, CriminalRecordsConsoleKey.Key, new CMUFirearmRegistryMsg(_entries), args.Actor);
    }

    private void SendToOpenConsoles()
    {
        var message = new CMUFirearmRegistryMsg(_entries);
        var consoles = EntityQueryEnumerator<CriminalRecordsConsoleComponent>();
        while (consoles.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, CriminalRecordsConsoleKey.Key))
                _ui.ServerSendUiMessage(uid, CriminalRecordsConsoleKey.Key, message);
        }
    }

    /// <summary>Every serial-numbered gun in the crate, including ones packed inside cases.</summary>
    private List<Entity<CMUGunSerialNumberComponent>> FindGuns(EntityUid crate)
    {
        var found = new List<Entity<CMUGunSerialNumberComponent>>();
        var pending = new Stack<EntityUid>();
        pending.Push(crate);
        while (pending.TryPop(out var uid))
        {
            if (uid != crate && TryComp(uid, out CMUGunSerialNumberComponent? serial) && serial.Serial.Length > 0)
                found.Add((uid, serial));

            if (!TryComp(uid, out ContainerManagerComponent? containers))
                continue;

            foreach (var container in containers.Containers.Values)
            {
                foreach (var contained in container.ContainedEntities)
                {
                    pending.Push(contained);
                }
            }
        }

        return found;
    }

    private static bool IsColony(string? faction)
    {
        return string.Equals(faction, ColonyFaction, StringComparison.OrdinalIgnoreCase);
    }
}
