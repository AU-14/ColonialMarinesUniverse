using System.Linq;
using Content.Shared._RMC14.Dropship;
using Content.Shared.AU14.Objectives.Capture;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.NPC.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Player;
using Content.Shared.Interaction;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;

namespace Content.Shared.AU14.Objectives.Capture;

public sealed class SharedCaptureObjectiveSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly Content.Shared._RMC14.Dropship.SharedDropshipSystem _dropshipSystem = default!;
    private static readonly ISawmill Sawmill = Logger.GetSawmill("capture-obj");

    // Tracks ongoing hoists to prevent multiple simultaneous hoists per structure
    private readonly HashSet<EntityUid> _hoisting = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CaptureObjectiveComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<CaptureObjectiveComponent, HoistFlagDoAfterEvent>(OnHoistFlagDoAfter);
    }



    private void OnInteractHand(EntityUid uid, CaptureObjectiveComponent comp, InteractHandEvent args)
    {
        if (args.Handled)
            return;
        if (_hoisting.Contains(uid))
        {
            args.Handled = true;
            // Popup logic should be handled in server/client system
            return;
        }
        if (!string.IsNullOrEmpty(comp.CurrentController))
        {
            args.Handled = true;
            // Popup logic should be handled in server/client system
            return;
        }
        if (!_entManager.TryGetComponent<NpcFactionMemberComponent>(args.User, out var npcFaction) || npcFaction.Factions.Count == 0)
        {
            args.Handled = true;
            // Popup logic should be handled in server/client system
            return;
        }
        // Only allow one faction per user
        var faction = npcFaction.Factions.First().ToString().ToUpperInvariant();
        _hoisting.Add(uid);
        args.Handled = true;
        // Raise event for popup logic
        var startedEvent = new FlagHoistStartedEvent(args.User, faction);
        RaiseLocalEvent(uid, startedEvent);
        var doAfterArgs = new DoAfterArgs(_entManager, args.User, comp.HoistTime, new HoistFlagDoAfterEvent { Faction = faction }, uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };
        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnHoistFlagDoAfter(EntityUid uid, CaptureObjectiveComponent comp, HoistFlagDoAfterEvent args)
    {
        _hoisting.Remove(uid);
        if (args.Cancelled || args.Handled)
            return;
        comp.CurrentController = args.Faction;
        // --- Begin: Update linked dropship destination's FactionController if Airfield is set ---
        if (!string.IsNullOrEmpty(comp.Airfield))
        {
            var airfieldId = comp.Airfield.ToLowerInvariant();
            var destQuery = _entManager.EntityQueryEnumerator<Content.Shared._RMC14.Dropship.DropshipDestinationComponent, MetaDataComponent>();
            while (destQuery.MoveNext(out var destUid, out _, out var meta))
            {
                var protoId = meta.EntityPrototype?.Name.ToLowerInvariant();
                if (protoId == airfieldId)
                {
                    _dropshipSystem.SetFactionController(destUid, args.Faction);
                }
            }
        }
        // --- End: Update linked dropship destination's FactionController if Airfield is set ---
        Sawmill.Info($"Flag at {uid} hoisted by {args.Faction}");
        // Raise event for popup logic
        var hoistedEvent = new FlagHoistedEvent(args.User, args.Faction);
        RaiseLocalEvent(uid, hoistedEvent);
        // Sprite and popup logic should be handled in server/client system
    }
}
