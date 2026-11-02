// SPDX-License-Identifier: AGPL-3.0-only
using Content.Server.Stack;
using Content.Shared.CMU14.ZLevelBuilding.Pit;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.DoAfter;
using Content.Shared.Ghost.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.ZLevelBuilding.Pit;

// A ladder, or a ladder under a hatch, that was built in a pit can be taken down by whoever stands under it (the lower end of it, the one on the level below): a verb of its menu begins a
// work of thirty seconds; then the ladder and the hatch are gone, the pit is open as it was, and the metal that the ladder (or the hatch) cost comes back.
public sealed class LadderDismantleSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StackSystem _stack = default!;

    private const string LadderTopProto = "RUMCPitLadderDown";
    private const string HatchTopProto = "RUMCPitHatchDown";
    private const string PitProto = "RUMCPit";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PitLadderComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<PitLadderComponent, LadderDismantleDoAfterEvent>(OnDoAfter);
    }

    private void OnGetVerbs(Entity<PitLadderComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || HasComp<GhostComponent>(args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("au14-ladder-dismantle"),
            Act = () => StartDismantle(ent, user),
            Priority = 30,
        });
    }

    private void StartDismantle(Entity<PitLadderComponent> ent, EntityUid user)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.DismantleTime, new LadderDismantleDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("au14-ladder-dismantle-started"), ent, user);
    }

    private void OnDoAfter(Entity<PitLadderComponent> ent, ref LadderDismantleDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var user = args.User;
        var top = FindTop(ent);
        var isHatch = top != null && MetaData(top.Value).EntityPrototype?.ID == HatchTopProto;

        // Where the pit was, on the level above: it opens again when the ladder is gone.
        var topPlace = top is { } found ? PlaceOf(found) : null;

        if (top is { } gone)
            QueueDel(gone);

        QueueDel(ent.Owner);

        if (topPlace is { } place)
            Spawn(PitProto, place);

        var metal = PitSystem.GetMetalCost(isHatch ? PitBuildOption.Hatch : PitBuildOption.Ladder);
        if (metal > 0)
            _stack.SpawnNextToOrDrop(metal, PitSystem.MetalStackType, user);

        _popup.PopupEntity(Loc.GetString("au14-ladder-dismantle-done", ("amount", metal)), user, user);
    }

    // The tile on the level above where the top of the ladder stood.
    private EntityCoordinates? PlaceOf(EntityUid top)
    {
        var xform = Transform(top);
        return xform.GridUid == null ? null : xform.Coordinates;
    }

    // The top of the ladder: the hatch that it is linked to, or the ladder (or hatch) that stands at the same place on the level above.
    private EntityUid? FindTop(Entity<PitLadderComponent> ladder)
    {
        if (TryComp<HatchLinkComponent>(ladder, out var link) && Exists(link.Hatch) && !TerminatingOrDeleted(link.Hatch))
            return link.Hatch;

        var xform = Transform(ladder);
        if (xform.MapUid is not { } mapUid || !TryComp<CMUZLevelMapComponent>(mapUid, out var zMap) || zMap.MapAbove is not { } aboveMapUid)
            return null;

        if (!TryComp<MapComponent>(aboveMapUid, out var aboveMap))
            return null;

        var coords = new MapCoordinates(_transform.GetWorldPosition(xform), aboveMap.MapId);
        if (!_map.TryFindGridAt(coords, out var aboveGrid, out var aboveGridComp))
            return null;

        var tile = _map.TileIndicesFor(aboveGrid, aboveGridComp, coords);
        var anchored = _map.GetAnchoredEntitiesEnumerator(aboveGrid, aboveGridComp, tile);
        while (anchored.MoveNext(out var candidate))
        {
            var id = MetaData(candidate.Value).EntityPrototype?.ID;
            if (id == LadderTopProto || id == HatchTopProto)
                return candidate.Value;
        }

        return null;
    }
}
