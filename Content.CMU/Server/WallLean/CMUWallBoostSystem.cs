using Content.Shared.CMU14.WallLean;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.WallLean;

/// <summary>
///     Someone leaning against a wall can give someone else a boost: they climb up on the leaner like a ladder and
///     end up on the level above, over the wall. Only outside, with something solid to climb onto up there.
/// </summary>
public sealed class CMUWallBoostSystem : EntitySystem
{
    private static readonly TimeSpan BoostDelay = TimeSpan.FromSeconds(4);

    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUWallLeaningComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUWallLeaningComponent, CMUWallBoostDoAfterEvent>(OnBoostDoAfter);
    }

    private void OnGetVerbs(Entity<CMUWallLeaningComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;
        if (!args.CanAccess || !args.CanInteract || user == ent.Owner || ent.Comp.Wall == null ||
            !HasComp<HumanoidProfileComponent>(user))
        {
            return;
        }

        var leaner = ent.Owner;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-wall-boost-verb"),
            Act = () => StartBoost(leaner, user),
        });
    }

    /// <summary>
    ///     Where the climber would end up: over the wall, on the level above. Null, with the reason, if they can't.
    /// </summary>
    private MapCoordinates? GetLanding(Entity<CMUWallLeaningComponent> leaner, EntityUid climber, out string? reason)
    {
        reason = "cmu-wall-boost-nothing-above";
        if (leaner.Comp.Wall is not { } wall || TerminatingOrDeleted(wall) ||
            Transform(climber).MapUid is not { } map)
        {
            return null;
        }

        if (_zLevels.HasOpaqueAbove(climber) || _zLevels.HasOpaqueAbove(leaner))
        {
            reason = "cmu-wall-boost-ceiling";
            return null;
        }

        var wallPos = _transform.GetWorldPosition(wall);
        if (!_zLevels.TryProjectToZMap(map, 1, wallPos, out var landing, out _) ||
            !_map.TryFindGridAt(landing, out var gridUid, out var grid) ||
            !_map.TryGetTileRef(gridUid, grid, landing.Position, out var tile) ||
            CMUZLevelOpeningCache.IsOpeningTile(tile.Tile, _tileDefinitions))
        {
            return null;
        }

        if (_turf.IsTileBlocked(tile, CollisionGroup.Impassable))
        {
            reason = "cmu-wall-boost-blocked";
            return null;
        }

        reason = null;
        return landing;
    }

    private bool CanBoost(EntityUid leaner, EntityUid climber, out string? reason)
    {
        reason = null;
        if (_mobState.IsIncapacitated(leaner) || _standing.IsDown(leaner))
            reason = "cmu-wall-boost-leaner-down";
        else if (_mobState.IsIncapacitated(climber) || _standing.IsDown(climber))
            reason = "cmu-wall-boost-climber-down";

        return reason == null;
    }

    private void StartBoost(EntityUid leaner, EntityUid climber)
    {
        if (!TryComp(leaner, out CMUWallLeaningComponent? leaning))
            return;

        if (!CanBoost(leaner, climber, out var reason) ||
            GetLanding((leaner, leaning), climber, out reason) == null)
        {
            _popup.PopupEntity(Loc.GetString(reason!), leaner, climber, PopupType.SmallCaution);
            return;
        }

        var args = new DoAfterArgs(EntityManager, climber, BoostDelay, new CMUWallBoostDoAfterEvent(), leaner, target: leaner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DistanceThreshold = 1.5f,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return;

        var climberName = Identity.Entity(climber, EntityManager);
        var leanerName = Identity.Entity(leaner, EntityManager);
        _popup.PopupEntity(Loc.GetString("cmu-wall-boost-start-self", ("leaner", leanerName)), leaner, climber);
        _popup.PopupEntity(Loc.GetString("cmu-wall-boost-start-leaner", ("climber", climberName)), leaner, leaner);
    }

    private void OnBoostDoAfter(Entity<CMUWallLeaningComponent> ent, ref CMUWallBoostDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var climber = args.User;
        if (!CanBoost(ent, climber, out var reason) ||
            GetLanding(ent, climber, out reason) is not { } landing)
        {
            _popup.PopupEntity(Loc.GetString(reason!), ent, climber, PopupType.SmallCaution);
            return;
        }

        if (!_zLevels.TryMove(climber, 1, worldPosition: landing.Position))
        {
            _popup.PopupEntity(Loc.GetString("cmu-wall-boost-nothing-above"), ent, climber, PopupType.SmallCaution);
            return;
        }

        var climberName = Identity.Entity(climber, EntityManager);
        _popup.PopupEntity(Loc.GetString("cmu-wall-boost-done-self"), climber, climber);
        _popup.PopupEntity(Loc.GetString("cmu-wall-boost-done-leaner", ("climber", climberName)), ent, ent);
    }
}
